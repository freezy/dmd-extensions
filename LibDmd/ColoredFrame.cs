using System.Windows.Markup;
using System.Windows.Media;
using LibDmd.Common;

namespace LibDmd
{
	public class ColoredFrame
	{

		/// <summary>
		/// Frame data
		/// </summary>
		public byte[] Data { get; }
		public int Width { get; }
		public int Height { get; }

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
		public bool RotateColors = false;
		public byte[] Rotations { get; }

		public ColoredFrame(byte[][] planes, Color[] palette, int paletteIndex)
		{
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
			RotateColors = false;
		}

		public ColoredFrame(byte[][] planes, Color[] palette, int paletteIndex,bool isrotation, byte[] rotations)
		{
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
			RotateColors = isrotation;
			Rotations= rotations;
		}

		public ColoredFrame(byte[][] planes, Color[] palette)
		{
			Planes = planes;
			Palette = palette;
			RotateColors = false;
			PaletteIndex = -1;
		}

		public ColoredFrame(byte[][] planes, Color[] palette, byte[] rotations)
		{
			Planes = planes;
			Palette = palette;
			RotateColors = true;
			Rotations = rotations;
			PaletteIndex = -1;
		}

		public ColoredFrame(int width, int height, byte[] frame, Color color)
		{
			Planes = FrameUtil.Split(width, height, 2, frame);
			Palette = ColorUtil.GetPalette(new[] { Colors.Black, color }, 4);
			RotateColors = false;
		}

		public ColoredFrame(int width, int height, byte[] data)
		{
			Width = width;
			Height = height;
			Data = data;
		}
	}
}
