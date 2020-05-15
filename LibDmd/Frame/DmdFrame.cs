using System;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;
using NLog;

namespace LibDmd.Frame
{
	/// <summary>
	/// A frame sent without a palette or color.
	/// </summary>RGB24 buffer must be divisible by 3
	public class DmdFrame : BaseFrame, ICloneable
	{
		public byte[] Data { get; private set; }

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

		public BmpFrame ConvertFromGray2(double hue, double sat, double lum)
		{
			return new BmpFrame(ImageUtil.ConvertFromGray2(Dimensions, Data, hue, sat, lum));
		}

		public BmpFrame ConvertFromGray4(double hue, double sat, double lum)
		{
			return new BmpFrame(ImageUtil.ConvertFromGray4(Dimensions, Data, hue, sat, lum));
		}

		public BmpFrame ConvertFromRgb24(Color[] palette)
		{
			return new BmpFrame(ImageUtil.ConvertFromRgb24(
				Dimensions,
				ConvertToRgb24(palette).Data
			));
		}

		public DmdFrame TransformGray2(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			var targetDim = GetTargetDimensions(fixedDest, multiDest);

			if (targetDim == Dimensions.Dynamic) {
				return Flip(1, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			}

			if (Dimensions == targetDim && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}

			// block-copy for same width but smaller height
			var cropHeight = Dimensions.Width == targetDim.Width && Dimensions.Height < targetDim.Height;
			if (cropHeight && renderGraph.Resize != ResizeMode.Stretch && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return Update(targetDim, CenterVertically(targetDim, Data));
			}

			// also copy for centering image if smaller
			if (Dimensions < targetDim) {
				return Update(targetDim, CenterFrame(targetDim, Data, 1));
			}

			// otherwise, convert to bitmap, resize, convert back.
			var bmp = ImageUtil.ConvertFromGray2(Dimensions, Data, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			return Update(ImageUtil.ConvertToGray2(transformedBmp));
		}

		public DmdFrame TransformGray4(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			var targetDim = GetTargetDimensions(fixedDest, multiDest);
			if (targetDim == Dimensions.Dynamic) {
				return Flip(1, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			}
			if (Dimensions == targetDim && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}

			// block-copy for same width but smaller height
			var cropHeight = Dimensions.Width == targetDim.Width && Dimensions.Height < targetDim.Height;
			if (cropHeight && renderGraph.Resize != ResizeMode.Stretch && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return Update(targetDim, CenterVertically(targetDim, Data));
			}

			// also copy for centering image if smaller
			if (Dimensions < targetDim) {
				return Update(targetDim, CenterFrame(targetDim, Data, 1));
			}

			// otherwise, convert to bitmap, resize, convert back.
			var bmp = ImageUtil.ConvertFromGray4(Dimensions, Data, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray4(transformedBmp);
			return Update(transformedFrame);
		}

		public DmdFrame TransformRgb24(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			var targetDim = GetTargetDimensions(fixedDest, multiDest);
			if (targetDim == Dimensions.Dynamic) {
				return Flip(3, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			}

			if (Dimensions == targetDim && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}

			// also copy for centering image if smaller
			if (Dimensions < targetDim) {
				return Update(targetDim, CenterFrame(targetDim, Data, 3));
			}

			var bmp = ImageUtil.ConvertFromRgb24(Dimensions, Data);
			var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedFrameData = new byte[targetDim.Surface * 3];
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
