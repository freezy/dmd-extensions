using System;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Frame
{
	public class BmpFrame : BaseFrame, ICloneable
	{
		public BitmapSource Bitmap;

		public BmpFrame()
		{
		}

		public BmpFrame(BitmapSource bitmap)
		{
			Bitmap = bitmap;
			Dimensions = bitmap.Dimensions();
		}
		public BmpFrame(BitmapSource bitmap, Dimensions dim)
		{
			Bitmap = bitmap;
			Dimensions = dim;
		}

		public BmpFrame Update(BitmapSource bmp)
		{
			Bitmap = bmp;
			Dimensions = bmp.Dimensions();
			return this;
		}

		private BmpFrame Update(Dimensions dimensions, byte[] data)
		{
			Dimensions = dimensions;
			Bitmap = ImageUtil.ConvertFromRgb24(Dimensions, data);
			return this;
		}

		// public DmdFrame ConvertToGray2() => ImageUtil.ConvertToGray2(Bitmap);
		//
		// public DmdFrame ConvertToGray4() => ImageUtil.ConvertToGray4(Bitmap);
		//

		/// <summary>
		/// Converts this bitmap frame to an RGB24 frame.
		/// </summary>
		/// <returns>RGB24 frame</returns>
		public DmdFrame ConvertToRgb24() => new DmdFrame(Dimensions, ImageUtil.ConvertToRgb24(Bitmap), 24);

		/// <summary>
		/// Converts this bitmap frame to a gray2 frame.
		/// </summary>
		/// <returns>Gray2 frame</returns>
		public DmdFrame ConvertToGray2() => new DmdFrame(Dimensions, ImageUtil.ConvertToGray2(Bitmap, 0, 1, out _), 2);

		/// <summary>
		/// Converts this bitmap frame to a gray4 frame.
		/// </summary>
		/// <returns>Gray4 frame</returns>
		public DmdFrame ConvertToGray4() => new DmdFrame(Dimensions, ImageUtil.ConvertToGray4(Bitmap), 4);

		public BmpFrame Transform(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			var targetDim = GetTargetDimensions(fixedDest, multiDest);
			var mustResize = targetDim != Dimensions.Dynamic && Dimensions != targetDim;
			if (!mustResize && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}
			return Transform(targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
		}

		private BmpFrame Transform(Dimensions dim, ResizeMode resize, bool flipHorizontally, bool flipVertically)
		{
			Bitmap = TransformationUtil.Transform(Bitmap, dim, resize, flipHorizontally, flipVertically);
			return this;
		}

		/// <summary>
		/// Up-scales the frame with the given algorithm, if the destination allows it.
		/// </summary>
		/// <param name="fixedDest">The fixed destination, null if dynamic. If fixed, DmdAllowHdScaling must be true, and the dimensions must be greater or equal the double of the frame size.</param>
		/// <param name="scalerMode">If and how to scale</param>
		/// <returns>Updated frame instance</returns>
		/// <exception cref="ArgumentException"></exception>
		public BmpFrame TransformHdScaling(IFixedSizeDestination fixedDest, ScalerMode scalerMode)
		{
			// skip if disabled
			if (scalerMode == ScalerMode.None) {
				return this;
			}

			// if destination doesn't allow scaling (e.g. pup), return
			if (fixedDest != null && !fixedDest.DmdAllowHdScaling) {
				return this;
			}

			// if double of frame size doesn't fit into destination, return
			if (fixedDest != null && !(Dimensions * 2).FitsInto(fixedDest.FixedSize)) {
				return this;
			}

			return TransformHdScaling(scalerMode);
		}

		private BmpFrame TransformHdScaling(ScalerMode scalerMode)
		{
			switch (scalerMode) {
				case ScalerMode.None:
					return this;

				case ScalerMode.Doubler:
					return Update(Dimensions * 2, FrameUtil.ScaleDoubleRgb(Dimensions, ConvertToRgb24().Data));

				case ScalerMode.Scale2x:
					return Update(Dimensions * 2, FrameUtil.Scale2XRgb(Dimensions, ConvertToRgb24().Data));

				default:
					throw new ArgumentOutOfRangeException(nameof(scalerMode), scalerMode, null);
			}
		}

		public object Clone()
		{
			return new BmpFrame(Bitmap);
		}

		public override string ToString() => ConvertToRgb24().ToString();
	}
}
