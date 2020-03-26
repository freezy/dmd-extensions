﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using NLog;
using NLog.Fluent;
using Color = System.Windows.Media.Color;

namespace LibDmd.Common
{
	/// <summary>
	/// Wärchziig zum hin- und härkonvertiärä vo Biud-Datä.
	/// </summary>
	public class FrameUtil
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Tuät es Biud i sini Bitahteiu uifteilä.
		/// </summary>
		/// 
		/// <remarks>
		/// Mr chas so gseh dass für äs Biud mit viar Graiteen zwe Ebänä fir
		/// jedes Bit uisächemid
		/// </remarks>
		/// 
		/// <param name="width">Bräiti vom Biud</param>
		/// <param name="height">Heechi vom Biud</param>
		/// <param name="bitlen">Mit wefu Bits pro Pixu s Biud konstruiärt isch</param>
		/// <param name="frame">D datä vom Biud</param>
		/// <returns>Än Ebini fir jedes Bit</returns>
		public static byte[][] Split(int width, int height, int bitlen, byte[] frame)
		{
			var planeSize = width * height / 8;
			var planes = new byte[bitlen][];
			for (var i = 0; i < bitlen; i++) {
				planes[i] = new byte[planeSize];
			}
			var byteIdx = 0;
			var bd = new byte[bitlen];
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x += 8) {
					for (var i = 0; i < bitlen; i++) {
						bd[i] = 0;
					}
					for (var v = 7; v >= 0; v--) {
						var pixel = frame[(y * width) + (x + v)];
						for (var i = 0; i < bitlen; i++) {
							bd[i] <<= 1;
							if ((pixel & ( 1 << i) ) != 0) {
								bd[i] |= 1;
							}
						}
					}
					for (var i = 0; i < bitlen; i++) {
						planes[i][byteIdx] = bd[i];
					}
					byteIdx++;
				}
			}
			return planes;
		}

		/// <summary>
		/// Puts an sequence of flat/concatenated bitplanes into an array.
		/// </summary>
		/// <param name="width">Width of the frame</param>
		/// <param name="height">Height of the frame</param>
		/// <param name="src">Concatenated bitplanes</param>
		/// <returns></returns>
		public static byte[][] SplitBitplanes(int width, int height, byte[] src)
		{
			var size = width * height / 8;
			var bitlen = src.Length / size;
			var planes = new byte[bitlen][];
			for (var i = 0; i < bitlen; i++) {
				planes[i] = src.Skip(i * size).Take(size).ToArray();
			}
			Logger.Debug("[SplitBitplanes] From {0} bytes to {1} planes at {2} bytes.", src.Length, bitlen, size);
			return planes;
		}

		/// <summary>
		/// Splits an RGB24 frame into each bit plane.
		/// </summary>
		/// <param name="width">Width of the frame</param>
		/// <param name="height">Height of the frame</param>
		/// <param name="frame">RGB24 data, top-left to bottom-right</param>
		/// <param name="frameBuffer">Destination buffer where planes are written</param>
		/// <param name="offset">Start writing at this offset</param>
		/// <returns>True if destination buffer changed, false otherwise.</returns>
		public static bool SplitRgb24(int width, int height, byte[] frame, byte[] frameBuffer, int offset)
		{
			var byteIdx = offset;
			var identical = true;
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x += 8) {
					byte r3 = 0;
					byte r4 = 0;
					byte r5 = 0;
					byte r6 = 0;
					byte r7 = 0;

					byte g3 = 0;
					byte g4 = 0;
					byte g5 = 0;
					byte g6 = 0;
					byte g7 = 0;

					byte b3 = 0;
					byte b4 = 0;
					byte b5 = 0;
					byte b6 = 0;
					byte b7 = 0;

					for (var v = 7; v >= 0; v--) {
						var pos = (y * width + x + v) * 3;

						var pixelr = frame[pos];
						var pixelg = frame[pos + 1];
						var pixelb = frame[pos + 2];

						r3 <<= 1;
						r4 <<= 1;
						r5 <<= 1;
						r6 <<= 1;
						r7 <<= 1;
						g3 <<= 1;
						g4 <<= 1;
						g5 <<= 1;
						g6 <<= 1;
						g7 <<= 1;
						b3 <<= 1;
						b4 <<= 1;
						b5 <<= 1;
						b6 <<= 1;
						b7 <<= 1;

						if ((pixelr & 8) != 0) r3 |= 1;
						if ((pixelr & 16) != 0) r4 |= 1;
						if ((pixelr & 32) != 0) r5 |= 1;
						if ((pixelr & 64) != 0) r6 |= 1;
						if ((pixelr & 128) != 0) r7 |= 1;

						if ((pixelg & 8) != 0) g3 |= 1;
						if ((pixelg & 16) != 0) g4 |= 1;
						if ((pixelg & 32) != 0) g5 |= 1;
						if ((pixelg & 64) != 0) g6 |= 1;
						if ((pixelg & 128) != 0) g7 |= 1;

						if ((pixelb & 8) != 0) b3 |= 1;
						if ((pixelb & 16) != 0) b4 |= 1;
						if ((pixelb & 32) != 0) b5 |= 1;
						if ((pixelb & 64) != 0) b6 |= 1;
						if ((pixelb & 128) != 0) b7 |= 1;
					}

					identical = identical &&
						frameBuffer[byteIdx + 5120] == r3 &&
						frameBuffer[byteIdx + 5632] == r4 &&
						frameBuffer[byteIdx + 6144] == r5 &&
						frameBuffer[byteIdx + 6656] == r6 &&
						frameBuffer[byteIdx + 7168] == r7 &&

						frameBuffer[byteIdx + 2560] == g3 &&
						frameBuffer[byteIdx + 3072] == g4 &&
						frameBuffer[byteIdx + 3584] == g5 &&
						frameBuffer[byteIdx + 4096] == g6 &&
						frameBuffer[byteIdx + 4608] == g7 &&

						frameBuffer[byteIdx + 0] == b3 &&
						frameBuffer[byteIdx + 512] == b4 &&
						frameBuffer[byteIdx + 1024] == b5 &&
						frameBuffer[byteIdx + 1536] == b6 &&
						frameBuffer[byteIdx + 2048] == b7;


					frameBuffer[byteIdx + 5120] = r3;
					frameBuffer[byteIdx + 5632] = r4;
					frameBuffer[byteIdx + 6144] = r5;
					frameBuffer[byteIdx + 6656] = r6;
					frameBuffer[byteIdx + 7168] = r7;

					frameBuffer[byteIdx + 2560] = g3;
					frameBuffer[byteIdx + 3072] = g4;
					frameBuffer[byteIdx + 3584] = g5;
					frameBuffer[byteIdx + 4096] = g6;
					frameBuffer[byteIdx + 4608] = g7;

					frameBuffer[byteIdx + 0] = b3;
					frameBuffer[byteIdx + 512] = b4;
					frameBuffer[byteIdx + 1024] = b5;
					frameBuffer[byteIdx + 1536] = b6;
					frameBuffer[byteIdx + 2048] = b7;
					byteIdx++;
				}
			}
			return !identical;
		}

		/// <summary>
		/// Tuät mehreri Bit-Ebänä widr zämäfiägä.
		/// </summary>
		/// <param name="width">Bräiti vom Biud</param>
		/// <param name="height">Heechi vom Biud</param>
		/// <param name="bitPlanes">Ä Lischtä vo Ebänä zum zämäfiägä</param>
		/// <returns>Äs Graistuifäbiud mit sefu Bittiäfi wiä Ebänä gä wordä sind</returns>
		public static byte[] Join(int width, int height, byte[][] bitPlanes)
		{
			var frame = new byte[width * height];

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

						System.Diagnostics.Debug.Assert(bitPlanes.Length == 4);
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
							Logger.Error("Error retrieving pixel {0} on plane {1}. Frame size = {2}x{3}, plane size = {4}.", f, p, width, height, planes[p].Length);
							throw;
						}
					}
				}
			}
			return frame;
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
						var indexWithinSubframe = mapAdafruitIndex(x, y, width, height, numLogicalRows);
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

		private static int mapAdafruitIndex(int x, int y, int width, int height, int numLogicalRows)
		{
			var pairOffset = 16;
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
	/// Merges an array of bit planes into one single array.
	/// </summary>
	/// <param name="planes">Source planes</param>
	/// <param name="frame">Destination array</param>
	/// <param name="offset">Where to start copying at destination</param>
	/// <returns>True if destination array changed, false otherwise.</returns>
	public static bool Copy(byte[][] planes, byte[] frame, int offset)
		{
			var identical = true;
			foreach (var plane in planes) {
				identical = identical && CompareBuffers(plane, 0, frame, offset, plane.Length);
				Buffer.BlockCopy(plane, 0, frame, offset, plane.Length);
				offset += plane.Length;
			}
			return !identical;
		}

		public static byte[][] Copy(int width, int height, byte[] planes, int bitlength, int offset)
		{
			var copy = new byte[bitlength][];
			var planeSize = width * height / 8;
			for (var i = 0; i < bitlength; i++) {
				copy[i] = new byte[planeSize];
				Buffer.BlockCopy(planes, offset + i * planeSize, copy[i], 0, planeSize);
			}
			return copy;
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

		/// <summary>
		/// Converts a 2-bit frame to a 4-bit frame or vice versa
		/// </summary>
		/// <param name="srcFrame">Top left bottom right pixels with values between 0 and 3</param>
		/// <param name="mapping">A list of values assigned to each of the pixels.</param>
		/// <returns>Top left bottom right pixels with values between 0 and 15</returns>
		public static byte[] ConvertGrayToGray(byte[] srcFrame, byte[] mapping)
		{
			var destFrame = new byte[srcFrame.Length];
			for (var i = 0; i < destFrame.Length; i++) {
				destFrame[i] = mapping[srcFrame[i]];
			}
			return destFrame;
		}

		public static byte[] ConvertToRgb24(int width, int height, byte[][] planes, Color[] palette)
		{
			var frame = Join(width, height, planes);
			return ColorUtil.ColorizeFrame(width, height, frame, palette);
		}

		/// <summary>
		/// Tuät ä Bit-Ebini uifd Konsolä uisä druckä
		/// </summary>
		/// <param name="width">Bräiti vom Biud</param>
		/// <param name="height">Heechi vom Biud</param>
		/// <param name="frame">D Bit-Ebini</param>
		public static void DumpHex(int width, int height, byte[] frame)
		{
			var i = 0;
			for (var y = 0; y < height; y++) {
				var sb = new StringBuilder(width);
				for (var x = 0; x < width; x++) {
					sb.Append(frame[i++].ToString("X"));
				}
				Logger.Debug(sb);
			}
		}

		/// <summary>
		/// Tuät äs Biud uifd Konsolä uisä druckä
		/// </summary>
		/// <param name="width">Bräiti vom Biud</param>
		/// <param name="height">Heechi vom Biud</param>
		/// <param name="plane">S Biud</param>
		public static void DumpBinary(int width, int height, byte[] plane)
		{
			var i = 0;
			var planeBits = new BitArray(plane);
			for (var y = 0; y < height; y++) {
				var sb = new StringBuilder(width);
				for (var x = 0; x < width; x++) {
					sb.Append(planeBits.Get(i++) ? "1" : "0");
				}
				Logger.Debug(sb);
			}
		}

		/// <summary>
		/// Tuät äs Biud häschä
		/// </summary>
		/// <param name="input">S Biud</param>
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

		public static uint Checksum(byte[] input)
		{
			unchecked {
				var cs = (uint)(((uint)0) ^ (-1));
				var len = input.Length;
				for (var i = 0; i < len; i++) {
					cs = (cs >> 8) ^ checksumtable[(cs ^ input[i]) & 0xFF];
				}
				cs = (uint)(cs ^ (-1));

				if (cs < 0) {
					cs += (uint)4294967296;
				}
				return cs;
			}
		}

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
	}
}

public enum ColorMatrix
{
	Rgb, Rbg
}