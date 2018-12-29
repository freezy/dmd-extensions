using System.Runtime.InteropServices;

namespace LibDmd.Output.Virtual.SkiaDmd.GLContext
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct RECT
	{
		public int left;
		public int top;
		public int right;
		public int bottom;
	}
}
