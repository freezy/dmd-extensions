using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Common
{
	/// <summary>
	/// Tools for dealing with frame data.
	/// </summary>
	public static class FrameUtil
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Splits a pixel array into separate bit planes.
		/// </summary>
		/// 
		/// <remarks>
		/// A bit plane is a byte array with the same dimensions as the original frame,
		/// but since it's bits, a pixel can be either one or zero, so they are packed
		/// into bytes.
		///
		/// This makes it more efficient to transfer than one byte per pixel, where only
		/// 2 or 4 bits are used.
		/// </remarks>
		/// 
		/// <param name="dim">Frame dimensions</param>
		/// <param name="bitlen">How many bits per pixel, i.e. how many bit planes</param>
		/// <param name="frame">Frame data, from top left to bottom right</param>
		/// <param name="destPlanes">If set, write the bit planes into this.</param>
		/// <returns>Array of bit plans</returns>
		public static byte[][] Split(Dimensions dim, int bitlen, byte[] frame, byte[][] destPlanes = null)
		{
			using (Profiler.Start("FrameUtil.Split")) {

				var planeSize = dim.Surface / 8;
				var planes = destPlanes ?? new byte[bitlen][];

				try
				{
					for (var i = 0; i < bitlen; i++) {
						if (planes[i] == null) { // recycle, if possible
							planes[i] = new byte[planeSize];
						}
					}

					var byteIdx = 0;
					var bd = new byte[bitlen];
					for (var y = 0; y < dim.Height; y++)
					{
						for (var x = 0; x < dim.Width; x += 8)
						{
							for (var i = 0; i < bitlen; i++)
							{
								bd[i] = 0;
							}

							for (var v = 7; v >= 0; v--)
							{
								var pixel = frame[(y * dim.Width) + (x + v)];
								for (var i = 0; i < bitlen; i++)
								{
									bd[i] <<= 1;
									if ((pixel & (1 << i)) != 0)
									{
										bd[i] |= 1;
									}
								}
							}

							for (var i = 0; i < bitlen; i++)
							{
								planes[i][byteIdx] = bd[i];
							}

							byteIdx++;
						}
					}
				}
				catch (IndexOutOfRangeException e)
				{
					Logger.Error("Split failed: {0}x{1} frame:{2} bitlen:{3}", dim.Width, dim.Height, frame.Length, bitlen);
					throw new IndexOutOfRangeException(e.Message, e);
				}

				return planes;
			}
		}

		/// <summary>
		/// Joins an array of bit planes back into one single byte array where one byte represents one pixel.
		/// </summary>
		/// <param name="dim">Frame dimensions</param>
		/// <param name="bitPlanes">Array of bit planes</param>
		/// <returns>Byte array from top left to bottom right</returns>
		public static byte[] Join(Dimensions dim, byte[][] bitPlanes)
		{
			using (Profiler.Start("FrameUtil.Join")) {

				var frame = new byte[dim.Surface];
				if (bitPlanes.Length == 2) {
					unsafe
					{
						fixed (byte* pFrame = &frame[0])
						{
							var pfEnd = pFrame + frame.Length;

							fixed (byte* plane0 = &bitPlanes[0][0], plane1 = &bitPlanes[1][0])
							{
								byte* pp0 = plane0;
								byte* pp1 = plane1;
								byte andValue = 1;

								for (var pf = pFrame; pf < pfEnd; pf++) {
									if ((*pp0 & andValue) > 0)
										*pf |= 1;
									if ((*pp1 & andValue) > 0)
										*pf |= 2;

									if (andValue == 0x80) {
										pp0++;
										pp1++;
										andValue = 0x01;
									} else
										andValue <<= 1;
								}
							}
						}
					}
				} else if (bitPlanes.Length == 4) {
					unsafe
					{
						fixed (byte* pFrame = &frame[0])
						{
							var pfEnd = pFrame + frame.Length;

							Debug.Assert(bitPlanes.Length == 4);
							fixed (byte* plane0 = &bitPlanes[0][0], plane1 = &bitPlanes[1][0],
								   plane2 = &bitPlanes[2][0], plane3 = &bitPlanes[3][0])
							{
								byte* pp0 = plane0;
								byte* pp1 = plane1;
								byte* pp2 = plane2;
								byte* pp3 = plane3;

								byte andValue = 1;

								for (var pf = pFrame; pf < pfEnd; pf++) {
									if ((*pp0 & andValue) > 0)
										*pf |= 1;
									if ((*pp1 & andValue) > 0)
										*pf |= 2;
									if ((*pp2 & andValue) > 0)
										*pf |= 4;
									if ((*pp3 & andValue) > 0)
										*pf |= 8;

									if (andValue == 0x80) {
										pp0++;
										pp1++;
										pp2++;
										pp3++;
										andValue = 0x01;
									} else
										andValue <<= 1;
								}
							}
						}
					}
				} else {
					var planes = new BitArray[bitPlanes.Length];
					for (var i = 0; i < bitPlanes.Length; i++) {
						planes[i] = new BitArray(bitPlanes[i]);
					}
					for (var f = 0; f < frame.Length; f++) {
						for (var p = 0; p < bitPlanes.Length; p++) {
							try {
								var bit = planes[p].Get(f) ? (byte)1 : (byte)0;
								frame[f] |= (byte)(bit << p);
							} catch (ArgumentOutOfRangeException) {
								Logger.Error("Error retrieving pixel {0} on plane {1}. Frame size = {2}x{3}, plane size = {4}.", f, p, dim.Width, dim.Height, planes.Length);
								throw;
							}
						}
					}
				}
				return frame;
			}
		}

		public static void SplitIntoRgbPlanes(char[] rgb565, int width, int numLogicalRows, byte[] dest, ColorMatrix colorMatrix = ColorMatrix.Rgb) // originally "convertAdafruit()"
		{
			var pairOffset = 16;
			var height = rgb565.Length / width;
			var subframeSize = rgb565.Length / 2;

			for (var x = 0; x < width; ++x) {
				for (var y = 0; y < height; ++y) {
					if (y % (pairOffset * 2) >= pairOffset) {
						continue;
					}

					// This are the two indices of the pixel comprising a dot-pair in the input.
					var inputIndex0 = y * width + x;
					var inputIndex1 = (y + pairOffset) * width + x;

					var color0 = rgb565[inputIndex0];
					var color1 = rgb565[inputIndex1];

					int r0 = 0, r1 = 0, g0 = 0, g1 = 0, b0 = 0, b1 = 0;
					switch (colorMatrix)
					{
						case ColorMatrix.Rgb:
							r0 = (color0 >> 13) & 0x7;
							g0 = (color0 >> 8) & 0x7;
							b0 = (color0 >> 2) & 0x7;
							r1 = (color1 >> 13) & 0x7;
							g1 = (color1 >> 8) & 0x7;
							b1 = (color1 >> 2) & 0x7;
							break;

						case ColorMatrix.Rbg:
							r0 = (color0 >> 13) & 0x7;
							b0 = (color0 >> 8) & 0x7;
							g0 = (color0 >> 2) & 0x7;
							r1 = (color1 >> 13) & 0x7;
							b1 = (color1 >> 8) & 0x7;
							g1 = (color1 >> 2) & 0x7;
							break;
					}

					for (var subframe = 0; subframe < 3; ++subframe) {
						var dotPair =
							(r0 & 1) << 5
							| (g0 & 1) << 4
							| (b0 & 1) << 3
							| (r1 & 1) << 2
							| (g1 & 1) << 1
							| (b1 & 1) << 0;
						var indexWithinSubframe = MapAdafruitIndex(x, y, width, height, numLogicalRows);
						var indexWithinOutput = subframe * subframeSize + indexWithinSubframe;
						dest[indexWithinOutput] = (byte)dotPair;
						r0 >>= 1;
						g0 >>= 1;
						b0 >>= 1;
						r1 >>= 1;
						g1 >>= 1;
						b1 >>= 1;
					}
				}
			}
		}

		private static int MapAdafruitIndex(int x, int y, int width, int height, int numLogicalRows)
		{
			var logicalRowLengthPerMatrix = 32 * 32 / 2 / numLogicalRows;
			var logicalRow = y % numLogicalRows;
			var dotPairsPerLogicalRow = width * height / numLogicalRows / 2;
			var widthInMatrices = width / 32;
			var matrixX = x / 32;
			var matrixY = y / 32;
			var totalMatrices = width * height / 1024;
			var matrixNumber = totalMatrices - ((matrixY + 1) * widthInMatrices) + matrixX;
			var indexWithinMatrixRow = x % logicalRowLengthPerMatrix;
			var index = logicalRow * dotPairsPerLogicalRow
						+ matrixNumber * logicalRowLengthPerMatrix + indexWithinMatrixRow;
			return index;
		}

		/// <summary>
		/// Copies a byte array to another byte array.
		/// </summary>
		/// <param name="frame">Source array</param>
		/// <param name="dest">Destination array</param>
		/// <param name="offset">Offset at destination</param>
		/// <returns>True if destination array changed, false otherwise.</returns>
		public static bool Copy(byte[] frame, byte[] dest, int offset)
		{
			var identical = CompareBuffers(frame, 0, dest, offset, frame.Length);
			Buffer.BlockCopy(frame, 0, dest, offset, frame.Length);
			return !identical;
		}

		//Scale planes by doubling the pixels in each byte
		public static readonly ushort[] doublePixel = {
		  0x0000,0x0003,0x000C,0x000F,0x0030,0x0033,0x003C,0x003F,0x00C0,0x00C3,0x00CC,0x00CF,0x00F0,0x00F3,0x00FC,0x00FF,
		  0x0300,0x0303,0x030C,0x030F,0x0330,0x0333,0x033C,0x033F,0x03C0,0x03C3,0x03CC,0x03CF,0x03F0,0x03F3,0x03FC,0x03FF,
		  0x0C00,0x0C03,0x0C0C,0x0C0F,0x0C30,0x0C33,0x0C3C,0x0C3F,0x0CC0,0x0CC3,0x0CCC,0x0CCF,0x0CF0,0x0CF3,0x0CFC,0x0CFF,
		  0x0F00,0x0F03,0x0F0C,0x0F0F,0x0F30,0x0F33,0x0F3C,0x0F3F,0x0FC0,0x0FC3,0x0FCC,0x0FCF,0x0FF0,0x0FF3,0x0FFC,0x0FFF,
		  0x3000,0x3003,0x300C,0x300F,0x3030,0x3033,0x303C,0x303F,0x30C0,0x30C3,0x30CC,0x30CF,0x30F0,0x30F3,0x30FC,0x30FF,
		  0x3300,0x3303,0x330C,0x330F,0x3330,0x3333,0x333C,0x333F,0x33C0,0x33C3,0x33CC,0x33CF,0x33F0,0x33F3,0x33FC,0x33FF,
		  0x3C00,0x3C03,0x3C0C,0x3C0F,0x3C30,0x3C33,0x3C3C,0x3C3F,0x3CC0,0x3CC3,0x3CCC,0x3CCF,0x3CF0,0x3CF3,0x3CFC,0x3CFF,
		  0x3F00,0x3F03,0x3F0C,0x3F0F,0x3F30,0x3F33,0x3F3C,0x3F3F,0x3FC0,0x3FC3,0x3FCC,0x3FCF,0x3FF0,0x3FF3,0x3FFC,0x3FFF,
		  0xC000,0xC003,0xC00C,0xC00F,0xC030,0xC033,0xC03C,0xC03F,0xC0C0,0xC0C3,0xC0CC,0xC0CF,0xC0F0,0xC0F3,0xC0FC,0xC0FF,
		  0xC300,0xC303,0xC30C,0xC30F,0xC330,0xC333,0xC33C,0xC33F,0xC3C0,0xC3C3,0xC3CC,0xC3CF,0xC3F0,0xC3F3,0xC3FC,0xC3FF,
		  0xCC00,0xCC03,0xCC0C,0xCC0F,0xCC30,0xCC33,0xCC3C,0xCC3F,0xCCC0,0xCCC3,0xCCCC,0xCCCF,0xCCF0,0xCCF3,0xCCFC,0xCCFF,
		  0xCF00,0xCF03,0xCF0C,0xCF0F,0xCF30,0xCF33,0xCF3C,0xCF3F,0xCFC0,0xCFC3,0xCFCC,0xCFCF,0xCFF0,0xCFF3,0xCFFC,0xCFFF,
		  0xF000,0xF003,0xF00C,0xF00F,0xF030,0xF033,0xF03C,0xF03F,0xF0C0,0xF0C3,0xF0CC,0xF0CF,0xF0F0,0xF0F3,0xF0FC,0xF0FF,
		  0xF300,0xF303,0xF30C,0xF30F,0xF330,0xF333,0xF33C,0xF33F,0xF3C0,0xF3C3,0xF3CC,0xF3CF,0xF3F0,0xF3F3,0xF3FC,0xF3FF,
		  0xFC00,0xFC03,0xFC0C,0xFC0F,0xFC30,0xFC33,0xFC3C,0xFC3F,0xFCC0,0xFCC3,0xFCCC,0xFCCF,0xFCF0,0xFCF3,0xFCFC,0xFCFF,
		  0xFF00,0xFF03,0xFF0C,0xFF0F,0xFF30,0xFF33,0xFF3C,0xFF3F,0xFFC0,0xFFC3,0xFFCC,0xFFCF,0xFFF0,0xFFF3,0xFFFC,0xFFFF
		};


		/// <summary>
		/// Doubles the scale of a number of bit planes in both dimensions, by doubling each pixel.
		/// </summary>
		/// <param name="dim">Dimensions of source planes</param>
		/// <param name="srcPlanes">Planes to scale</param>
		/// <returns>Scaled bit planes</returns>
		public static byte[][] ScaleDouble(Dimensions dim, byte[][] srcPlanes)
		{
			var planes = new byte[srcPlanes.Length][];
			for (var l = 0; l < srcPlanes.Length; l++)
			{
				planes[l] = ScaleDoublePlane(dim, srcPlanes[l]);
			}
			return planes;
		}

		/// <summary>
		/// Doubles the scale of a bit plane in both dimensions, by doubling each pixel.
		/// </summary>
		/// <param name="dim">Dimensions of source plane</param>
		/// <param name="srcPlane">Source plane (eight 1-bit pixels packed into one byte</param>
		/// <returns>Scaled bit plane</returns>
		private static byte[] ScaleDoublePlane(Dimensions dim, byte[] srcPlane)
		{
			var newDim = dim * 2;
			var newPlaneSize = newDim.Surface / 8;
			ushort[] scaledPlane = new ushort[newPlaneSize / 2]; // div 2 because, ushorts
			var width = newDim.Width / 2 / 8;
			for (var i = 0; i < newDim.Height; i++) {
				for (var k = 0; k < width; k++) {
					scaledPlane[i * width + k] = scaledPlane[(i + 1) * width + k] = doublePixel[srcPlane[i / 2 * width + k]];
				}
				i++;
			}
			// copy ushorts back to bytes
			var plane = new byte[newPlaneSize];
			Buffer.BlockCopy(scaledPlane, 0, plane, 0, newPlaneSize);
			return plane;
		}

		/// <summary>
		/// Scales a number of bit planes in both dimensions by two, by using the "2X" algorithm.
		/// </summary>
		/// <see cref="http://www.scale2x.it/algorithm"/>
		/// <param name="dim">Dimensions of source plane</param>
		/// <param name="srcPlane">Source plane (eight 1-bit pixels packed into one byte</param>
		/// <returns>Scaled bit planes</returns>
		public static byte[][] Scale2X(Dimensions dim, byte[][] srcPlane)
		{
			var joinData = Join(dim, srcPlane);
			var frameData = Scale2X(dim, joinData);
			return Split(dim * 2, srcPlane.Length, frameData);
		}

		/// <summary>
		/// Double the pixels coming from the frame data.
		/// </summary>
		/// <param name="dim">Size of the original data</param>
		/// <param name="frame">Frame data to be resized</param>
		/// <returns>Scaled frame data</returns>
		public static byte[] ScaleDouble(Dimensions dim, byte[] frame)
		{
			using (Profiler.Start("FrameUtil.ScaleDouble")) {
				byte[] scaledData = new byte[dim.Surface * 4];
				var outputWidth = dim.Width * 2;
				var outputHeight = dim.Height * 2;
				const int scale = 2;

				int targetIdx = 0;
				for (var i = 0; i < outputHeight; ++i) {
					var iUnscaled = i / scale;
					for (var j = 0; j < outputWidth; ++j) {
						var jUnscaled = j / scale;
						scaledData[targetIdx++] = frame[iUnscaled * dim.Width + jUnscaled];
					}
				}

				return scaledData;
			}
		}

		/// <summary>
		/// Implementation of Scale2 for frame data.
		/// </summary>
		/// <param name="dim">Original dimensions</param>
		/// <param name="data">Original frame data</param>
		/// <returns>Scaled frame data</returns>
		public static byte[] Scale2X(Dimensions dim, byte[] data)
		{
			using (Profiler.Start("FrameUtil.Scale2X")) {

				byte[] scaledData = new byte[dim.Surface * 4];
				var outputWidth = dim.Width * 2;

				for (var y = 0; y < dim.Height; y++)
				{
					for (var x = 0; x < dim.Width; x++)
					{
						var colorB = ImageUtil.GetPixel(x, y - 1, dim.Width, dim.Height, data);
						var colorH = ImageUtil.GetPixel(x, y + 1, dim.Width, dim.Height, data);
						var colorD = ImageUtil.GetPixel(x - 1, y, dim.Width, dim.Height, data);
						var colorF = ImageUtil.GetPixel(x + 1, y, dim.Width, dim.Height, data);

						var colorE = ImageUtil.GetPixel(x, y, dim.Width, dim.Height, data);

						if ((colorB != colorH) && (colorD != colorF))
						{
							ImageUtil.SetPixel(2 * x, 2 * y, colorD == colorB ? colorD : colorE, outputWidth, scaledData);
							ImageUtil.SetPixel(2 * x + 1, 2 * y, colorB == colorF ? colorF : colorE, outputWidth, scaledData);
							ImageUtil.SetPixel(2 * x, 2 * y + 1, colorD == colorH ? colorD : colorE, outputWidth, scaledData);
							ImageUtil.SetPixel(2 * x + 1, 2 * y + 1, colorH == colorF ? colorF : colorE, outputWidth, scaledData);
						}
						else
						{
							ImageUtil.SetPixel(2 * x, 2 * y, colorE, outputWidth, scaledData);
							ImageUtil.SetPixel(2 * x + 1, 2 * y, colorE, outputWidth, scaledData);
							ImageUtil.SetPixel(2 * x, 2 * y + 1, colorE, outputWidth, scaledData);
							ImageUtil.SetPixel(2 * x + 1, 2 * y + 1, colorE, outputWidth, scaledData);
						}
					}
				}
				return scaledData;
			}
		}

		/// <summary>
		/// Doubles the pixels coming from the frame data.
		/// </summary>
		/// <param name="dim">Size of the original data</param>
		/// <param name="frame">RGB24 frame data to be resized</param>
		/// <returns></returns>
		public static byte[] ScaleDoubleRgb(Dimensions dim, byte[] frame)
		{
			using (Profiler.Start("FrameUtil.ScaleDoubleRgb")) {
				var outputDim = dim * 2;
				byte[] scaledData = new byte[outputDim.Surface * 3];
				const int scale = 2;

				int targetIdx = 0;
				for (var i = 0; i < outputDim.Height; ++i) {
					var iUnscaled = i / scale;
					for (var j = 0; j < outputDim.Width; ++j) {
						var jUnscaled = j / scale;
						scaledData[targetIdx++] = frame[iUnscaled * dim.Width * 3 + jUnscaled * 3];
						scaledData[targetIdx++] = frame[iUnscaled * dim.Width * 3 + jUnscaled * 3 + 1];
						scaledData[targetIdx++] = frame[iUnscaled * dim.Width * 3 + jUnscaled * 3 + 2];
					}
				}
				return scaledData;
			}
		}
		/// <summary>
		/// Convert a RGB16/RGB565 ushort (UINT16) to 3 bytes RGB24
		/// </summary>
		/// <param name="rgb565">in: RGB565 color</param>
		/// <param name="r">out: red RGB24 component</param>
		/// <param name="g">out: green RGB24 component</param>
		/// <param name="b">out: blue RGB24 component</param>
		public static void rgb565_to_rgb888(ushort rgb565, ref byte r, ref byte g, ref byte b)
		{
			r = (byte)(((rgb565 >> 8) & 0xF8) | ((rgb565 >> 13) & 0x07)); // shifting then copying the 3 most significant bits to the right
			g = (byte)(((rgb565 >> 3) & 0xFC) | ((rgb565 >> 9) & 0x03)); // shifting then copying the 2 most significant bits to the right
			b = (byte)(((rgb565 << 3) & 0xF8) | ((rgb565 >> 2) & 0x07)); // shifting then copying the 3 most significant bits to the right
		}
		/// <summary>
		/// Convert a full frame data in RGB16/RGB565 to a frame data in RGB24
		/// </summary>
		/// <param name="dim">dimensions of the frame</param>
		/// <param name="frame">ushort buffer with the RGB565 frame data</param>
		/// <returns>byte buffer with the RGB24 frame data</returns>
		public static byte[] ConvertRGB16ToRGB24(Dimensions dim, ushort[] frame)
		{
			byte[] rgb24Data = new byte[dim.Surface * 3];
			for (var i = 0; i < dim.Surface; i++) {
				rgb565_to_rgb888(frame[i], ref rgb24Data[i * 3], ref rgb24Data[i * 3 + 1], ref rgb24Data[i * 3 + 2]);
			}
			return rgb24Data;
		}

		/// <summary>
		/// Implementation of Scale2 for RGB frame data.
		/// </summary>
		/// <param name="dim">Original dimensions</param>
		/// <param name="data">Original frame data</param>
		/// <returns>scaled frame planes</returns>
		public static byte[] Scale2XRgb(Dimensions dim, byte[] data)
		{
			using (Profiler.Start("FrameUtil.Scale2XRgb")) {
				var targetWidth = dim.Width * 2;
				var targetHeight = dim.Height * 2;
				byte[] scaledData = new byte[targetWidth * targetHeight * 3];

				for (var y = 0; y < dim.Height; y++)
				{
					for (var x = 0; x < dim.Width; x++)
					{
						var colorB = ImageUtil.GetRgbPixel(x, y - 1, dim.Width, dim.Height, data);
						var colorH = ImageUtil.GetRgbPixel(x, y + 1, dim.Width, dim.Height, data);
						var colorD = ImageUtil.GetRgbPixel(x - 1, y, dim.Width, dim.Height, data);
						var colorF = ImageUtil.GetRgbPixel(x + 1, y, dim.Width, dim.Height, data);

						var colorE = ImageUtil.GetRgbPixel(x, y, dim.Width, dim.Height, data);
						if (!CompareBuffers(colorB, 0, colorH, 0, 3) && !CompareBuffers(colorD, 0, colorF, 0, 3))
						{
							ImageUtil.SetRgbPixel(2 * x, 2 * y, CompareBuffers(colorD, 0, colorB, 0, 3) ? colorD : colorE, targetWidth, scaledData);
							ImageUtil.SetRgbPixel(2 * x + 1, 2 * y, CompareBuffers(colorB, 0, colorF, 0, 3) ? colorF : colorE, targetWidth, scaledData);
							ImageUtil.SetRgbPixel(2 * x, 2 * y + 1, CompareBuffers(colorD, 0, colorH, 0, 3) ? colorD : colorE, targetWidth, scaledData);
							ImageUtil.SetRgbPixel(2 * x + 1, 2 * y + 1, CompareBuffers(colorH, 0, colorF, 0, 3) ? colorF : colorE, targetWidth, scaledData);
						}
						else
						{
							ImageUtil.SetRgbPixel(2 * x, 2 * y, colorE, targetWidth, scaledData);
							ImageUtil.SetRgbPixel(2 * x + 1, 2 * y, colorE, targetWidth, scaledData);
							ImageUtil.SetRgbPixel(2 * x, 2 * y + 1, colorE, targetWidth, scaledData);
							ImageUtil.SetRgbPixel(2 * x + 1, 2 * y + 1, colorE, targetWidth, scaledData);
						}
					}
				}
				return scaledData;
			}
		}

		//Scale down planes by displaying every second pixel
		private static byte[] ScaleDown(Dimensions dim, byte[] srcPlane)
		{
			using (Profiler.Start("FrameUtil.ScaleDown")) {

				var planeSize = dim.Surface / 8;
				byte[] scaledPlane = new byte[planeSize];
				ushort[] plane = new ushort[planeSize * 2];
				Buffer.BlockCopy(srcPlane, 0, plane, 0, planeSize * 4);

				for (var i = 0; i < dim.Height*2; i++)
				{
					for (var k = 0; k < (dim.Width / 8); k++)
					{
						ushort srcVal = plane[(i * (dim.Width / 8)) + k];
						byte destVal = (byte) ((srcVal & 0x0001) | (srcVal & 0x0004) >> 1 | (srcVal & 0x0010) >> 2 | (srcVal & 0x0040) >> 3 | (srcVal & 0x0100) >> 4 | (srcVal & 0x0400) >> 5 | (srcVal & 0x1000) >> 6 | (srcVal & 0x4000) >> 7);
						scaledPlane[((i / 2) * (dim.Width / 8)) + k] = destVal;
					}
					i++;
				}
				return scaledPlane;
			}
		}

		public static byte[][] ScaleDown(Dimensions dim, byte[][] srcPlanes)
		{
			using (Profiler.Start("FrameUtil.ScaleDown")) {
				var planes = new byte[srcPlanes.Length][];
				for (var l = 0; l < srcPlanes.Length; l++)
				{
					planes[l] = ScaleDown(dim, srcPlanes[l]);
				}
				return planes;
			}
		}

		/// <summary>
		/// Converts a 2-bit frame to a 4-bit frame or vice versa
		/// </summary>
		/// <param name="srcFrame">Top left bottom right pixels with values between 0 and 3</param>
		/// <param name="mapping">A list of values assigned to each of the pixels.</param>
		/// <returns>Top left bottom right pixels with values between 0 and 15</returns>
		public static byte[] ConvertGrayToGray(byte[] srcFrame, params byte[] mapping)
		{
			using (Profiler.Start("FrameUtil.ConvertGrayToGray")) {

				var destFrame = new byte[srcFrame.Length];
				for (var i = 0; i < destFrame.Length; i++) {
					destFrame[i] = mapping[srcFrame[i]];
				}
				return destFrame;
			}
		}

		public static byte[] NewPlane(Dimensions dim)
		{
			var count = dim.Width / 8 * dim.Height;
			var destFrame = new byte[count];
			return destFrame;
		}


		public static void ClearPlane(byte[] plane)
		{
			unsafe
			{
				fixed (byte* b1 = plane)
				{
					memset(b1, 0, plane.Length);
				}
			}
		}

		public static void OrPlane(byte[] plane, byte[] target)
		{
			Debug.Assert(plane.Length == target.Length);
			unsafe
			{
				fixed (void* b1 = plane, b2 = target)
				{
					int* p = (int *)b1;
					int* t = (int *)b2;

					int count = plane.Length / 4;

					while(count-- > 0)
					{
						*t = *t | *p;
						t++;
						p++;
					}
				}
			}
		}


		/// <summary>
		/// Combine Plane A and Plane B using a mask.   Masked parts from A will be used.
		/// </summary>
		/// <param name="planeA">Plane A</param>
		/// <param name="planeB">Plane B</param>
		/// <param name="mask">Mask</param>
		/// <returns>Combined plane</returns>
		public static byte[] CombinePlaneWithMask(byte[] planeA, byte[] planeB, byte[] mask)
		{
			var length = planeA.Length;
			Debug.Assert(length == planeB.Length && length == mask.Length);
			byte[] outPlane = new byte[length];

			unchecked
			{
				for (int i = 0; i < length; i++)
				{
					var maskBits = mask[i];
					outPlane[i] = (byte)((planeA[i] & maskBits) | (planeB[i] & ~maskBits));
				}
			}
			return outPlane;
		}

		/// <summary>
		/// Tuät ä Bit-Ebini uifd Konsolä uisä druckä
		/// </summary>
		/// <param name="dim">Dimensionä vom Biud</param>
		/// <param name="frame">D Bit-Ebini</param>
		public static void DumpHex(Dimensions dim, byte[] frame)
		{
			var i = 0;
			for (var y = 0; y < dim.Height; y++) {
				var sb = new StringBuilder(dim.Width);
				for (var x = 0; x < dim.Width; x++) {
					sb.Append(frame[i++].ToString("X"));
				}
				Logger.Debug(sb);
			}
		}

		/// <summary>
		/// Tuät äs Biud uifd Konsolä uisä druckä
		/// </summary>
		/// <param name="dim">Dimensionä vom Biud</param>
		/// <param name="plane">S Biud</param>
		public static void DumpBinary(Dimensions dim, byte[] plane)
		{
			var i = 0;
			var planeBits = new BitArray(plane);
			for (var y = 0; y < dim.Height; y++) {
				var sb = new StringBuilder(dim.Width);
				for (var x = 0; x < dim.Width; x++) {
					sb.Append(planeBits.Get(i++) ? "1" : "0");
				}
				Logger.Debug(sb);
			}
		}

		/// <summary>
		/// Tuät äs Biud häschä
		/// </summary>
		/// <returns>Dr Häsch</returns>
		public static readonly uint[] checksumtable = {
				0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F,
				0xE963A535, 0x9E6495A3, 0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988,
				0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91, 0x1DB71064, 0x6AB020F2,
				0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
				0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9,
				0xFA0F3D63, 0x8D080DF5, 0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172,
				0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B, 0x35B5A8FA, 0x42B2986C,
				0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
				0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423,
				0xCFBA9599, 0xB8BDA50F, 0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924,
				0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D, 0x76DC4190, 0x01DB7106,
				0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
				0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D,
				0x91646C97, 0xE6635C01, 0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E,
				0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457, 0x65B0D9C6, 0x12B7E950,
				0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
				0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7,
				0xA4D1C46D, 0xD3D6F4FB, 0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0,
				0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9, 0x5005713C, 0x270241AA,
				0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
				0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81,
				0xB7BD5C3B, 0xC0BA6CAD, 0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A,
				0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683, 0xE3630B12, 0x94643B84,
				0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
				0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB,
				0x196C3671, 0x6E6B06E7, 0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC,
				0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5, 0xD6D6A3E8, 0xA1D1937E,
				0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
				0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55,
				0x316E8EEF, 0x4669BE79, 0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236,
				0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F, 0xC5BA3BBE, 0xB2BD0B28,
				0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
				0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F,
				0x72076785, 0x05005713, 0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38,
				0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21, 0x86D3D2D4, 0xF1D4E242,
				0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
				0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69,
				0x616BFFD3, 0x166CCF45, 0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2,
				0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB, 0xAED16A4A, 0xD9D65ADC,
				0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
				0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693,
				0x54DE5729, 0x23D967BF, 0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94,
				0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
			};

		public static readonly byte[] reversebyte = {
			0x00, 0x80, 0x40, 0xc0, 0x20, 0xa0, 0x60, 0xe0,
			0x10, 0x90, 0x50, 0xd0, 0x30, 0xb0, 0x70, 0xf0,
			0x08, 0x88, 0x48, 0xc8, 0x28, 0xa8, 0x68, 0xe8,
			0x18, 0x98, 0x58, 0xd8, 0x38, 0xb8, 0x78, 0xf8,
			0x04, 0x84, 0x44, 0xc4, 0x24, 0xa4, 0x64, 0xe4,
			0x14, 0x94, 0x54, 0xd4, 0x34, 0xb4, 0x74, 0xf4,
			0x0c, 0x8c, 0x4c, 0xcc, 0x2c, 0xac, 0x6c, 0xec,
			0x1c, 0x9c, 0x5c, 0xdc, 0x3c, 0xbc, 0x7c, 0xfc,
			0x02, 0x82, 0x42, 0xc2, 0x22, 0xa2, 0x62, 0xe2,
			0x12, 0x92, 0x52, 0xd2, 0x32, 0xb2, 0x72, 0xf2,
			0x0a, 0x8a, 0x4a, 0xca, 0x2a, 0xaa, 0x6a, 0xea,
			0x1a, 0x9a, 0x5a, 0xda, 0x3a, 0xba, 0x7a, 0xfa,
			0x06, 0x86, 0x46, 0xc6, 0x26, 0xa6, 0x66, 0xe6,
			0x16, 0x96, 0x56, 0xd6, 0x36, 0xb6, 0x76, 0xf6,
			0x0e, 0x8e, 0x4e, 0xce, 0x2e, 0xae, 0x6e, 0xee,
			0x1e, 0x9e, 0x5e, 0xde, 0x3e, 0xbe, 0x7e, 0xfe,
			0x01, 0x81, 0x41, 0xc1, 0x21, 0xa1, 0x61, 0xe1,
			0x11, 0x91, 0x51, 0xd1, 0x31, 0xb1, 0x71, 0xf1,
			0x09, 0x89, 0x49, 0xc9, 0x29, 0xa9, 0x69, 0xe9,
			0x19, 0x99, 0x59, 0xd9, 0x39, 0xb9, 0x79, 0xf9,
			0x05, 0x85, 0x45, 0xc5, 0x25, 0xa5, 0x65, 0xe5,
			0x15, 0x95, 0x55, 0xd5, 0x35, 0xb5, 0x75, 0xf5,
			0x0d, 0x8d, 0x4d, 0xcd, 0x2d, 0xad, 0x6d, 0xed,
			0x1d, 0x9d, 0x5d, 0xdd, 0x3d, 0xbd, 0x7d, 0xfd,
			0x03, 0x83, 0x43, 0xc3, 0x23, 0xa3, 0x63, 0xe3,
			0x13, 0x93, 0x53, 0xd3, 0x33, 0xb3, 0x73, 0xf3,
			0x0b, 0x8b, 0x4b, 0xcb, 0x2b, 0xab, 0x6b, 0xeb,
			0x1b, 0x9b, 0x5b, 0xdb, 0x3b, 0xbb, 0x7b, 0xfb,
			0x07, 0x87, 0x47, 0xc7, 0x27, 0xa7, 0x67, 0xe7,
			0x17, 0x97, 0x57, 0xd7, 0x37, 0xb7, 0x77, 0xf7,
			0x0f, 0x8f, 0x4f, 0xcf, 0x2f, 0xaf, 0x6f, 0xef,
			0x1f, 0x9f, 0x5f, 0xdf, 0x3f, 0xbf, 0x7f, 0xff,
		};

		public static uint Checksum(byte[] input, bool Reverse = false)
		{
			unchecked {
				var cs = (uint)(((uint)0) ^ (-1));
				var len = input.Length;
				if (!Reverse)
				{
					for (var i = 0; i < len; i++)
					{
						cs = (cs >> 8) ^ checksumtable[(cs ^ input[i]) & 0xFF];
					}
				}
				else
				{
					for (var i = 0; i < len; i++)
					{
						cs = (cs >> 8) ^ checksumtable[(cs ^ reversebyte[input[i]]) & 0xFF];
					}
				}
				cs = (uint)(cs ^ (-1));

				if (cs < 0) {
					cs += (uint)4294967296;
				}
				return cs;
			}
		}

		public static uint ChecksumWithMask(byte[] input, byte[] mask, bool Reverse = false)
		{
			unchecked
			{
				var cs = (uint)(((uint)0) ^ (-1));
				var len = input.Length;
				if (!Reverse)
				{
					for (var i = 0; i < len; i++)
					{
						cs = (cs >> 8) ^ checksumtable[(cs ^ (input[i] & mask[i])) & 0xFF];
					}
				}
				else
				{
					for (var i = 0; i < len; i++)
					{
						cs = (cs >> 8) ^ checksumtable[(cs ^ (reversebyte[input[i]] & mask[i])) & 0xFF];
					}
				}
				cs = (uint)(cs ^ (-1));

				if (cs < 0)
				{
					cs += (uint)4294967296;
				}
				return cs;
			}
		}

		[DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
		public static extern unsafe IntPtr memset(byte* dest, int c, int byteCount);


		[DllImport("msvcrt.dll", CallingConvention=CallingConvention.Cdecl)]
		private static extern unsafe int memcmp(byte* b1, byte* b2, int count);

		/// <summary>
		/// Compares two byte arrays and returns true if they are identical.
		/// </summary>
		/// <param name="buffer1">First buffer to compare</param>
		/// <param name="offset1">Offset in first buffer</param>
		/// <param name="buffer2">Second buffer to compare</param>
		/// <param name="offset2">Offset of second buffer</param>
		/// <param name="count">Byte length to compare</param>
		/// <returns>True if identical, false otherwise.</returns>
		public static unsafe bool CompareBuffers(byte[] buffer1, int offset1, byte[] buffer2, int offset2, int count)
		{
			if (buffer1 == null || buffer2 == null) {
				return false;
			}

			fixed (byte* b1 = buffer1, b2 = buffer2) {
				return memcmp(b1 + offset1, b2 + offset2, count) == 0;
			}
		}

		/// <summary>
		/// Fast byte array comparison, courtesy to https://stackoverflow.com/questions/43289/comparing-two-byte-arrays-in-net/8808245#8808245
		///
		/// This is about 10x faster than <see cref="CompareBuffers(byte[], int, byte[], int, int)"/>, but it doesn't
		/// allow providing an offset.
		/// </summary>
		/// <remarks>
		/// Copyright (c) 2008-2013 Hafthor Stefansson
		/// Distributed under the MIT/X11 software license
		/// Ref: http://www.opensource.org/licenses/mit-license.php.
		/// </remarks>
		/// <param name="a1">First array to compare</param>
		/// <param name="a2">Second array to compare</param>
		/// <returns>True if byte arrays are identical, false otherwise.</returns>
		public static unsafe bool CompareBuffersFast(byte[] a1, byte[] a2) {
			unchecked {
				if (a1 == a2) {
					return true;
				}

				if (a1 == null || a2 == null || a1.Length != a2.Length) {
					return false;
				}

				fixed (byte* p1=a1, p2=a2) {
					byte* x1=p1, x2=p2;
					int l = a1.Length;
					for (int i = 0; i < l / 8; i++, x1 += 8, x2 += 8) {
						if (*((long*)x1) != *((long*)x2)) {
							return false;
						}
					}

					if ((l & 4) != 0) {
						if (*((int*)x1) != *((int*)x2)) {
							return false;
						}
						x1+=4;
						x2+=4;
					}

					if ((l & 2) != 0) {
						if (*((short*)x1) != *((short*)x2)) {
							return false;
						} 
						x1+=2; 
						x2+=2;
					}

					if ((l & 1) == 0) {
						return true;
					}

					return *x1 == *x2;
				}
			}
		}
	}
}

public enum ColorMatrix
{
	Rgb, Rbg
}
