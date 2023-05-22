using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Windows.Media;
using LibDmd.Frame;

namespace LibDmd.Input.PinballFX
{
	/// <summary>
	/// Polls for the Pinball FX3 process, grabs the DMD display from the memory.
	/// </summary>
	/// <remarks>
	/// Can be launched any time. Will wait with sending frames until Pinball FX3 is
	/// launched and stop sending when it exits.
	/// </remarks>
	public class PinballFX3MemoryGrabber : MemoryGrabber<DmdFrame>, IGray2Source, IDmdColorSource, IGameNameSource
	{
		public override string Name { get; } = "Pinball FX3";

		public IObservable<DmdFrame> GetGray2Frames()
		{
			return GetFrames();
		}

		// DMD Stuff
		private const int DMDWidth = 128;
		private const int DMDHeight = 32;
		private static readonly byte[] RawDMD = new byte[DMDWidth * DMDHeight];
		private byte[] _lastFrame;
		private readonly BehaviorSubject<Color> _dmdColor = new BehaviorSubject<Color>(Colors.OrangeRed);

		// adddresses in the target process
		private static Process _gameProc;
		private static IntPtr _pBaseAddress = IntPtr.Zero;
		private static IntPtr _dmdAddress = IntPtr.Zero;
		private static IntPtr _gameNameAddress = IntPtr.Zero;

		// Game Name
		private string _lastGameName;
		private readonly Subject<string> _gameName = new Subject<string>();

		public IObservable<Color> GetDmdColor() => _dmdColor.DistinctUntilChanged();

		public IObservable<string> GetGameName() => _gameName;

		protected override DmdFrame CaptureDMD()
		{
			// Initialize a new writeable bitmap to receive DMD pixels.
			var frame = new byte[DMDWidth * DMDHeight];

			// Check if a table is loaded... and retrieve DMD offset in memory.
			_dmdAddress = GetDMDOffset(_hProcess);

			// ..if not, return an empty frame (blank DMD).
			if (_dmdAddress == IntPtr.Zero) {
				return new DmdFrame(128, 32, frame, 2);
			}

			// Retrieve DMD color from memory.
			_dmdColor.OnNext(GetDMDColor(_hProcess));

			ReadGameName(_hProcess);

			// Grab the whole raw DMD block from game's memory.
			ReadProcessMemory(_hProcess, _dmdAddress, RawDMD, RawDMD.Length, IntPtr.Zero);

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
					if (pixelByte > 6) {
						return null;
					}

					// for williams it goes from 3 to 6
					if (pixelByte > 2) {
						pixelByte -= 3;

					} else if (pixelByte == 2) {
						// otherwise, Pinball FX doesn't seem to have more than pixels between 0-2, so make the shade 3 lighter.
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
			return identical ? null : new DmdFrame(128, 32, frame, 2);
		}

		private static IntPtr GetDMDOffset(IntPtr hProcess)
		{
			// Retrieve DMD offset in memory using pointers.
			var buf = new byte[4];
			ReadProcessMemory(hProcess, _pBaseAddress, buf, buf.Length, IntPtr.Zero);
			ReadProcessMemory(hProcess, B4ToPointer(buf) + 0xF0, buf, buf.Length, IntPtr.Zero);
			ReadProcessMemory(hProcess, B4ToPointer(buf) + 0x34, buf, buf.Length, IntPtr.Zero);
			return B4ToPointer(buf);
		}

		private static Color GetDMDColor(IntPtr hProcess)
		{
			// Retrieve DMD color in memory using pointers.
			// A few examples of what this memory looks like:
			// Yellow: 3C 7D D1 00 50 46 94 2F 00 33 33 FF 00 00 00 D0
			// Aqua:   3C 7D D1 00 C8 9F 6F 29 33 33 00 FF 00 00 00 D0
			var pAddress = new byte[4];
			var colorBytes = new byte[4];
			ReadProcessMemory(hProcess, _pBaseAddress, pAddress, pAddress.Length, IntPtr.Zero);
			ReadProcessMemory(hProcess, B4ToPointer(pAddress) + 0xF0, pAddress, pAddress.Length, IntPtr.Zero);
			ReadProcessMemory(hProcess, B4ToPointer(pAddress) + 0x5C, pAddress, pAddress.Length, IntPtr.Zero);
			ReadProcessMemory(hProcess, B4ToPointer(pAddress) + 0x8, colorBytes, colorBytes.Length, IntPtr.Zero);
			if (BitConverter.IsLittleEndian) Array.Reverse(colorBytes);
			var colorCode = BitConverter.ToInt32(colorBytes, 0);

			// Switch among color codes in memory and return corresponding RGB color (hex).
			switch (colorCode) {
				case 0x003333FF: // Yellow
					return Color.FromRgb(0xff, 0xff, 0);
				case 0x330033FF: // Magenta
					return Color.FromRgb(0xff, 0x00, 0xff);
				case 0x333300FF: // Aqua
					return Color.FromRgb(0x00, 0xff, 0xff);
				case 0x113300FF: // Green
					return Color.FromRgb(0x00, 0xff, 0x66);
				case 0x331111FF: // Blue
					return Color.FromRgb(0x66, 0x66, 0xff);
				case 0x001133FF: // Orange
					return Color.FromRgb(0xff, 0x66, 0x00);
				case 0x003311FF: // Bright Green
					return Color.FromRgb(0x66, 0xff, 0x00);
				case 0x111133FF: // Salmon
					return Color.FromRgb(0xff, 0x66, 0x66);
				default: // Cannot get DMD color for some reason...
					return Colors.OrangeRed;
			}
		}

		private void ReadGameName(IntPtr hProcess)
		{
			if (_gameNameAddress == IntPtr.Zero && _gameProc != null) {
				// Locating the game name didn't work before, try again now that the DMD is loaded.
				_gameNameAddress = GetGameNameAddress(_gameProc);
				_gameProc = null;
			}

			var buff = new byte[128];
			ReadProcessMemory(hProcess, _gameNameAddress, buff, buff.Length, IntPtr.Zero);

			var name = Encoding.ASCII.GetString(buff).Trim('\0');
			if (!name.Contains(Path)) {
				return;
			}

			name = name.Split(':')[0];

			var identical = name == _lastGameName;
			_lastGameName = name;

			if (!identical) {
				Logger.Info($"Found Pinball FX3 game: {name}");
				_gameName.OnNext(name);
			}
		}

		protected override IntPtr AttachGameProcess(Process p)
		{
			if (p.ProcessName == "Pinball FX3") {
				return GetPointerBaseAddress(p);
			}

			return IntPtr.Zero;
		}

		// Byte pattern we use to identify the DMD memory struct in the FX3 process
		private static readonly byte[] DMDPointerSig = new byte[] { 0x8B, 0x81, 0xFF, 0xFF, 0xFF, 0xFF, 0x89, 0x45, 0xFF, 0x8B, 0x81, 0xFF, 0xFF, 0xFF, 0xFF, 0x89, 0x45, 0xFF, 0xA1 };
		private IntPtr GetPointerBaseAddress(Process gameProc)
		{
			// Open the process for wait and read operations
			var processHandle = OpenProcess(SYNCHRONIZE | PROCESS_VM_READ, false, gameProc.Id);
			if (processHandle == IntPtr.Zero) {
				return IntPtr.Zero;
			}

			// Find DMD pointer base address offset in memory with its signature pattern.
			IntPtr baseOffset = FindPattern(gameProc, BaseAddress(gameProc), gameProc.MainModule.ModuleMemorySize, DMDPointerSig, 19);
			var pointerBuf = new byte[4];
			ReadProcessMemory(gameProc.Handle, baseOffset, pointerBuf, pointerBuf.Length, IntPtr.Zero);
			_pBaseAddress = B4ToPointer(pointerBuf);

			_gameNameAddress = GetGameNameAddress(gameProc);
			_gameProc = gameProc;

			// Return game's process handle.
			return processHandle;
		}

		// Byte pattern we use to identify the game name in the FX3 process
		private const string Path = ":/meta_steam_pfx3/skin/";
		private static readonly byte[] PathSig = Encoding.ASCII.GetBytes(Path);
		private const string PrefixNulls = "\0\0\0";
		private IntPtr GetGameNameAddress(Process gameProc)
		{
			// Find game name pointer base address offset in memory with its signature pattern.
			IntPtr offset = FindPattern(gameProc, BaseAddress(gameProc), gameProc.MainModule.ModuleMemorySize, PathSig, PathSig.Length);

			if (offset == IntPtr.Zero) {
				return IntPtr.Zero;
			}

			var buff = new byte[128];
			const int prefixPadding = 64;

			// Find the path signature in memory. It looks something like:
			// "\0\0\0WMS_Getaway:/meta_steam_pfx3/skin/skin/n/in/team_pfx3/skin/
			ReadProcessMemory(gameProc.Handle, offset - prefixPadding - PathSig.Length, buff, buff.Length, IntPtr.Zero);

			var buffStr = Encoding.ASCII.GetString(buff);

			var pathIndex = buffStr.IndexOf(Path, StringComparison.Ordinal);

			if (pathIndex == -1) {
				return IntPtr.Zero;
			}

			// Seek backwards to the start of the string
			var startIndex = buffStr.LastIndexOf(PrefixNulls, pathIndex, StringComparison.Ordinal);
			if (startIndex == -1) {
				return IntPtr.Zero;
			}

			return offset - prefixPadding + startIndex + PrefixNulls.Length - PathSig.Length;
		}
	}
}
