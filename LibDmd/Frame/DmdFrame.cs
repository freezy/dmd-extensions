using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
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
		public int NumColors => (int)Math.Pow(2, BitLength);
		
		public static bool operator ==(DmdFrame x, DmdFrame y) => Equals(x, y);
		public static bool operator != (DmdFrame x, DmdFrame y) => Equals(x, y);

		public FrameFormat Format {
			get {
				switch (BitLength) {
					case 2: return FrameFormat.Gray2;
					case 4: return FrameFormat.Gray4;
					case 24: return FrameFormat.Rgb24;
					default: throw new InvalidOperationException($"Invalid bit length {BitLength}");
				}
			}
		}

		private bool IsGray => BitLength <= 8;
		private bool IsRgb24 => BitLength == 24;

		private int BytesPerPixel => BitLength <= 8 ? 1 : BitLength / 8;

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
			
			#if DEBUG
			AssertData();
			#endif
		}
		
		public DmdFrame(int width, int height, byte[] data, int bitLength)
		{
			Dimensions = new Dimensions(width, height);
			Data = data;
			BitLength = bitLength;
			
			#if DEBUG
			AssertData();
			#endif
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
			
			#if DEBUG
			AssertData();
			#endif
			return this;
		}

		public DmdFrame Update(Dimensions dim, byte[] data, int bitLength)
		{
			Dimensions = dim;
			Data = data;
			BitLength = bitLength;
			
			#if DEBUG
			AssertData();
			#endif
			return this;
		}

		public DmdFrame Update(Dimensions dim, byte[] data)
		{
			Dimensions = dim;
			Data = data;

			#if DEBUG
			AssertData();
			#endif
			return this;
		}

		public DmdFrame Resize(Dimensions dim, int bitLength)
		{
			Dimensions = dim;
			BitLength = bitLength;
			Data = new byte[Dimensions.Surface * BytesPerPixel];
			return this;
		}

		public DmdFrame Clear()
		{
			Data = new byte[Dimensions.Surface * BytesPerPixel];
			return this;
		}

		/// <summary>
		/// Converts this frame to gray2.
		/// </summary>
		/// <returns>This frame, converted to gray2.</returns>
		/// <exception cref="ArgumentException">If frame is already gray2</exception>
		public DmdFrame ConvertToGray2()
		{
			using (Profiler.Start("DmdFrame.ConvertToGray2")) {

				switch (BitLength) {
					case 2:
						throw new ArgumentException("Frame is already gray2.");
					case 4:
						return Update(FrameUtil.ConvertGrayToGray(Data, 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3), 2);
					case 6:
						return Update(FrameUtil.ConvertGrayToGray(Data, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,  0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3), 2);
					case 24:
						return Update(ImageUtil.ConvertToGray(Dimensions, Data, 4), 2);
					default:
						throw new ArgumentException("Invalid bit length.");
				}
			}
		}

		/// <summary>
		/// Converts this frame to gray4.
		/// </summary>
		/// <returns>This frame, converted to gray4.</returns>
		/// <exception cref="ArgumentException">If frame is already gray4</exception>
		public DmdFrame ConvertToGray4()
		{
			using (Profiler.Start("DmdFrame.ConvertToGray4")) {

				switch (BitLength) {
					case 2:
						return Update(FrameUtil.ConvertGrayToGray(Data, 0x0, 0x1, 0x4, 0xf), 4);
					case 4:
						throw new ArgumentException("Frame is already gray4.");
					case 6:
						return Update(FrameUtil.ConvertGrayToGray(Data, 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3, 0x4, 0x4, 0x4, 0x4, 0x5, 0x5, 0x5, 0x5, 0x6, 0x6, 0x6, 0x6, 0x7, 0x7, 0x7, 0x7, 0x8, 0x8, 0x8, 0x8, 0x9, 0x9, 0x9, 0x9, 0xa, 0xa, 0xa, 0xa, 0xb, 0xb, 0xb, 0xb, 0xc, 0xc, 0xc, 0xc, 0xd, 0xd, 0xd, 0xd, 0xe, 0xe, 0xe, 0xe, 0xf, 0xf, 0xf, 0xf), 4);
					case 24:
						return Update(ImageUtil.ConvertToGray(Dimensions, Data, 16), 4);
					default:
						throw new ArgumentException("Invalid bit length.");
				}
			}
		}

		/// <summary>
		/// Converts this gray frame to Bitmap.
		/// </summary>
		/// <param name="palette">Palette to use for conversion</param>
		/// <returns>New bitmap frame</returns>
		public BmpFrame ConvertToBmp(Color[] palette) => ConvertToRgb24(palette).ConvertToBmp();

		/// <summary>
		/// Converts this RGB frame to Bitmap.
		/// </summary>
		/// <returns>New bitmap frame</returns>
		/// <exception cref="ArgumentException">Thrown if executed on a grayscale frame</exception>
		public BmpFrame ConvertToBmp()
		{
			using (Profiler.Start("DmdFrame.ConvertToBmp")) {

				#if DEBUG
				if (!IsRgb24) {
					throw new ArgumentException("Cannot convert from gray to bitmap. Convert to RGB24 first.");
				}
				#endif
				return new BmpFrame(ImageUtil.ConvertFromRgb24(Dimensions, Data));
			}
		}

		/// <summary>
		/// Up- or downscales image, and flips if necessary.
		/// </summary>
		///
		/// <remarks>
		/// Images are actually never upscaled, if the source image is smaller than destination frame, it gets centered.
		/// Downscaling is done depending on the render graph's `Resize` setting.
		/// </remarks>
		/// <param name="renderGraph">Render graph reference to retrieve flipping and resizing config</param>
		/// <param name="fixedDest">If not null, the fixed destination we're transforming for.</param>
		/// <param name="multiDest">If not null, the multi-res destination we're transforming for.</param>
		/// <returns></returns>
		public DmdFrame TransformGray(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			using (Profiler.Start("DmdFrame.TransformGray")) {

				var bytesPerPixel = BytesPerPixel;
				var targetDim = GetTargetDimensions(fixedDest, multiDest);
				if (targetDim == Dimensions.Dynamic || Dimensions == targetDim) {
					// just flip
					Data = TransformationUtil.Flip(Dimensions, bytesPerPixel, Data, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
					return this;
				}

				// perf: if no flipping these cases can easily done on the byte array directly
				if (!renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {

					// copy whole block if only vertical padding
					if (Dimensions.Width == targetDim.Width && Dimensions.Height < targetDim.Height) {
						return Update(targetDim, CenterVertically(targetDim, Data, bytesPerPixel), BitLength);
					}

					// copy line by line if centering image
					if (Dimensions.FitsInto(targetDim)) {
						return Update(targetDim, CenterFrame(targetDim, Data, bytesPerPixel), BitLength);
					}
				}

				// otherwise, convert to bitmap, resize, convert back.
				var bmp = ImageUtil.ConvertFrom(BitLength, Dimensions, Data, 0, 1, 1);
				var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
				var transformedFrame = ImageUtil.ConvertTo(BitLength, transformedBmp);

				return Update(targetDim, transformedFrame, BitLength);
			}
		}

		public DmdFrame TransformRgb24(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			using (Profiler.Start("DmdFrame.TransformRgb24")) {

				// todo merge with TransformGray
				// do anything at all?
				if (fixedDest == null && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
					return this;
				}

				// just flip?
				if (fixedDest == null || fixedDest.FixedSize == Dimensions) {
					Data = TransformationUtil.Flip(Dimensions, 3, Data, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
					return this;
				}

				// resize
				var bmp = ImageUtil.ConvertFromRgb24(Dimensions, Data);
				var transformedBmp = TransformationUtil.Transform(bmp, fixedDest.FixedSize, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
				var transformedFrame = ImageUtil.ConvertToRgb24(transformedBmp);
				return new DmdFrame(fixedDest.FixedSize, transformedFrame, 24);
			}
		}

		/// <summary>
		/// Up-scales the frame with the given algorithm, if the destination allows it.
		/// </summary>
		/// <param name="fixedDest">The fixed destination, null if dynamic. If fixed, DmdAllowHdScaling must be true, and the dimensions must be greater or equal the double of the frame size.</param>
		/// <param name="scalerMode">If and how to scale</param>
		/// <returns>Updated frame instance</returns>
		/// <exception cref="ArgumentException"></exception>
		public DmdFrame TransformHdScaling(IFixedSizeDestination fixedDest, ScalerMode scalerMode)
		{
			using (Profiler.Start("DmdFrame.TransformHdScaling")) {

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

				// if source is already > 128x32, return
				if (Dimensions > new Dimensions(128, 32)) {
					return this;
				}

				return TransformHdScaling(scalerMode);
			}
		}

		public DmdFrame TransformHdScaling(ScalerMode scalerMode)
		{
			using (Profiler.Start("DmdFrame.TransformHdScaling")) {

				switch (scalerMode) {
					case ScalerMode.None:
						return this;

					case ScalerMode.Doubler:
						return IsRgb24
							? Update(Dimensions * 2, FrameUtil.ScaleDoubleRgb(Dimensions, Data))
							: Update(Dimensions * 2, FrameUtil.ScaleDouble(Dimensions, Data));

					case ScalerMode.Scale2x:
						return IsRgb24
							? Update(Dimensions * 2, FrameUtil.Scale2XRgb(Dimensions, Data))
							: Update(Dimensions * 2, FrameUtil.Scale2X(Dimensions, Data));

					default:
						throw new ArgumentOutOfRangeException(nameof(scalerMode), scalerMode, null);
				}
			}
		}
		
		/// <summary>
		/// Converts a grayscale frame to RGB24.
		/// </summary>
		///
		/// <param name="palette">Palette, must cover the bit length of the frame.</param>
		/// <returns>This updated instance.</returns>
		/// <exception cref="ArgumentException">If this frame already is RGB24, or palette doesn't match bit length.</exception>
		public DmdFrame ConvertToRgb24(Color[] palette)
		{
			using (Profiler.Start("DmdFrame.ConvertToRgb24")) {

				#if DEBUG
				if (!IsGray) {
					throw new ArgumentException($"Cannot convert a {BitLength}-bit frame to RGB24.");
				}

				if (palette.Length.GetBitLength() != BitLength) {
					throw new ArgumentException($"Cannot convert a {BitLength}-bit frame with {palette.Length} colors to RGB24.");
				}
				#endif

				Data = ColorUtil.ColorizeRgb24(Dimensions, Data, palette);
				BitLength = 24;

				return this;
			}
		}
		
		/// <summary>
		/// Checks whether frame data and bit length are both equal.
		/// </summary>
		/// <param name="a">First frame to compare</param>
		/// <param name="b">Second frame to compare</param>
		/// <returns>True if data and bit length are identical, false otherwise.</returns>
		private static bool Equals(DmdFrame a, DmdFrame b)
		{
			if (ReferenceEquals(null, a) && ReferenceEquals(null, b)) {
				return true;
			}
			if (ReferenceEquals(null, a)) {
				return false;
			}
			if (ReferenceEquals(null, b)) {
				return false;
			}
			if (ReferenceEquals(a, b)) {
				return true;
			}
			return a.BitLength == b.BitLength && a.Dimensions == b.Dimensions && FrameUtil.CompareBuffersFast(a.Data, b.Data);
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
			sb.AppendLine($"DMD Frame {Dimensions}@{BitLength} ({Data.Length} bytes):");
			if (BitLength <= 4) {
				for (var y = 0; y < Dimensions.Height; y++) {
					for (var x = 0; x < Dimensions.Width; x++) {
						sb.Append(Data[y * Dimensions.Width + x].ToString("X"));
					}
					sb.AppendLine();
				}
			} else if (BitLength <= 8) {
				for (var y = 0; y < Dimensions.Height; y++) {
					for (var x = 0; x < Dimensions.Width; x++) {
						sb.Append(Data[y * Dimensions.Width + x].ToString("X2") + " ");
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
		
#if DEBUG
		private void AssertData()
		{
			if (Dimensions.Surface * BytesPerPixel != Data.Length) {
				throw new ArgumentException($"Data length does not match dimensions and bit length: {Dimensions} * {BytesPerPixel} = {Dimensions.Surface} * {BytesPerPixel} != {Data.Length}.");
			}
		}
#endif
	}
}
