using System;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Frame
{
	/// <summary>
	/// A frame sent without a palette or color.
	/// </summary>
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

		public DmdFrame ConvertToRgb24(Color[] palette)
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
			Data = ImageUtil.ConvertToGray(Dimensions, Data, numColors);
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
				ConvertToRgb24(palette).Data
			));
		}

		public DmdFrame TransformGray2(RenderGraph renderGraph, IFixedSizeDestination dest, IMultiSizeDestination multiDest)
		{
			if (dest == null) {
				return Flip(1, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			}

			if (Dimensions == dest.FixedSize && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}

			// block-copy for same width but smaller height
			var cropHeight = Dimensions.Width == dest.FixedSize.Width && Dimensions.Height < dest.FixedSize.Height;
			if (cropHeight && renderGraph.Resize != ResizeMode.Stretch && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				var transformedFrameData = new byte[dest.FixedSize.Surface];
				Buffer.BlockCopy(
					Data,
					0,
					transformedFrameData,
					((dest.FixedSize.Height - Dimensions.Height) / 2) * dest.FixedSize.Width, Data.Length
				);
				return Update(transformedFrameData);
			}

			var bmp = ImageUtil.ConvertFromGray2(Dimensions, Data, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.FixedSize, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			return Update(ImageUtil.ConvertToGray2(transformedBmp));
		}

		public DmdFrame TransformGray4(RenderGraph renderGraph, IFixedSizeDestination dest, IMultiSizeDestination multiDest)
		{
			if (dest == null) {
				return Flip(1, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			}
			if (Dimensions == dest.FixedSize && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}
			var bmp = ImageUtil.ConvertFromGray4(Dimensions, Data, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.FixedSize, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray4(transformedBmp);
			return Update(transformedFrame);
		}

		public DmdFrame TransformRgb24(RenderGraph renderGraph, IFixedSizeDestination dest, IMultiSizeDestination multiDest)
		{
			if (dest == null) {
				return Flip(3, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			}
			if (Dimensions == dest.FixedSize && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}
			var bmp = ImageUtil.ConvertFromRgb24(Dimensions, Data);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.FixedSize, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedFrameData = new byte[dest.FixedSize.Surface * 3];
			ImageUtil.ConvertToRgb24(transformedBmp, transformedFrameData);
			return Update(transformedFrameData);
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
