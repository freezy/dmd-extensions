using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using LibDmd.Frame;

namespace LibDmd.Test
{
	public static class FrameGenerator
	{
		public static DmdFrame FromString(string frame)
		{
			var match = Regex.Match(frame, @"\d{2} \d{2}", RegexOptions.IgnoreCase);
			var parse = match.Success ? (Func<string,  (byte[], Dimensions, int)>)Parse2CharsPerPixel : Parse;

			var (data, dim, bitLength) = parse(frame);
			return new DmdFrame(dim, data, bitLength);
		}

		public static DmdFrame FromWhiteString(string white) => FromString(white, white, white);

		public static DmdFrame FromString(string red, string green, string blue)
		{
			var (r, redDim, _) = Parse2CharsPerPixel(red);
			var (g, greenDim, _) = Parse2CharsPerPixel(green);
			var (b, blueDim, _) = Parse2CharsPerPixel(blue);

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

		public static ColoredFrame FromString(string frame, params Color[] palette)
		{
			var match = Regex.Match(frame, @"\d{2} \d{2}", RegexOptions.IgnoreCase);
			var parse = match.Success ? (Func<string,  (byte[], Dimensions, int)>)Parse2CharsPerPixel : Parse;

			var (data, dim, bitLength) = parse(frame);
			return new ColoredFrame(new DmdFrame(dim, data, bitLength), palette);
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
			var max = 0;
			for (var y = 0; y < height; y++) {
				var line = lines[y];
				for (var x = 0; x < width; x++) {
					if (x < line.Length) {
						var c = line[x];
						var v = Convert.ToByte(c.ToString(), 16);
						data[y * width + x] = v;
						max = Math.Max(max, v);
					} else {
						data[y * width + x] = 0;
					}
				}
			}

			var bitLength = (max + 1).GetBitLength();
			return (data, new Dimensions(width, height), bitLength);
		}
		
		private static (byte[], Dimensions, int) Parse2CharsPerPixel(string frame)
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
			var max = 0;
			
			var data = new byte[width * height];
			for (var y = 0; y < height; y++) {
				var line = lines[y];
				for (var x = 0; x < width; x++) {
					if (x < line.Length) {
						var c = line[x];
						var v = Convert.ToByte(c, 16);
						data[y * width + x] = v;
						max = Math.Max(max, v);
					} else {
						data[y * width + x] = 0;
					}
				}
			}
			var bitLength = (max + 1).GetBitLength();
			return (data, new Dimensions(width, height), bitLength);
		}
	}
}
