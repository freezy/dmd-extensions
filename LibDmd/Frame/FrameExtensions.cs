using System;

namespace LibDmd.Frame
{
	public static class FrameExtensions
	{
		public static int GetBitLength(this int numColors) => (int)(Math.Log(numColors) / Math.Log(2));
	}
}
