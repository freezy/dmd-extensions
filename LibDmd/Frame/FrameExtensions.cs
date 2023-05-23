using System;
using System.Windows.Media.Imaging;

namespace LibDmd.Frame
{
	public static class FrameExtensions
	{
		public static int GetBitLength(this int numColors) => (int)(Math.Log(numColors) / Math.Log(2));

		public static Dimensions Dimensions(this BitmapSource bmp) => new Dimensions(bmp.PixelWidth, bmp.PixelHeight);
	}
}
