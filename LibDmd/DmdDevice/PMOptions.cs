using System.Runtime.InteropServices;

namespace LibDmd.DmdDevice
{
	[StructLayout(LayoutKind.Sequential)]
	public struct PMoptions
	{
		public int Red, Green, Blue;
		public int Perc66, Perc33, Perc0;
		public int DmdOnly, Compact, Antialias;
		public int Colorize;
		public int Red66, Green66, Blue66;
		public int Red33, Green33, Blue33;
		public int Red0, Green0, Blue0;
	}
}
