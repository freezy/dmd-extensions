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
			var lines = frame
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

			return new DmdFrame(new Dimensions(width, height), data, colors.Count.GetBitLength());
		}
	}
}
