using System;
using System.Windows.Media.Imaging;

namespace LibDmd.Frame
{
	public static class FrameExtensions
	{
		public static int GetBitLength(this int numColors) => (int)(Math.Log(numColors) / Math.Log(2));
		public static int GetByteLength(this int bitLength) => bitLength <= 8 ? 1 : bitLength / 8;

		public static Dimensions Dimensions(this BitmapSource bmp) => new Dimensions(bmp.PixelWidth, bmp.PixelHeight);
	}
}
