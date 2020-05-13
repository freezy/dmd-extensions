using System.Windows.Media;
using LibDmd.Common;

namespace LibDmd.Input
{
	/// <summary>
	/// A non-colored frame.
	/// </summary>
	///
	/// <remarks>
	/// Frames
	/// </remarks>
	public class DmdFrame
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
		public DmdFrame Clone()
		{
			return new DmdFrame(Dimensions, Data);
		}
	}

	public class RawDmdFrame : DmdFrame
	{
		public byte[][] RawPlanes;

		public RawDmdFrame()
		{
		}

		public RawDmdFrame(Dimensions dim) : base(dim)
		{
		}

		public RawDmdFrame Update(Dimensions dim, byte[] data, byte[][]rawPlanes)
		{
			Update(dim, data);
			RawPlanes = rawPlanes;
			return this;
		}

	}
}
