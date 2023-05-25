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

		public BmpFrame Update(BitmapSource bmp)
		{
			Bitmap = bmp;
			Dimensions = bmp.Dimensions();
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

		/// <summary>
		/// Converts this bitmap frame to a gray6 frame.
		/// </summary>
		/// <returns>Gray6 frame</returns>
		public DmdFrame ConvertToGray6() => new DmdFrame(Dimensions, ImageUtil.ConvertToGray6(Bitmap), 6);

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

		public object Clone()
		{
			return new BmpFrame(Bitmap);
		}

		public override string ToString() => ConvertToRgb24().ToString();
	}
}
