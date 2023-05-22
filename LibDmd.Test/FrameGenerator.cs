using System;
using System.Linq;
using LibDmd.Frame;

namespace LibDmd.Test
{
	public static class FrameGenerator
	{
		public static DMDFrame FromString(string frame)
		{
			var lines = frame
				.Split('\n')
				.Select(l => l.Trim())
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
						data[y * width + x] = Convert.ToByte(c.ToString(), 16);
					} else {
						data[y * width + x] = 0;
					}
				}
			}

			return new DMDFrame{ Dimensions = new Dimensions(width, height), Data = data };
		}
	}
}
