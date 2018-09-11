using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LibDmd.Common;
using NLog;

namespace LibDmd.Input.TPAGrabber
{
	/// <summary>
	/// Polls for the Pinball Arcade DX11 process, grabs the DMD display from the memory.
	/// </summary>
	/// <remarks>
	/// Can be launched any time. Will wait with sending frames until Pinball Arcade DX11 is
	/// launched and stop sending when it exits.
	/// </remarks>
	public class TPAGrabber : AbstractSource, IGray4Source
	{
		public override string Name { get; } = "Pinball Arcade DX11";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		/// <summary>
		/// Wait time between polls for the Pinball Arcade DX11 process. Stops polling as soon
		/// as the process is found.
		///
		/// Can be set quite high, just about as long as it takes for Pinball Arcade DX11 to start.
		/// </summary>
		public TimeSpan PollForProcessDelay { get; set; } = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Frequency with which frames are pulled off the display.
		/// </summary>
		public double FramesPerSecond { get; set; } = 25;

		private IConnectableObservable<byte[]> _framesGray4;
		private IDisposable _capturer;
		private IntPtr _handle;
		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// DMD Stuff + Codecave
		private const int DMDWidth = 128;
		private const int DMDHeight = 32;
		private const int LineJump = 0x400;
		private const int MemBlockSize = 0x1FC02;
		private static readonly byte[] RawDMD = new byte[MemBlockSize];

		private static readonly byte[] DMDCreationSignature = new byte[] { 0x0F, 0xB6, 0x16, 0x8B, 0x75, 0xF0, 0xD3, 0xEA, 0x83, 0xE2, 0x01, 0x03, 0xFA };
		private static readonly byte[] GameStateSignature = new byte[] { 0xC7, 0x05, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xC7, 0x87, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x74, 0x14, 0xC7, 0x05, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00, 0xC7, 0x05 };

		private static IntPtr _dmdPatch = IntPtr.Zero;
		private static IntPtr _gameState = IntPtr.Zero;
		private static IntPtr _codeCave = IntPtr.Zero;
		private static IntPtr _gameBase = IntPtr.Zero;

		private byte[] _lastFrame;

		/// <summary>
		/// Waits for the Pinball Arcade DX11 process.
		/// </summary>
		/// 
		private void StartPolling()
		{
			// We need debug privileges to snoop on PBA's memory space
			SetDebugPrivilege();

			Logger.Info("Waiting for Pinball Arcade DX11 to spawn...");
			var success = new Subject<Unit>();
			Observable
				.Timer(TimeSpan.Zero, PollForProcessDelay)
				.TakeUntil(success)
				.Subscribe(x => {
					_handle = FindGameHandle();
					if (_handle != IntPtr.Zero) {
						StartCapturing();
						success.OnNext(Unit.Default);
					}
				});
		}

		/// <summary>
		/// Starts sending frames.
		/// </summary>
		private void StartCapturing()
		{
			_capturer = _framesGray4.Connect();
			_onResume.OnNext(Unit.Default);
		}

		/// <summary>
		/// Stops sending frames because we couldn't aquire the game handle anymore,
		/// usually because Pinball Arcade DX11 was closed.
		/// </summary>
		private void StopCapturing()
		{
			// TODO send blank frame
			_capturer.Dispose();
			_onPause.OnNext(Unit.Default);
			StartPolling();
		}

		public IObservable<byte[]> GetGray4Frames()
		{
			if (_framesGray4 != null) {
				return _framesGray4;
			}
			_framesGray4 = Observable
				.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
				.Select(x => CaptureDMD())
				.Where(frame => frame != null)
				.Publish();

			StartPolling();
			return _framesGray4;
		}

		public byte[] CaptureDMD()
		{
			// if the process has exited, stop capture
			if (WaitForSingleObject(_handle, 0) == WAIT_OBJECT_0)
			{
				CloseHandle(_handle);
				_handle = IntPtr.Zero;
				StopCapturing();
				return null;
			}

			// Initialize a new writeable bitmap to receive DMD pixels.
			var frame = new byte[DMDWidth * DMDHeight];

			// Check if a table is loaded..
			var tableLoaded = new byte[1];
			ReadProcessMemory((int)_handle, (int)_gameBase + (int)_gameState, tableLoaded, 1, 0);

			// ..if not, return an empty frame (blank DMD).
			if (tableLoaded[0] == 0) {
				return frame;
			}

			// Retrieve the DMD entrypoint from EAX registry (returned by our codecave).
			var eax = new byte[4];
			ReadProcessMemory((int)_handle, (int)_codeCave, eax, 4, 0);

			// Now we have our DMD location in memory + little hack to re-align the DMD block.
			var dmdOffset = BitConverter.ToInt32(eax, 0) - 0x1F406;

			// Grab the whole raw DMD block from game's memory.
			ReadProcessMemory((int)_handle, dmdOffset, RawDMD, MemBlockSize + 2, 0);

			// Check the DMD CRC flag, skip the frame if the value is incorrect.
			if (RawDMD[0] != 0x02) return null;

			// Used to parse pixel bytes of the DMD memory block, starting at 2 to skip the flag bytes.
			var rawPixelIndex = 2;

			var identical = true;

			// For each pixel on Y axis.
			for (var dmdY = 0; dmdY < DMDHeight; dmdY++) {

				// For each pixel on X axis.
				for (var dmdX = 0; dmdX < DMDWidth; dmdX++) {

					// RGB to BGR
					double hue, sat, lum;
					ColorUtil.RgbToHsl(RawDMD[rawPixelIndex], RawDMD[rawPixelIndex + 1], RawDMD[rawPixelIndex + 2], out hue, out sat, out lum);

					var pos = dmdY * DMDWidth + dmdX;
					//var pixel = (byte)Math.Max(0, lum * 15 * 3 - 12); // [ 0,4,10,15 ]
					//var pixel = (byte)(lum * 15 * 1.5);               // [ 0,8,11,13 ]
					var pixel = (byte)(lum * 15 * 1.6);                 // [ 0,8,12,14 ]

					// drop garbage frames
					if (pixel > 15) {
						return null;
					}

					if (identical && (_lastFrame == null || _lastFrame[pos] == pixel)) {
						identical = false;
					}
					frame[pos] = pixel;

					// Each pixel takes 4 bytes of data in memory, advance 2 pixels.
					rawPixelIndex += 8;
				}
				// Jump to the next DMD line.
				rawPixelIndex += LineJump * 2 + DMDWidth * 8;
			}
			_lastFrame = frame;

			// Return the DMD bitmap we've created or null if frame was identical to previous.
			return identical ? null : frame;
		}

		// Check if the game is started and return its process handle.
		private static IntPtr FindGameHandle()
		{
			var processList = Process.GetProcesses();
			foreach (var p in processList) {
				if (p.ProcessName == "PinballArcade11" || p.ProcessName == "PinballArcadeCabinet") {
					// When the process is found, find needed offsets..
					FindOffsets(p);
					// ...then write the codecave.
					var processHandle = PatchCodeCave(p);
					return processHandle;
				}
			}
			return IntPtr.Zero;
		}

		// Helper function to retrieve process base address.
		private static IntPtr BaseAddress(Process process)
		{
			var procMod = process.MainModule;
			return procMod.BaseAddress;
		}

		// Not fully commented.. basically we're creating a codecave to let the game retrieve for us the DMD location in memory.
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static IntPtr PatchCodeCave(Process gameProc)
		{
			// Defines offset address of our codecave.
			_gameBase = BaseAddress(gameProc);
			var patchOffset = _gameBase + (int)_dmdPatch;

			// Access rights to the process.
			const int PROCESS_VM_OPERATION = 0x0008;
			const int PROCESS_VM_READ = 0x0010;
			const int PROCESS_VM_WRITE = 0x0020;
			const int SYNCHRONIZE = 0x00100000;

			// Open the process to allow memory operations + return process handle.
			var processHandle = OpenProcess(SYNCHRONIZE | PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, gameProc.Id);
			if (processHandle == IntPtr.Zero) {
				return processHandle;
			}

			// Allocating memory to write the codecave.
			_codeCave = VirtualAllocEx(processHandle, IntPtr.Zero, 0x100, 0x1000, 0x40);
			var mem = BitConverter.GetBytes((int)_codeCave);

			// Build the JMP to the original code.
			var joc = ASMJump(_codeCave + 17, patchOffset + 13);

			// Build the codecave.
			var dmdCodeCave = new byte[] {
				0x0F, 0xB6, 0x16,                     // MOVZX EDX,BYTE PTR DS:[ESI]
				0x8B, 0x75, 0xF0,                     // MOV ESI,DWORD PTR SS:[EBP-10]
				0xD3, 0xEA,                           // SHR EDX,CL
				0x83, 0xE2, 0x01,                     // AND EDX,1
				0x03, 0xFA,                           // ADD EDI,EDX
				0xE9, joc[1], joc[2], joc[3], joc[4]  // JMP > ORIGINAL CODE
			};

			// Write the codecave into memory.
			WriteProcessMemory(processHandle, _codeCave + 4, dmdCodeCave, dmdCodeCave.Length, 0);

			// Build the JMP to the codecave.
			var jcc = ASMJump(patchOffset + 5, _codeCave + 4);

			// Build the jump to the codecave.
			var jmpToCC = new byte[] {
				0xA3, mem[0], mem[1], mem[2], mem[3], // MOV DWORD PTR DS:[XXXXXXXX],EAX
				0xE9, jcc[1], jcc[2], jcc[3], jcc[4], // JMP > CODECAVE
				0x90, 0x90, 0x90                      // NOP NOP NOP
			};
			// Write the jump into memory.
			WriteProcessMemory(processHandle, patchOffset, jmpToCC, jmpToCC.Length, 0);

			// Return the process handle.
			return processHandle;
		}

		// Function to calculate the 5-byte array for an ASM JMP.
		private static byte[] ASMJump(IntPtr location, IntPtr destination)
		{
			var JMPbytes = new byte[4];
			JMPbytes = BitConverter.GetBytes((UInt64)destination - (UInt64)location - (UInt64)5);
			return new byte[] { 0xE9, JMPbytes[0], JMPbytes[1], JMPbytes[2], JMPbytes[3] };
		}

		private static void FindOffsets(Process gameProc)
		{
			// Get game process base address
			var gameBase = (int)BaseAddress(gameProc);

			// Retrieve DMD creation offset
			_dmdPatch = FindPattern(gameProc, gameBase, 0xFFFFFF, DMDCreationSignature, 0) - gameBase;

			// Retrieve game state pointer + offset
			var gameStatePointer = FindPattern(gameProc, gameBase, 0xFFFFFF, GameStateSignature, 34);
			var pointerOffset = new byte[4];
			ReadProcessMemory((int)gameProc.Handle, (int)gameStatePointer, pointerOffset, pointerOffset.Length, 0);
			_gameState = new IntPtr(BitConverter.ToInt32(pointerOffset, 0) - gameBase);
		}

		// Function to search byte pattern in process memory then return its offset.
		private static IntPtr FindPattern(Process gameProc, int gameBase, int size, byte[] bytePattern, int Offset)
		{
			// Create a byte array to store memory region.
			var memoryRegion = new byte[size];

			// Dump process memory into the array. 
			ReadProcessMemory((int)gameProc.Handle, gameBase, memoryRegion, size, 0);

			// Loop into dumped memory region to find the pattern.
			for (var x = 0; x < memoryRegion.Length; x++) {

				// If we find the first pattern's byte in memory, loop through the entire array.
				for (var y = 0; y < bytePattern.Length; y++) {

					// If pattern byte is 0xFF, this is a joker, continue pattern loop.
					if (bytePattern[y] == 0xFF) {
						continue;
					}
					// If pattern byte is different than memory byte, we're not at the right place, back to the memory region loop...
					if (bytePattern[y] != memoryRegion[x + y]) {
						break;
					}
					// We've reached the end of the pattern array, we've found the offset.
					if (y == bytePattern.Length - 1)
						return new IntPtr(gameBase + Offset + x); // Return the offset.
				}
			}
			// We've reached the end of memory region, offset not found.
			return IntPtr.Zero;
		}

		#region Dll Imports

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
		public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] buffer, int size, int lpNumberOfBytesRead);

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
		static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, int lpNumberOfBytesWritten);

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
		public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
		public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int flAllocationType, int flProtect);

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
		public static extern UInt32 WaitForSingleObject(IntPtr hWaitHandle, UInt32 dwMilliseconds);
		const UInt32 INFINITE = 0xFFFFFFFF;
		const UInt32 WAIT_ABANDONED = 0x00000080;
		const UInt32 WAIT_OBJECT_0 = 0x00000000;
		const UInt32 WAIT_TIMEOUT = 0x00000102;

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto, SetLastError = true)]
		static extern bool CloseHandle(IntPtr handle);
		
		#endregion

	}
}