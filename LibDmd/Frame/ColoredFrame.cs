using System;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;

namespace LibDmd.Frame
{
	public class ColoredFrame : ICloneable
	{
		/// <summary>
		/// Dimensions of the frame, in pixel
		/// </summary>
		public Dimensions Dimensions;

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

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette)
		{
			Dimensions = dim;
			Planes = planes;
			Palette = palette;
			PaletteIndex = -1;
		}

		public ColoredFrame(Dimensions dim, byte[] frame, Color color)
		{
			Planes = FrameUtil.Split(dim, 2, frame);
			Palette = ColorUtil.GetPalette(new[] { Colors.Black, color }, 4);
		}

		public ColoredFrame Update(Dimensions dimensions)
		{
			Dimensions = dimensions;
			return this;
		}

		public ColoredFrame Update(byte[][] planes)
		{
			Planes = planes;
			return this;
		}

		public ColoredFrame Update(byte[][] planes, Color[] palette)
		{
			Planes = planes;
			Palette = palette;
			return this;
		}

		public ColoredFrame Flip(bool flipHorizontally, bool flipVertically)
		{
			Planes = TransformationUtil.Flip(Dimensions, Planes, flipHorizontally, flipVertically);
			return this;
		}

		public object Clone()
		{
			return new ColoredFrame(Dimensions, Planes, Palette, PaletteIndex);
		}

		public DmdFrame ConvertToGray(params byte[] mapping)
		{
			var data = FrameUtil.Join(Dimensions, Planes);
			return new DmdFrame(Dimensions, FrameUtil.ConvertGrayToGray(data, mapping));
		}

		public DmdFrame ConvertToGray()
		{
			return new DmdFrame(Dimensions, FrameUtil.Join(Dimensions, Planes));
		}

		public DmdFrame ConvertToRgb24()
		{
			return new DmdFrame(Dimensions, ColorUtil.ColorizeFrame(
				Dimensions,
				FrameUtil.Join(Dimensions, Planes),
				Palette
			));
		}

		public BmpFrame ConvertToBmp()
		{
			return new BmpFrame(
				ImageUtil.ConvertFromRgb24(Dimensions,
					ColorUtil.ColorizeFrame(
						Dimensions,
						FrameUtil.Join(Dimensions, Planes),
						Palette
					)
				)
			);
		}
	}
}
