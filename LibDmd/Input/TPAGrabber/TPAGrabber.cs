using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
	public class TPAGrabber : IFrameSource
	{
		public string Name { get; } = "Pinball Arcade DX11";

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

		private IConnectableObservable<BitmapSource> _frames;
		private IDisposable _capturer;
		private IntPtr _handle;
		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// DMD Stuff + Codecave
		private const int MemBlockSize = 203772;
		private const int LineJump = 0x404;
		private const int DMDWidth = 256;
		private const int DMDHeight = 65;
		private static readonly byte[] RawDMD = new byte[MemBlockSize];

		private const int Patch = 0x001ADB4D;     // Working with v1.49.9 DX11
		private const int GameState = 0x0062728D; // Working with v1.49.9 DX11
		private static IntPtr _codeCave = IntPtr.Zero;
		private static IntPtr _gameBase = IntPtr.Zero;

		/// <summary>
		/// Waits for the Pinball Arcade DX11 process.
		/// </summary>
		private void StartPolling()
		{
			var curIdentity = WindowsIdentity.GetCurrent();
			var myPrincipal = new WindowsPrincipal(curIdentity);
			if (!myPrincipal.IsInRole(WindowsBuiltInRole.Administrator)) {
				throw new AdminRightsRequiredException("You need to run this as Administrator if you want to grab the DMD from the Pinball Arcade's memory.");
			}

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
			_capturer = _frames.Connect();
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

		public IObservable<BitmapSource> GetFrames()
		{
			if (_frames == null) {
				_frames = Observable
					.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
					.Do(l => Console.WriteLine("--> [{0}] Tick", l))
					.Select(x => CaptureDMD())
					.Where(bmp => bmp != null)
					.Do(l => Console.WriteLine("-->      Captured", l))
					.Publish();
				StartPolling();
			}
			return _frames;
		}

		public BitmapSource CaptureDMD()
		{
			// Initialize a new writeable bitmap to receive DMD pixels.
			var wBmp = new WriteableBitmap(DMDWidth, DMDHeight, 96, 96, PixelFormats.Bgr32, null);

			// Check if a table is loaded..
			var tableLoaded = new byte[1];
			ReadProcessMemory((int)_handle, (int)_gameBase + GameState, tableLoaded, 1, 0);

			// ..if not, return an empty frame (blank DMD).
			if (tableLoaded[0] == 0) {
				wBmp.Freeze();
				return wBmp;
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

			// Lock the writeable bitmap to expose the backbuffer to other threads.
			wBmp.Lock();

			// Used to parse pixel bytes of the DMD memory block, starting at 2 to skip the flag bytes.
			var pixelIndex = 2;

			// For each pixel on Y axis.
			for (var dmdY = 0; dmdY < DMDHeight - 1; dmdY++) {
				// For each pixel on X axis.
				for (var dmdX = 0; dmdX < DMDWidth - 1; dmdX++) {

					// We got to use unsafe code, pointers are much faster for this kind of low level image processing.
					unsafe {
						// Get a pointer to the back buffer.
						var pBackBuffer = (int)wBmp.BackBuffer;

						// Find the address of the pixel to draw.
						pBackBuffer += dmdY * wBmp.BackBufferStride;
						pBackBuffer += dmdX * 4;

						// Compute the pixel's color..
						var colorData = RawDMD[pixelIndex] << 16; // R
						colorData |= RawDMD[pixelIndex + 1] << 8; // G
						colorData |= RawDMD[pixelIndex + 2] << 0; // B      

						// Assign the color data to the pixel.
						*((int*)pBackBuffer) = colorData;
					}
					// Specify the area of the bitmap that changed.
					wBmp.AddDirtyRect(new Int32Rect(dmdX, dmdY, 1, 1));

					// Each pixel takes 4 bytes of data in memory, jump to next pixel.
					pixelIndex += 4;
				}
				// Jump to the next DMD line.
				pixelIndex += LineJump;
			}
			// We're done, release the backbuffer and make it available for display.
			wBmp.Unlock();

			// Freeze the DMD bitmap and make it readable to any thread.
			wBmp.Freeze();

			// Return the DMD bitmap we've created.
			return wBmp;
		}

		// Check if the game is started and return its process handle.
		private static IntPtr FindGameHandle()
		{
			var processList = Process.GetProcesses();
			foreach (var p in processList) {
				if (p.ProcessName == "PinballArcade11") {
					// When the process is found, write the codecave.
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
		private static IntPtr PatchCodeCave(Process gameProc)
		{
			// Defines offset address of our codecave.
			_gameBase = BaseAddress(gameProc);
			var patchOffset = _gameBase + Patch;

			// Access rights to the process.
			const int PROCESS_VM_OPERATION = 0x0008;
			const int PROCESS_VM_READ = 0x0010;
			const int PROCESS_VM_WRITE = 0x0020;

			// Open the process to allow memory operations + return process handle.
			var processHandle = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, gameProc.Id);

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