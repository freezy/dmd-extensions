using System;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Frame
{
	/// <summary>
	/// A frame sent without a palette or color.
	/// </summary>RGB24 buffer must be divisible by 3
	public class DmdFrame : BaseFrame, ICloneable
	{
		public byte[] Data { get; private set; }
		public int BitLength;
		
		private int BytesPerPixel => BitLength < 8 ? 1 : BitLength / 8;

		public DmdFrame()
		{
		}

		public DmdFrame(int width, int height, int bitLength) : this(new Dimensions(width, height), bitLength)
		{
		}

		public DmdFrame(Dimensions dim, int bitLength)
		{
			Dimensions = dim;
			BitLength = bitLength;
			Data = new byte[dim.Surface * BytesPerPixel];
		}

		public DmdFrame(Dimensions dim, byte[] data, int bitLength)
		{
			Dimensions = dim;
			Data = data;
			BitLength = bitLength;
			
			// todo remove when coverage done and all tested
			AssertData();
		}
		
		public DmdFrame(int width, int height, byte[] data, int bitLength)
		{
			Dimensions = new Dimensions(width, height);
			Data = data;
			BitLength = bitLength;
			
			// todo remove when coverage done and all tested
			AssertData();
		}

		public DmdFrame Update(DmdFrame frame)
		{
			Dimensions = frame.Dimensions;
			Data = frame.Data;
			BitLength = frame.BitLength;
			
			return this;
		}

		public DmdFrame Update(byte[] data, int bitLength)
		{
			Data = data;
			BitLength = bitLength;
			
			// todo remove when coverage done and all tested
			AssertData();
			return this;
		}

		public DmdFrame Update(Dimensions dim, byte[] data, int bitLength)
		{
			Dimensions = dim;
			Data = data;
			BitLength = bitLength;
			
			// todo remove when coverage done and all tested
			AssertData();
			return this;
		}

		public DmdFrame ConvertToRgb24(Color[] palette)
		{
			Data = ColorUtil.ColorizeFrame(Dimensions, Data, palette).Data;
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
			return Transform(2, 1, renderGraph, fixedDest, multiDest);
		}

		public DmdFrame TransformGray4(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			return Transform(4, 1, renderGraph, fixedDest, multiDest);
		}

		public DmdFrame TransformRgb24(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			return Transform(24, 3, renderGraph, fixedDest, multiDest);
		}
		
		public DmdFrame TransformRgb24(IFixedSizeDestination fixedDest, ResizeMode resize, bool flipHorizontally, bool flipVertically)
		{
			// do anything at all?
			if (fixedDest == null && !flipHorizontally && !flipVertically) {
				return this;
			}

			// just flip?
			if (fixedDest == null || fixedDest.FixedSize == Dimensions) {
				Data = TransformationUtil.Flip(Dimensions, 3, Data, flipHorizontally, flipVertically);
				return this;
			}
			
			// resize
			var bmp = ImageUtil.ConvertFromRgb24(Dimensions, Data);
			var transformedBmp = TransformationUtil.Transform(bmp, fixedDest.FixedSize, resize, flipHorizontally, flipVertically);
			var transformedFrame = ImageUtil.ConvertToRgb24(transformedBmp);
			return new DmdFrame(fixedDest.FixedSize, transformedFrame, 24);
		}

		public DmdFrame TransformHdScaling(IFixedSizeDestination fixedDest, ScalerMode scalerMode)
		{
			if (BitLength > 8) {
				throw new ArgumentException("Cannot double-scale a frame with more than 8 bits per pixel.");
			}

			// skip if disabled
			if (scalerMode == ScalerMode.None) {
				return this;
			}
			
			// if destination doesn't allow scaling (pup), return
			if (fixedDest != null && !fixedDest.DmdAllowHdScaling) {
				return this;
			}

			// if double of frame size doesn't fit into destination, return
			if (fixedDest != null && !(Dimensions * 2).FitInto(fixedDest.FixedSize)) {
				return this;
			}
			
			// resize
			Data = scalerMode == ScalerMode.Doubler 
				? FrameUtil.ScaleDouble(Dimensions, Data) 
				: FrameUtil.Scale2X(Dimensions, Data);
			Dimensions *= 2;

			return this;
		}
		
		public DmdFrame ColorizeGrayRgb24(Color[] palette)
		{
			Data = ColorUtil.Colorize(Dimensions, Data, palette);
			BitLength = 24;
			
			return this;
		}

		/// <summary>
		/// Up- or downscales image, and flips if necessary.
		/// </summary>
		///
		/// <remarks>
		/// Images are actually never upscaled, if the source image is smaller than destination frame, it gets centered.
		/// Downscaling is done depending on the render graph's `Resize` setting.
		/// </remarks>
		/// <param name="bitLen">Number of bit planes</param>
		/// <param name="bytesPerPixel">Pixel size in bytes</param>
		/// <param name="renderGraph">Render graph reference to retrieve flipping and resizing config</param>
		/// <param name="fixedDest">If not null, the fixed destination we're transforming for.</param>
		/// <param name="multiDest">If not null, the multi-res destination we're transforming for.</param>
		/// <returns></returns>
		private DmdFrame Transform(int bitLen, int bytesPerPixel, RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			var targetDim = GetTargetDimensions(fixedDest, multiDest);
			if (targetDim == Dimensions.Dynamic || Dimensions == targetDim) {
				// no resizing, we're done here.
				return Flip(bytesPerPixel, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			}

			// perf: if no flipping these cases can easily done on the byte array directly
			if (!renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {

				// copy whole block if only vertical padding
				if (Dimensions.Width == targetDim.Width && Dimensions.Height < targetDim.Height) {
					return Update(targetDim, CenterVertically(targetDim, Data, bytesPerPixel), bitLen);
				}

				// copy line by line if centering image
				if (Dimensions.FitInto(targetDim)) {
					return Update(targetDim, CenterFrame(targetDim, Data, bytesPerPixel), bitLen);
				}
			}

			// otherwise, convert to bitmap, resize, convert back.
			var bmp = ImageUtil.ConvertFrom(bitLen, Dimensions, Data, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedFrame = ImageUtil.ConvertTo(bitLen, transformedBmp);

			return Update(targetDim, transformedFrame, bitLen);
		}

		private DmdFrame Flip(int bytesPerPixel, bool flipHorizontally, bool flipVertically)
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
			return new DmdFrame(Dimensions, Data, BitLength);
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"DMDFrame {Dimensions}@{BitLength} ({Data.Length} bytes):");
			if (BitLength <= 8) {
				for (var y = 0; y < Dimensions.Height; y++) {
					for (var x = 0; x < Dimensions.Width; x++) {
						sb.Append(Data[y * Dimensions.Width + x].ToString("X"));
					}
					sb.AppendLine();
				}
			} else if (BitLength == 24) {
				for (var p = 0; p < 3; p++) {
					sb.AppendLine($"::{PlaneName(p)}::");
					for (var y = 0; y < Dimensions.Height; y++) {
						for (var x = 0; x < Dimensions.Width; x++) {
							sb.Append(Data[y * Dimensions.Width * 3 + x * 3 + p].ToString("X2") + " ");
						}
						sb.AppendLine();
					}
				}
			} else {
				throw new ArgumentException("Cannot print frame with bit length " + BitLength);
			}
			return sb.ToString();
		}

		private static string PlaneName(int p)
		{
			switch (p) {
				case 0: return "RED";
				case 1: return "GREEN";
				case 2: return "BLUE";
				default: return "PLANE " + p;
			}
		}
		
		
		private void AssertData()
		{
			// todo remove when coverage done and all tested
			if (Dimensions.Surface * BytesPerPixel != Data.Length) {
				throw new ArgumentException($"Data length does not match dimensions and bit length: {Dimensions} * {BytesPerPixel} = {Dimensions.Surface} * {BytesPerPixel} != {Data.Length}.");
			}
		}
	}
}
