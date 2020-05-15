using System;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Frame
{
	public class BmpFrame : ICloneable
	{
		public BitmapSource Bitmap;
		public Dimensions Dimensions;

		public BmpFrame()
		{
		}

		public BmpFrame(BitmapSource bitmap)
		{
			Bitmap = bitmap;
			Dimensions = new Dimensions(bitmap.PixelWidth, bitmap.PixelHeight);
		}

		public BmpFrame(BitmapSource bitmap, Dimensions dim)
		{
			Bitmap = bitmap;
			Dimensions = dim;
		}

		public BmpFrame Transform(Dimensions dim, ResizeMode resize, bool flipHorizontally, bool flipVertically)
		{
			Bitmap = TransformationUtil.Transform(Bitmap, dim, resize, flipHorizontally, flipVertically);
			return this;
		}

		public BmpFrame Update(BitmapSource bmp)
		{
			Bitmap = bmp;
			return this;
		}

		public DmdFrame ConvertToGray2()
		{
			return new DmdFrame(Dimensions, ImageUtil.ConvertToGray2(Bitmap));
		}

		public DmdFrame ConvertToGray4()
		{
			return new DmdFrame(Dimensions, ImageUtil.ConvertToGray4(Bitmap));
		}
		public DmdFrame ConvertToRgb24()
		{
			return new DmdFrame(Dimensions, ImageUtil.ConvertToRgb24(Bitmap));
		}

		public BmpFrame Transform(RenderGraph renderGraph, IFixedSizeDestination dest, IMultiSizeDestination multiDest)
		{
			if (dest == null && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return this;
			}
			var dim = dest?.FixedSize ?? Dimensions;
			return Transform(dim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
		}


		public object Clone()
		{
			return new BmpFrame(Bitmap, Dimensions);
		}
	}
}
