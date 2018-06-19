using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace LibDmd
{
	public class ColoredFrame
	{
		/// <summary>
		/// Frame data, split into bit planes
		/// </summary>
		public byte[][] Planes { get; }

		/// <summary>
		/// Color palette
		/// </summary>
		public Color[] Palette { get; }

		/// <summary>
		/// Palette index from animation or -1 if not set.
		/// </summary>
		public int PaletteIndex { get; }

		public ColoredFrame(byte[][] planes, Color[] palette, int paletteIndex)
		{
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
		}

		public ColoredFrame(byte[][] planes, Color[] palette)
		{
			Planes = planes;
			Palette = palette;
			PaletteIndex = -1;
		}
	}
}
