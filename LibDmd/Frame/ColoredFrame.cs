using System;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;

namespace LibDmd
{
	public class ColoredFrame : BaseFrame, ICloneable
	{
		/// <summary>
		/// Frame data, split into bit planes
		/// </summary>
		public byte[][] Planes { get; private set; }

		/// <summary>
		/// Color palette
		/// </summary>
		public Color[] Palette { get; private set; }

		/// <summary>
		/// Palette index from animation or -1 if not set.
		/// </summary>
		public int PaletteIndex { get; }

		/// <summary>
		/// Colour Rotation descriptions.
		/// </summary>
		/// <remarks>
		/// Size: 8*3 bytes: 8 colour rotations available per frame, 1 byte for the first colour,
		/// 1 byte for the number of colours, 1 byte for the time interval between 2 rotations in 10ms
		/// <remarks>
		public byte[] Rotations { get; }

		/// <summary>
		/// If set, colors defined in <see cref="Rotations" are rotated./>
		/// </summary>
		public readonly bool RotateColors;
		
		public ColoredFrame()
		{
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette, int paletteIndex)
		{
			Dimensions = dim;
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette, int paletteIndex,bool rotateColors, byte[] rotations)
		{
			Dimensions = dim;
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
			RotateColors = rotateColors;
			Rotations= rotations;
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette)
		{
			Dimensions = dim;
			Planes = planes;
			Palette = palette;
			RotateColors= false;
			PaletteIndex = -1;
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette, byte[] rotations)
		{
			Dimensions = dim;
			Planes = planes;
			Palette = palette;
			RotateColors= true;
			Rotations = rotations;
			PaletteIndex = -1;
		}

		public ColoredFrame(Dimensions dim, byte[] frame, Color color)
		{
			Dimensions = dim;
			Planes = FrameUtil.Split(dim, 2, frame);
			Palette = ColorUtil.GetPalette(new[] { Colors.Black, color }, 4);
			RotateColors = false;
		}

		public object Clone() => new ColoredFrame(Dimensions, Planes, Palette, PaletteIndex);
		
		public DmdFrame ConvertToGray()
		{
			return new DmdFrame(Dimensions, FrameUtil.Join(Dimensions, Planes), Planes.Length);
		}
		
		public DmdFrame ConvertToGray(params byte[] mapping)
		{
			var data = FrameUtil.Join(Dimensions, Planes);
			return new DmdFrame(Dimensions, FrameUtil.ConvertGrayToGray(data, mapping), mapping.Length.GetBitLength());
		}

		public ColoredFrame Update(byte[][] planes, Color[] palette)
		{
			Planes = planes;
			Palette = palette;
			return this;
		}
	}
}
