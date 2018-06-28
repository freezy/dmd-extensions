using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Media;
using NLog;

namespace LibDmd.Input.PinballFX
{
	/// <summary>
	/// Polls for the Pinball FX3 process, grabs the DMD display from the memory.
	/// </summary>
	/// <remarks>
	/// Can be launched any time. Will wait with sending frames until Pinball FX3 is
	/// launched and stop sending when it exits.
	/// </remarks>
	public class PinballFX3MemoryGrabber : AbstractSource, IGray2Source
	{
		public override string Name { get; } = "Pinball FX3";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		/// <summary>
		/// Wait time between polls for the Pinball FX3 process. Stops polling as soon
		/// as the process is found.
		///
		/// Can be set quite high, just about as long as it takes for Pinball FX3 to start.
		/// </summary>
		public TimeSpan PollForProcessDelay { get; set; } = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Frequency with which frames are pulled off the memory.
		/// </summary>
		public double FramesPerSecond { get; set; } = 60;

		private IConnectableObservable<byte[]> _framesGray2;
		private IDisposable _capturer;
		private IntPtr _handle;
		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// DMD Stuff
		private const int DMDWidth = 128;
		private const int DMDHeight = 32;
		private static readonly byte[] RawDMD = new byte[DMDWidth * DMDHeight];

		private static readonly byte[] DMDPointerSig = new byte[] { 0x83, 0xB8, 0xE4, 0x00, 0x00, 0x00, 0x00, 0x74, 0x34, 0x8B, 0x0D, 0xFF, 0xFF, 0xFF, 0xFF, 0xE8, 0xFF, 0xFF, 0xFF, 0xFF, 0x84, 0xC0, 0x75, 0x25, 0xA1 };
		private static IntPtr _pBaseAddress = IntPtr.Zero;
		private static IntPtr _dmdOffset = IntPtr.Zero;
		private static IntPtr _gameBase = IntPtr.Zero;
		private static String _dmdColor = String.Empty;

		private byte[] _lastFrame;

		/// <summary>
		/// Waits for the Pinball FX3 process.
		/// </summary>
		/// 
		private void StartPolling()
		{
			var curIdentity = WindowsIdentity.GetCurrent();
			var myPrincipal = new WindowsPrincipal(curIdentity);
			if (!myPrincipal.IsInRole(WindowsBuiltInRole.Administrator)) {
				throw new AdminRightsRequiredException("You need to run this as Administrator if you want to grab the DMD from the Pinball FX3's memory.");
			}

			Logger.Info("Waiting for Pinball FX3 to spawn...");
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
			Logger.Info($"Reading DMD data from Pinball FX3's memory at {FramesPerSecond} fps...");
			_capturer = _framesGray2.Connect();
			_onResume.OnNext(Unit.Default);
		}

		/// <summary>
		/// Stops sending frames because we couldn't aquire the game handle anymore,
		/// usually because Pinball FX3 was closed.
		/// </summary>
		private void StopCapturing()
		{
			// TODO send blank frame
			_capturer.Dispose();
			_onPause.OnNext(Unit.Default);
			StartPolling();
		}

		public IObservable<byte[]> GetGray2Frames()
		{
			if (_framesGray2 != null)
			{
				return _framesGray2;
			}
			_framesGray2 = Observable
				.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
				.Select(x => CaptureDMD())
				.Where(frame => frame != null)
				.Publish();

			StartPolling();
			return _framesGray2;
		}
		
		public byte[] CaptureDMD()
		{
			// Initialize a new writeable bitmap to receive DMD pixels.
			var frame = new byte[DMDWidth * DMDHeight];

			// Check if a table is loaded... and retrieve DMD offset in memory.
			_dmdOffset = GetDMDOffset((int)_handle);

			// ..if not, return an empty frame (blank DMD).
			if (_dmdOffset == IntPtr.Zero) {
				return frame;
			}

			// Retrieve DMD color from memory.
			_dmdColor = GetDMDColor((int)_handle); // Return RGB hex color value of DMD (return null value if the color cannot be retrieved). 
												   // TODO - APPLY COLOR TO THE DMD

			// Grab the whole raw DMD block from game's memory.
			ReadProcessMemory((int)_handle, (int)_dmdOffset, RawDMD, RawDMD.Length, 0);

			// Used to parse pixel bytes of the DMD memory block.
			var rawPixelIndex = 0;

			var identical = true;

			// For each pixel on Y axis.
			for (var dmdY = 0; dmdY < DMDHeight; dmdY++) {

				// For each pixel on X axis.
				for (var dmdX = 0; dmdX < DMDWidth; dmdX++) {

					var pos = dmdY * DMDWidth + dmdX;

					var pixelByte = RawDMD[rawPixelIndex];

					// drop garbage frames
					if (pixelByte > 3) {
						return null;
					}

					// Pinball FX doesn't seem to have more than pixels between 0-2, so make the shade 3 lighter.
					if (pixelByte == 2) {
						pixelByte++;
					}

					// set pixel
					frame[pos] = Math.Max((byte)0, Math.Min((byte)3, pixelByte));

					// check for identical frame
					if (identical && (_lastFrame == null || _lastFrame[pos] == frame[pos])) {
						identical = false;
					}

					// Each pixel takes 1 byte of data in memory, advance 1 pixel.
					rawPixelIndex += 1;
				}
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
				if (p.ProcessName == "Pinball FX3") {
					// When the process is found, retrieve DMD pointer base address and return process handle.
					return GetPointerBaseAddress(p); // This func returns process handle.
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

		private static IntPtr GetDMDOffset(int processHandle)
		{
			// Retrieve DMD offset in memory using pointers.
			var pAddress = new byte[4];
			ReadProcessMemory(processHandle, (int)_gameBase + (int)_pBaseAddress, pAddress, pAddress.Length, 0);
			ReadProcessMemory(processHandle, BitConverter.ToInt32(pAddress, 0) + 0xE8, pAddress, pAddress.Length, 0);
			ReadProcessMemory(processHandle, BitConverter.ToInt32(pAddress, 0) + 0x34, pAddress, pAddress.Length, 0);
			return new IntPtr(BitConverter.ToInt32(pAddress, 0));
		}

		private static string GetDMDColor(int processHandle)
		{
			// Retrieve DMD color in memory using pointers.
			var pAddress = new byte[4];
			var colorBytes = new byte[4];
			ReadProcessMemory(processHandle, (int)_gameBase + (int)_pBaseAddress, pAddress, pAddress.Length, 0);
			ReadProcessMemory(processHandle, BitConverter.ToInt32(pAddress, 0) + 0xE8, pAddress, pAddress.Length, 0);
			ReadProcessMemory(processHandle, BitConverter.ToInt32(pAddress, 0) + 0x64, pAddress, pAddress.Length, 0);
			ReadProcessMemory(processHandle, BitConverter.ToInt32(pAddress, 0) + 0x184, pAddress, pAddress.Length, 0);
			ReadProcessMemory(processHandle, BitConverter.ToInt32(pAddress, 0), colorBytes, colorBytes.Length, 0);
			if (BitConverter.IsLittleEndian) Array.Reverse(colorBytes);
			var colorCode = BitConverter.ToInt32(colorBytes, 0);

			// Switch among color codes in memory and return corresponding RGB color (hex).
			switch (colorCode) {
				case 0x003333FF: // Yellow
					return "#FFFF00";
				case 0x330033FF: // Magenta
					return "#FF00FF";
				case 0x333300FF: // Aqua
					return "#00FFFF";
				case 0x113300FF: // Green
					return "#00FF66";
				case 0x331111FF: // Blue
					return "#6666FF";
				case 0x001133FF: // Orange
					return "#FF6600";
				case 0x003311FF: // Bright Green
					return "#66FF00";
				case 0x111133FF: // Salmon
					return "#FF6666";
				default: // Cannot get DMD color for some reason...
					return null;
			}
		}

		private static IntPtr GetPointerBaseAddress(Process gameProc)
		{
			// Get game process base address.
			_gameBase = BaseAddress(gameProc);

			// Read access rights to the process.
			const int PROCESS_VM_READ = 0x0010;

			// Open the process to allow memory operations.
			var processHandle = OpenProcess(PROCESS_VM_READ, false, gameProc.Id);

			// Find DMD pointer base address offset in memory with its signature pattern.
			IntPtr baseOffset = FindPattern(gameProc, (int)BaseAddress(gameProc), 0xFFFFFF, DMDPointerSig, 25);
			var offsetBytes = new byte[4];
			ReadProcessMemory((int)gameProc.Handle, (int)baseOffset, offsetBytes, offsetBytes.Length, 0);
			_pBaseAddress = new IntPtr(BitConverter.ToInt32(offsetBytes, 0) - (int)_gameBase);

			// Return game's process handle.
			return processHandle;
		}

		// Function to search byte pattern in process memory then return its offset.
		private static IntPtr FindPattern(Process gameProc, int gameBase, int size, byte[] bytePattern, int offset)
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
						return new IntPtr(gameBase + offset + x); // Return the offset.
				}
			}
			// We've reached the end of memory region, offset not found.
			return IntPtr.Zero;
		}

		#region Dll Imports

		[DllImport("kernel32.dll")]
		public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] buffer, int size, int lpNumberOfBytesRead);

		[DllImport("kernel32.dll")]
		static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, int lpNumberOfBytesWritten);

		[DllImport("kernel32.dll")]
		public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll")]
		public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int flAllocationType, int flProtect);

		#endregion

	}

	public class AdminRightsRequiredException : Exception
	{
		public AdminRightsRequiredException(string message) : base(message)
		{
		}
	}
}