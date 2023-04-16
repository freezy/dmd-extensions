using System;
using System.Diagnostics;
using System.Text;

namespace LibDmd.Input.PinballFX
{
	/// <summary>
	/// Polls for the Pinball FX3 process, grabs the current game name from memory.
	/// </summary>
	/// <remarks>
	/// Can be launched any time. Will wait with sending names until Pinball FX3 is
	/// launched and stop sending when it exits.
	/// </remarks>
	public class PinballFX3GameNameMemoryGrabber : PinabllFX3MemoryGrabberBase<String>
	{
		private static IntPtr _nameAddress = IntPtr.Zero;
		private string _lastName;

		public override string Name { get; } = "Pinball FX3 Game Name";

		public PinballFX3GameNameMemoryGrabber()
		{
			FramesPerSecond = 1;
		}

		protected override string CaptureDMD()
		{
			var buff = new byte[128];
			ReadProcessMemory(_hProcess, _nameAddress, buff, buff.Length, IntPtr.Zero);

			var name = Encoding.ASCII.GetString(buff).Trim('\0');
			if (!name.Contains(path)) {
				return null;
			}

			name = name.Split(':')[0];

			var identical = name == _lastName;
			_lastName = name;

			if (!identical) {
				Console.WriteLine("Found name!: {0}", name);
				return name;
			}

			return null;
		}

		private static readonly string path = ":/meta_steam_pfx3/skin/";
		private static readonly byte[] pathSig = Encoding.ASCII.GetBytes(path);
		private static readonly string prefixNulls = "\0\0\0";

		protected override IntPtr GetPointerBaseAddress(Process gameProc)
		{
			// Open the process for wait and read operations
			var processHandle = OpenProcess(SYNCHRONIZE | PROCESS_VM_READ, false, gameProc.Id);
			if (processHandle == IntPtr.Zero) {
				return processHandle;
			}

			// Find game name pointer base address offset in memory with its signature pattern.
			IntPtr offset = FindPattern(gameProc, BaseAddress(gameProc), gameProc.MainModule.ModuleMemorySize, pathSig, pathSig.Length);

			if (offset == IntPtr.Zero) {
				return offset;
			}

			var buff = new byte[128];
			var prefixPadding = 64;

			// Find the path signature in memory. It looks something like:
			// "\0\0\0WMS_Getaway:/meta_steam_pfx3/skin/skin/n/in/team_pfx3/skin/
			ReadProcessMemory(gameProc.Handle, offset - prefixPadding - pathSig.Length, buff, buff.Length, IntPtr.Zero);

			var buffStr = Encoding.ASCII.GetString(buff);

			var pathIndex = buffStr.IndexOf(path);

			if (pathIndex == -1) {
				return IntPtr.Zero;
			}

			// Seek backwards to the start of the string
			var startIndex = buffStr.LastIndexOf(prefixNulls, pathIndex);
			if (startIndex == -1) {
				return IntPtr.Zero;
			}

			_nameAddress = offset - prefixPadding + startIndex + prefixNulls.Length - pathSig.Length;

			ReadProcessMemory(gameProc.Handle, _nameAddress, buff, buff.Length, IntPtr.Zero);

			// Return game's process handle.
			return processHandle;
		}
	}
}
