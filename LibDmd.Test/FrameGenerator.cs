using System;
using System.Collections.Generic;
using System.Linq;
using LibDmd.Frame;

namespace LibDmd.Test
{
	public static class FrameGenerator
	{
		public static DmdFrame FromString(string frame)
		{
			var (data, dim, bitLength) = Parse(frame);
			return new DmdFrame(dim, data, bitLength);
		}

		public static DmdFrame FromString(string red, string green, string blue)
		{
			var (r, redDim) = Parse2CharsPerColor(red);
			var (g, greenDim) = Parse2CharsPerColor(green);
			var (b, blueDim) = Parse2CharsPerColor(blue);

			if (redDim != greenDim || greenDim != blueDim) {
				throw new ArgumentException("Dimensions must match.");
			}

			var rgb24Frame = new DmdFrame(redDim, 24);
			for (var i = 0; i < rgb24Frame.Data.Length; i += 3) {
				rgb24Frame.Data[i] = r[i / 3];
				rgb24Frame.Data[i + 1] = g[i / 3];
				rgb24Frame.Data[i + 2] = b[i / 3];
			}

			return rgb24Frame;
		}
		
		private static (byte[], Dimensions, int) Parse(string frame)
		{
			var lines = frame
				.Trim()
				.Split('\n')
				.Select(l => l.Trim())
				.Where(l => l.Length > 0)
				.ToArray();

			var width = lines.OrderBy(l => -l.Length).First().Length;
			var height = lines.Length;
			
			var data = new byte[width * height];
			var colors = new HashSet<int>();
			for (var y = 0; y < height; y++) {
				var line = lines[y];
				for (var x = 0; x < width; x++) {
					if (x < line.Length) {
						var c = line[x];
						data[y * width + x] = Convert.ToByte(c.ToString(), 16);
					} else {
						data[y * width + x] = 0;
					}
					colors.Add(data[y * width + x]);
				}
			}

			var bitLength = (colors.OrderBy(c => -c).First() + 1).GetBitLength();
			return (data, new Dimensions(width, height), bitLength);
		}
		
		private static (byte[], Dimensions) Parse2CharsPerColor(string frame)
		{
			var lines = frame
				.Trim()
				.Split('\n')
				.Select(l => l.Trim())
				.Select(l => l.Split(' '))
				.Where(l => l.Length > 0)
				.ToArray();

			var width = lines.OrderBy(l => -l.Length).First().Length;
			var height = lines.Length;
			
			var data = new byte[width * height];
			for (var y = 0; y < height; y++) {
				var line = lines[y];
				for (var x = 0; x < width; x++) {
					if (x < line.Length) {
						var c = line[x];
						data[y * width + x] = Convert.ToByte(c, 16);
					} else {
						data[y * width + x] = 0;
					}
				}
			}

			return (data, new Dimensions(width, height));
		}
	}
}
