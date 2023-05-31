using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Frame;
using NLog;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;

namespace LibDmd.Common
{
	public static class ImageUtil
	{
		private static readonly Dictionary<int, FrameData> FrameDataObjectPool = new Dictionary<int, FrameData>();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Returns a frame object for the given size.
		/// 
		/// The idea is not to instantiate a frame object for every frame but 
		/// only for every size.
		/// </summary>
		/// <param name="dim">Frame dimensions</param>
		/// <returns></returns>
		private static FrameData GetFrameDataFromPool(Dimensions dim)
		{
			var key = dim.Surface;
			if (!FrameDataObjectPool.ContainsKey(key)) {
				FrameDataObjectPool.Add(key, new FrameData());
			}
			return FrameDataObjectPool[key];
		}
		
		/// <summary>
		/// Converts a bitmap to a 2- or 4-bit grayscale or rgb24 array.
		/// </summary>
		/// <param name="bitLength">Bit length, 2, 4 or 24.</param>
		/// <param name="bmp">Source bitmap</param>
		/// <param name="lum">Multiply luminosity</param>
		/// <returns>Frame with value for every pixel between 0 and 15</returns>
		public static byte[] ConvertTo(int bitLength, BitmapSource bmp, double lum = 1)
		{
			switch (bitLength) {
				case 2: return ConvertToGray2(bmp, 0, lum, out _);
				case 4: return ConvertToGray4(bmp, lum);
				case 24: return ConvertToRgb24(bmp, lum: lum);
				default: throw new ArgumentException("Bit length must be either 2, 4 or 24.");
			}
		}
		
		/// <summary>
		/// Converts an 2- or 4-bit grayscale or rgb24 array to a bitmap.
		/// </summary>
		/// <param name="bitLength">Bit length, 2, 4 or 24.</param>
		/// <param name="dim">Image dimensions</param>
		/// <param name="frame">4-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created (for gray only)</param>
		/// <param name="saturation">Saturation in which the bitmap will be created (for gray only)</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created (for gray only)</param>
		/// <returns>Bitmap</returns>
		public static BitmapSource ConvertFrom(int bitLength, Dimensions dim, byte[] frame, double hue, double saturation, double luminosity)
		{
			lock (FrameDataObjectPool) {
				switch (bitLength) {
					case 2: return ConvertFromGray2(dim, GetFrameDataFromPool(dim).With(frame), hue, saturation, luminosity);
					case 4: return ConvertFromGray4(dim, GetFrameDataFromPool(dim).With(frame), hue, saturation, luminosity);
					case 24: return ConvertFromRgb24(dim, GetFrameDataFromPool(dim).With(frame));
					default: throw new ArgumentException("Bit length must be either 2, 4 or 24.");
				}
			}
		}

		public static DmdFrame ConvertToGray2(BitmapSource bmp) => new DmdFrame(bmp.Dimensions(), ConvertToGray2(bmp, 0, 1, out _), 2);

		/// <summary>
		/// Converts a bitmap to a 2-bit grayscale array by using the luminosity of the pixels and
		/// histogram stretching.
		/// </summary>
		/// <param name="bmp">Source bitmap</param>
		/// <param name="minLum">Min threshold for luminosity histogram stretching</param>
		/// <param name="maxLum">Max threshold for luminosity histogram stretching</param>
		/// <param name="hue">Detected hue</param>
		/// <returns>Array with value for every pixel between 0 and 3</returns>
		public static byte[] ConvertToGray2(BitmapSource bmp, double minLum, double maxLum, out double hue)
		{
			var frame = new byte[bmp.PixelWidth * bmp.PixelHeight];
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);
			double imageHue = 0;
			for (var y = 0; y < bmp.PixelHeight; y++) {
				rect.Y = y;
				for (var x = 0; x < bmp.PixelWidth; x++) {

					rect.X = x;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

					// convert to HSL
					ColorUtil.RgbToHsl(bytes[2], bytes[1], bytes[0], out var h, out _, out var luminosity);

					var pixelBrightness = (luminosity - minLum) / (maxLum - minLum);
					byte frameVal = (byte)Math.Min(Math.Max(Math.Round(pixelBrightness * 3d), 0), 3);
					frame[y * bmp.PixelWidth + x] = frameVal;

					// Don't use very low luminosity values to calculate hue because they are less accurate.
					if (frameVal > 0) {
						imageHue = h;
					}
				}
			}
			hue = imageHue;
			return frame;
		}

		/// <summary>
		/// Converts an RGB24 frame to a grayscale array.
		/// </summary>
		/// <param name="dim">Pixel dimensions</param>
		/// <param name="frameRgb24">RGB24 frame, top left to bottom right, three bytes per pixel with values between 0 and 255</param>
		/// <param name="numColors">Number of gray tones. 4 for 2 bit, 16 for 4 bit</param>
		/// <returns>Gray2 frame, top left to bottom right, one byte per pixel with values between 0 and 3</returns>
		public static byte[] ConvertToGray(Dimensions dim, byte[] frameRgb24, int numColors)
		{
			var frame = new byte[dim.Surface];
			var pos = 0;
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width * 3; x += 3) {
					var rgbPos = y * dim.Width * 3 + x;

					// convert to HSL
					ColorUtil.RgbToHsl(frameRgb24[rgbPos], frameRgb24[rgbPos + 1], frameRgb24[rgbPos + 2], out _, out _, out var luminosity);
					frame[pos++] = (byte)Math.Round(luminosity * (numColors - 1));
				}
			}
			return frame;
		}

		/// <summary>
		/// Converts a bitmap to a 4-bit grayscale array.
		/// </summary>
		/// <param name="bmp">Source bitmap</param>
		/// <param name="lum">Multiply luminosity</param>
		/// <returns>Array with value for every pixel between 0 and 15</returns>
		public static byte[] ConvertToGray4(BitmapSource bmp, double lum = 1)
		{
			var frame = new byte[bmp.PixelWidth * bmp.PixelHeight];
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);
			for (var y = 0; y < bmp.PixelHeight; y++) {
				rect.Y = y;
				for (var x = 0; x < bmp.PixelWidth; x++) {

					rect.X = x;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

					// convert to HSL
					ColorUtil.RgbToHsl(bytes[2], bytes[1], bytes[0], out _, out _, out var luminosity);

					frame[y * bmp.PixelWidth + x] = (byte)Math.Round(luminosity * 15d * lum);
				}
			}
			return frame;
		}

		/// <summary>
		/// Converts a bitmap to a 6-bit grayscale array.
		/// </summary>
		/// <param name="bmp">Source bitmap</param>
		/// <param name="lum">Multiply luminosity</param>
		/// <returns>Array with value for every pixel between 0 and 15</returns>
		public static byte[] ConvertToGray6(BitmapSource bmp, double lum = 1)
		{
			var frame = new byte[bmp.PixelWidth * bmp.PixelHeight];
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);
			for (var y = 0; y < bmp.PixelHeight; y++)
			{
				rect.Y = y;
				for (var x = 0; x < bmp.PixelWidth; x++)
				{

					rect.X = x;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

					// convert to HSL
					double hue;
					double saturation;
					double luminosity;
					ColorUtil.RgbToHsl(bytes[2], bytes[1], bytes[0], out hue, out saturation, out luminosity);

					frame[y * bmp.PixelWidth + x] = (byte)Math.Round(luminosity * 63d * lum);
				}
			}
			return frame;
		}

		/// <summary>
		/// Converts a bitmap to an RGB24 array.
		/// </summary>
		/// <param name="bmp">Source bitmap</param>
		/// <param name="offset">Offset in destination array</param>
		/// <param name="lum">Multiply luminosity</param>
		/// <returns>New frame buffer. Will be filled with RGB values for each pixel between 0 and 255.</returns>
		public static byte[] ConvertToRgb24(BitmapSource bmp, int offset = 0, double lum = 1)
		{
			var frame = new byte[bmp.PixelWidth * bmp.PixelHeight * 3];
			var stride = bmp.PixelWidth * (bmp.Format.BitsPerPixel / 8);
			var bytes = new byte[bmp.PixelHeight * stride];
			bmp.CopyPixels(bytes, stride, 0);

			if (Math.Abs(lum - 1) > 0.01) {
				for (var i = 0; i < bytes.Length; i += 3) {
					ColorUtil.RgbToHsl(bytes[i + 2], bytes[i + 1], bytes[i], out var hue, out var saturation, out var luminosity);
					ColorUtil.HslToRgb(hue, saturation, luminosity * lum, out var r, out var g, out var b);
					frame[i] = r;
					frame[i + 1] = g;
					frame[i + 2] = b;
				}
			} else {
				unsafe
				{
					fixed (byte* pBuffer = frame, pBytes = bytes)
					{
						byte* pB = pBuffer, pEnd = pBytes + bytes.Length;
						for (var pByte = pBytes; pByte < pEnd; pByte += 4, pB += 3) {
							*(pB) = *(pByte + 2);
							*(pB + 1) = *(pByte + 1);
							*(pB + 2) = *(pByte);
						}
					}
				}
			}

			return frame;
		}

		/// <summary>
		/// Converts an image to a BitmapSource
		/// </summary>
		/// <param name="img">Image to convert</param>
		/// <returns>Converted bitmap</returns>
		public static BitmapSource ConvertToBitmap(Image img)
		{
			var bitmap = new Bitmap(img);
			var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);

			var bitmapSource = BitmapSource.Create(
				bitmapData.Width, bitmapData.Height, 96, 96, ConvertPixelFormat(bitmap.PixelFormat), null,
				bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

			bitmap.UnlockBits(bitmapData);
			return bitmapSource;
		}

		public static Image ConvertToImage(BitmapSource bitmapsource)
		{
			// convert image format
			var src = new FormatConvertedBitmap();
			src.BeginInit();
			src.Source = bitmapsource;
			src.DestinationFormat = PixelFormats.Bgra32;
			src.EndInit();

			// copy to bitmap
			var bitmap = new Bitmap(src.PixelWidth, src.PixelHeight, PixelFormat.Format32bppArgb);
			var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			src.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
			bitmap.UnlockBits(data);

			return bitmap;
		}

		public static void ConvertRgb24ToBgr32(Dimensions dim, byte[] from, byte[] to)
		{
			var pos = 0;
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width * 3; x += 3) {
					var fromPos = dim.Width * 3 * y + x;
					to[pos] = from[fromPos + 2];
					to[pos + 1] = from[fromPos + 1];
					to[pos + 2] = from[fromPos];
					to[pos + 3] = 0;
					pos += 4;
				}
			}
		}

		/// <summary>
		/// Converts an 2-bit grayscale array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frameData">2-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created</param>
		/// <param name="saturation">Saturation in which the bitmap will be created</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created</param>
		/// <returns>Bitmap</returns>
		private static BitmapSource ConvertFromGray2(Dimensions dim, FrameData frameData, double hue, double saturation, double luminosity)
		{
			if (frameData.Size > 0 && frameData.Size != dim.Surface) {
				throw new ArgumentException($"Must convert to {dim.Width}x{dim.Height} but frame buffer is {frameData.Size} bytes");
			}

			var bmp = new WriteableBitmap(dim.Width, dim.Height, 96, 96, PixelFormats.Bgr32, null);
			var bufferSize = (Math.Abs(bmp.BackBufferStride) * dim.Height + 2);
			var frameBuffer = new byte[bufferSize];

			var index = 0;
			bmp.Lock();
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width; x++) {

					try {
						var pixelLum = frameData.Get(y * dim.Width + x); // 0 - 3
						var lum = luminosity * pixelLum / 3;
						ColorUtil.HslToRgb(hue, saturation, lum, out var red, out var green, out var blue);

						frameBuffer[index] = blue;
						frameBuffer[index + 1] = green;
						frameBuffer[index + 2] = red;
						index += 4;

					} catch (IndexOutOfRangeException e) {
						Logger.Error(e, $"Converting {dim.Width}x{dim.Height} with {frameData.Size} bytes: Trying to get pixel at position {y * dim.Width + x}");
						throw;
					}
				}
			}
			bmp.WritePixels(new Int32Rect(0, 0, dim.Width, dim.Height), frameBuffer, bmp.BackBufferStride, 0);
			bmp.Unlock();
			bmp.Freeze();
			return bmp;
		}

		/// <summary>
		/// Converts an 4-bit grayscale array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frameData">4-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created</param>
		/// <param name="saturation">Saturation in which the bitmap will be created</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created</param>
		/// <returns>Bitmap</returns>
		private static BitmapSource ConvertFromGray4(Dimensions dim, FrameData frameData, double hue, double saturation, double luminosity)
		{
			var bmp = new WriteableBitmap(dim.Width, dim.Height, 96, 96, PixelFormats.Bgr32, null);
			var bufferSize = (Math.Abs(bmp.BackBufferStride) * dim.Height + 2);
			var frameBuffer = new byte[bufferSize];

			var index = 0;
			bmp.Lock();
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width; x++) {

					var pixelLum = frameData.Get(y * dim.Width + x);
					var lum = luminosity * pixelLum / 15;
					byte red, green, blue;
					ColorUtil.HslToRgb(hue, saturation, lum, out red, out green, out blue);

					frameBuffer[index] = blue;
					frameBuffer[index + 1] = green;
					frameBuffer[index + 2] = red;
					index += 4;
				}
			}
			bmp.WritePixels(new Int32Rect(0, 0, dim.Width, dim.Height), frameBuffer, bmp.BackBufferStride, 0);
			bmp.Unlock();
			bmp.Freeze();
			return bmp;
		}

		/// <summary>
		/// Converts an 6-bit grayscale array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frameData">6-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created</param>
		/// <param name="saturation">Saturation in which the bitmap will be created</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created</param>
		/// <returns>Bitmap</returns>
		private static BitmapSource ConvertFromGray6(Dimensions dim, FrameData frameData, double hue, double saturation, double luminosity)
		{
			var bmp = new WriteableBitmap(dim.Width, dim.Height, 96, 96, PixelFormats.Bgr32, null);
			var bufferSize = (Math.Abs(bmp.BackBufferStride) * dim.Height + 2);
			var frameBuffer = new byte[bufferSize];

			var index = 0;
			bmp.Lock();
			for (var y = 0; y < dim.Height; y++)
			{
				for (var x = 0; x < dim.Width; x++)
				{

					var pixelLum = frameData.Get(y * dim.Width + x);
					var lum = luminosity * pixelLum / 63;
					ColorUtil.HslToRgb(hue, saturation, lum, out var red, out var green, out var blue);

					frameBuffer[index] = blue;
					frameBuffer[index + 1] = green;
					frameBuffer[index + 2] = red;
					index += 4;
				}
			}
			bmp.WritePixels(new Int32Rect(0, 0, dim.Width, dim.Height), frameBuffer, bmp.BackBufferStride, 0);
			bmp.Unlock();
			bmp.Freeze();
			return bmp;
		}

		/// <summary>
		/// Converts an RGB24 array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frameData">RGB values for each pixel between 0 and 255</param>
		/// <returns>Bitmap</returns>
		private static BitmapSource ConvertFromRgb24(Dimensions dim, FrameData frameData)
		{
			var bmp = new WriteableBitmap(dim.Width, dim.Height, 96, 96, PixelFormats.Bgr32, null);
			var bufferSize = (Math.Abs(bmp.BackBufferStride) * dim.Height + 2);
			var frameBuffer = new byte[bufferSize];

			unsafe
			{
				fixed (byte* pFrameArray = frameData.ArraySrc, pDestArray = frameBuffer)
				{
					byte* srcPtr = (frameData.IsPointer) ? frameData.PointerSrc : pFrameArray;
					byte* srcEnd = srcPtr + dim.Surface * 3;
					byte* dstPtr = pDestArray;

					for (; srcPtr < srcEnd; srcPtr += 3, dstPtr += 4) {
						*dstPtr = *(srcPtr + 2);
						*(dstPtr + 1) = *(srcPtr + 1);
						*(dstPtr + 2) = *(srcPtr);
					}
				}
			}
			bmp.Lock();
			bmp.WritePixels(new Int32Rect(0, 0, dim.Width, dim.Height), frameBuffer, bmp.BackBufferStride, 0);
			bmp.Unlock();
			bmp.Freeze();
			return bmp;
		}

		/// <summary>
		/// Converts an 2-bit grayscale array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frame">2-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created</param>
		/// <param name="saturation">Saturation in which the bitmap will be created</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created</param>
		/// <returns>Bitmap</returns>
		public static BitmapSource ConvertFromGray2(Dimensions dim, byte[] frame, double hue, double saturation, double luminosity)
		{
			lock (FrameDataObjectPool) {
				return ConvertFromGray2(dim, GetFrameDataFromPool(dim).With(frame), hue, saturation, luminosity);
			}
		}

		/// <summary>
		/// Converts an 4-bit grayscale array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frame">4-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created</param>
		/// <param name="saturation">Saturation in which the bitmap will be created</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created</param>
		/// <returns>Bitmap</returns>
		public static BitmapSource ConvertFromGray4(Dimensions dim, byte[] frame, double hue, double saturation, double luminosity)
		{
			lock (FrameDataObjectPool) {
				return ConvertFromGray4(dim, GetFrameDataFromPool(dim).With(frame), hue, saturation, luminosity);
			}
		}

		/// <summary>
		/// Converts an 6-bit grayscale array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frame">6-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created</param>
		/// <param name="saturation">Saturation in which the bitmap will be created</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created</param>
		/// <returns>Bitmap</returns>
		public static unsafe BitmapSource ConvertFromGray6(Dimensions dim, byte* frame, double hue, double saturation, double luminosity)
		{
			lock (FrameDataObjectPool)
			{
				return ConvertFromGray6(dim, GetFrameDataFromPool(dim).With(frame), hue, saturation, luminosity);
			}
		}

		/// <summary>
		/// Converts an 6-bit grayscale array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frame">6-bit grayscale array</param>
		/// <param name="hue">Hue in which the bitmap will be created</param>
		/// <param name="saturation">Saturation in which the bitmap will be created</param>
		/// <param name="luminosity">Maximal luminosity in which the bitmap will be created</param>
		/// <returns>Bitmap</returns>
		public static BitmapSource ConvertFromGray6(Dimensions dim, byte[] frame, double hue, double saturation, double luminosity)
		{
			lock (FrameDataObjectPool)
			{
				return ConvertFromGray6(dim, GetFrameDataFromPool(dim).With(frame), hue, saturation, luminosity);
			}
		}

		/// <summary>
		/// Converts an RGB24 array to a bitmap.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frame">RGB values for each pixel between 0 and 255</param>
		/// <returns>Bitmap</returns>
		public static BitmapSource ConvertFromRgb24(Dimensions dim, byte[] frame)
		{
			lock (FrameDataObjectPool) {
				return ConvertFromRgb24(dim, GetFrameDataFromPool(dim).With(frame));
			}
		}

		/// <summary>
		/// Convert an RGB24 array to a RGB565 array.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="frameRgb24">RGB24 array, from top left to bottom right</param>
		/// <returns></returns>
		public static char[] ConvertToRgb565(Dimensions dim, byte[] frameRgb24)
		{
			var frame = new char[dim.Surface];
			var pos = 0;
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width * 3; x += 3) {
					var rgbPos = y * dim.Width * 3 + x;
					var r = frameRgb24[rgbPos];
					var g = frameRgb24[rgbPos + 1];
					var b = frameRgb24[rgbPos + 2];

					var x1 = (r & 0xF8) | (g >> 5);          // Take 5 bits of Red component and 3 bits of G component
					var x2 = ((g & 0x1C) << 3) | (b >> 3);   // Take remaining 3 Bits of G component and 5 bits of Blue component

					frame[pos++] = (char)((x1 << 8) + x2);
				}
			}
			return frame;
		}

		/// <summary>
		/// Converts between pixel formats.
		/// </summary>
		/// <param name="sourceFormat">Source format</param>
		/// <returns>Destination format</returns>
		private static System.Windows.Media.PixelFormat ConvertPixelFormat(PixelFormat sourceFormat)
		{
			switch (sourceFormat) {
				case PixelFormat.Format24bppRgb: return PixelFormats.Bgr24;
				case PixelFormat.Format32bppArgb: return PixelFormats.Bgra32;
				case PixelFormat.Format32bppRgb: return PixelFormats.Bgr32;
				case PixelFormat.Indexed: return PixelFormats.Indexed1;
				case PixelFormat.Format1bppIndexed: return PixelFormats.Indexed1;
				case PixelFormat.Format4bppIndexed: return PixelFormats.Indexed4;
				case PixelFormat.Format8bppIndexed: return PixelFormats.Indexed8;
				case PixelFormat.Format16bppGrayScale: return PixelFormats.Gray16;
				case PixelFormat.Format16bppRgb555: return PixelFormats.Bgr555;
				case PixelFormat.Format16bppRgb565: return PixelFormats.Bgr565;
				case PixelFormat.Format16bppArgb1555: return PixelFormats.Bgr101010;
				case PixelFormat.Format32bppPArgb: return PixelFormats.Pbgra32;
				case PixelFormat.Format48bppRgb: return PixelFormats.Rgb48;
				case PixelFormat.Format64bppArgb: return PixelFormats.Rgba64;
				case PixelFormat.Format64bppPArgb: return PixelFormats.Prgba64;
				case PixelFormat.Gdi:
				case PixelFormat.Alpha:
				case PixelFormat.PAlpha:
				case PixelFormat.Extended:
				case PixelFormat.Canonical:
				case PixelFormat.Undefined:
				case PixelFormat.Max:
				default:
					return new System.Windows.Media.PixelFormat();
			}
		}

		/// <summary>
		/// Sometimes we have a pointer, sometimes an array, but we don't want to implement
		/// everything twice, so this is a wrapper that supports both.
		/// </summary>
		private unsafe class FrameData
		{
			public int Size => IsPointer ? -1 : ArraySrc.Length;

			public byte* PointerSrc;
			public byte[] ArraySrc;
			public bool IsPointer;

			public FrameData With(byte* src)
			{
				PointerSrc = src;
				IsPointer = true;
				return this;
			}

			public FrameData With(byte[] src)
			{
				ArraySrc = src;
				IsPointer = false;
				return this;
			}

			public byte Get(int pos)
			{
				return IsPointer ? PointerSrc[pos] : ArraySrc[pos];
			}
		}

		/// <summary>
		/// Get pixel color of the frame data
		/// </summary>
		/// <param name="x">x coord</param>
		/// <param name="y">y coord</param>
		/// <param name="width">stride of data</param>
		/// <param name="frame">data</param>
		/// <returns>color of coord</returns>
		public static byte GetPixel(int x, int y, int width, int height, byte[] frame)
		{
			// Clamp edges so it doesn't wrap.
			x = Clamp(x, 0, width - 1);
			y = Clamp(y, 0, height - 1);

			return frame[x + (width * y)];
		}

		/// <summary>
		/// Set pixel color of a texture block
		/// </summary>
		/// <param name="x">x coord</param>
		/// <param name="y">y coord</param>
		/// <param name="color">color to set</param>
		/// <param name="width">stride of data</param>
		/// <param name="frame">data</param>
		public static void SetPixel(int x, int y, byte color, int width, byte[] frame)
		{
			frame[x + (width * y)] = color;
		}

		/// <summary>
		/// Get pixel color of the RGB frame data
		/// </summary>
		/// <param name="x">x coord</param>
		/// <param name="y">y coord</param>
		/// <param name="width">stride of data</param>
		/// <param name="height"></param>
		/// <param name="frame">data</param>
		/// <returns>color of coord</returns>
		public static byte[] GetRgbPixel(int x, int y, int width, int height, byte[] frame)
		{
			var rgb = new byte[3];
			// Clamp edges so it doesn't wrap.
			x = Clamp(x, 0, width - 1);
			y = Clamp(y, 0, height - 1);

			rgb[0] = frame[x * 3 + (width * 3 * y)];
			rgb[1] = frame[x * 3 + 1 + (width * 3 * y)];
			rgb[2] = frame[x * 3 + 2 + (width * 3 * y)];

			return rgb;
		}

		/// <summary>
		/// Set pixel RGB color of a texture block
		/// </summary>
		/// <param name="x">x coord</param>
		/// <param name="y">y coord</param>
		/// <param name="color">color to set</param>
		/// <param name="width">stride of data</param>
		/// <param name="frame">data</param>
		public static void SetRgbPixel(int x, int y, byte[] color, int width, byte[] frame)
		{
			frame[x * 3 + (width * 3 * y)] = color[0];
			frame[x * 3 + 1 + (width * 3 * y)] = color[1];
			frame[x * 3 + 2 + (width * 3 * y)] = color[2];
		}

		/// <summary>
		/// Clamp values
		/// </summary>
		/// <param name="value"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <returns></returns>
		public static int Clamp(int value, int min, int max)
		{
			return (value < min) ? min : (value > max) ? max : value;
		}
	}


	/// <summary>
	/// Scaler mode determines whether "HD up-scaling" is enabled.
	/// </summary>
	public enum ScalerMode
	{
		/// <summary>
		/// Don't upscale
		/// </summary>
		None,
		
		/// <summary>
		/// Double the pixels
		/// </summary>
		Doubler,

		/// <summary>
		/// Use scale2x algorithm
		/// </summary>
		Scale2x
	}
}
