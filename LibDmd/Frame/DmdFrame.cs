using System;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;

namespace LibDmd.Frame
{
	/// <summary>
	/// A non-colored frame.
	/// </summary>
	///
	/// <remarks>
	/// Frames
	/// </remarks>
	public class DmdFrame : ICloneable
	{
		public Dimensions Dimensions { get; private set; }
		public byte[] Data { get; private set; }

		public DmdFrame()
		{
		}

		public DmdFrame(Dimensions dim)
		{
			Dimensions = dim;
			Data = new byte[dim.Surface];
		}

		public DmdFrame(Dimensions dim, byte[] data)
		{
			Dimensions = dim;
			Data = data;
		}

		public DmdFrame Update(byte[] data)
		{
			Data = data;
			return this;
		}

		public DmdFrame Update(Dimensions dim)
		{
			Dimensions = dim;
			return this;
		}

		public DmdFrame Update(Dimensions dim, byte[] data)
		{
			Dimensions = dim;
			Data = data;
			return this;
		}

		public DmdFrame Colorize(Color[] palette)
		{
			Data = ColorUtil.ColorizeFrame(Dimensions, Data, palette);
			return this;
		}

		public DmdFrame ConvertGrayToGray(params byte[] mapping)
		{
			Data = FrameUtil.ConvertGrayToGray(Data, mapping);
			return this;
		}

		public DmdFrame ConvertToGray(int numColors)
		{
			Data = ImageUtil.ConvertToGray(Dimensions, Data, 4);
			return this;
		}

		public BmpFrame ConvertToBmp()
		{
			return new BmpFrame(ImageUtil.ConvertFromRgb24(Dimensions, Data));
		}

		public BmpFrame ConvertFromGray2(Dimensions dim, double hue, double sat, double lum)
		{
			return new BmpFrame(ImageUtil.ConvertFromGray2(dim, Data, hue, sat, lum));
		}

		public BmpFrame ConvertFromGray4(Dimensions dim, double hue, double sat, double lum)
		{
			return new BmpFrame(ImageUtil.ConvertFromGray4(dim, Data, hue, sat, lum));
		}

		public BmpFrame ConvertFromRgb24(Color[] palette)
		{
			return new BmpFrame(ImageUtil.ConvertFromRgb24(
				Dimensions,
				Colorize(palette).Data
			));
		}

		public DmdFrame Flip(int bytesPerPixel, bool flipHorizontally, bool flipVertically)
		{
			Data = TransformationUtil.Flip(Dimensions, bytesPerPixel, Data, flipHorizontally, flipVertically);
			return this;
		}

		/// <summary>
		/// Flat-clones the frame (i.e. the data is still the same, but now you
		/// can replace it without affecting other references of the frame).
		/// </summary>
		/// <returns></returns>
		public object Clone()
		{
			return new DmdFrame(Dimensions, Data);
		}
	}
}
