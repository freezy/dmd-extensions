using System.Windows.Media;
using LibDmd.Common;

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

		/// <summary>
		/// Rotation descriptions.
		/// </summary>
		public bool isRotation = false;
		public byte[] Rotations { get; }

		public ColoredFrame(byte[][] planes, Color[] palette, int paletteIndex)
		{
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
			isRotation = false;
		}

		public ColoredFrame(byte[][] planes, Color[] palette, int paletteIndex,bool isrotation, byte[] rotations)
		{
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
			isRotation = isrotation;
			Rotations= rotations;
		}

		public ColoredFrame(byte[][] planes, Color[] palette)
		{
			Planes = planes;
			Palette = palette;
			isRotation= false;
			PaletteIndex = -1;
		}

		public ColoredFrame(byte[][] planes, Color[] palette, byte[] rotations)
		{
			Planes = planes;
			Palette = palette;
			isRotation= true;
			Rotations = rotations;
			PaletteIndex = -1;
		}

		public ColoredFrame(int width, int height, byte[] frame, Color color)
		{
			Planes = FrameUtil.Split(width, height, 2, frame);
			Palette = ColorUtil.GetPalette(new[] { Colors.Black, color }, 4);
			isRotation = false;
		}
	}
}
