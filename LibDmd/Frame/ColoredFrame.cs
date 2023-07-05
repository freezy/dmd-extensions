using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace LibDmd
{
	public class ColoredFrame : DmdFrame, ICloneable, IEqualityComparer<ColoredFrame>
	{
		/// <summary>
		/// Color palette
		/// </summary>
		public Color[] Palette { get; private set; }

		/// <summary>
		/// Color Rotation descriptions.
		/// </summary>
		/// <remarks>
		/// Size: 8*3 bytes: 8 colour rotations available per frame, 1 byte for the first colour,
		/// 1 byte for the number of colours, 1 byte for the time interval between 2 rotations in 10ms
		/// </remarks>
		public byte[] Rotations { get;  private set; }

		/// <summary>
		/// If set, colors defined in <see cref="Rotations"/> are rotated.
		/// </summary>
		public bool RotateColors;

		public static bool operator == (ColoredFrame x, ColoredFrame y) => Equals(x, y);
		public static bool operator != (ColoredFrame x, ColoredFrame y) => !Equals(x, y);

		#region Constructors

		public ColoredFrame()
		{
		}

		public ColoredFrame(Dimensions dim, byte[] data, Color[] palette, byte[] rotations = null)
		{
			Dimensions = dim;
			Data = data;
			BitLength = palette.Length.GetBitLength();
			Palette = palette;
			Rotations = rotations;
			RotateColors = rotations != null && rotations.Length > 0;

			#if DEBUG
			AssertData();
			#endif
		}

		public ColoredFrame(DmdFrame frame, Color color)
		{
			Dimensions = frame.Dimensions;
			Data = frame.Data;
			BitLength = frame.BitLength;
			Palette = ColorUtil.GetPalette(new[] { Colors.Black, color }, frame.NumColors);
			RotateColors = false;

			#if DEBUG
			AssertData();
			#endif
		}

		public ColoredFrame(DmdFrame frame, params Color[] palette)
		{
			Dimensions = frame.Dimensions;
			Data = frame.Data;
			BitLength = frame.BitLength;
			Palette = palette;
			RotateColors = false;

			#if DEBUG
			AssertData();
			#endif
		}

		#endregion

		#region Updates

		public ColoredFrame Update(byte[] data, Color[] palette)
		{
			Data = data;
			BitLength = palette.Length.GetBitLength();
			Palette = palette;

			#if DEBUG
			AssertData();
			#endif
			return this;
		}

		private ColoredFrame Update(byte[] data)
		{
			Data = data;

			#if DEBUG
			AssertData();
			#endif
			return this;
		}

		public void Update(ColoredFrame frame)
		{
			Dimensions = frame.Dimensions;
			Data = new byte[frame.Data.Length];
			Buffer.BlockCopy(frame.Data, 0, Data, 0, frame.Data.Length);
			BitLength = frame.BitLength;
			Palette = frame.Palette;
			Rotations = frame.Rotations;
			RotateColors = frame.RotateColors;

			#if DEBUG
			AssertData();
			#endif
		}

		#endregion

		#region Conversions

		/// <summary>
		/// Creates a new grayscale frame with the only the grayscale data and no color conversion.
		/// </summary>
		/// <returns>New DMD frame</returns>
		public DmdFrame ConvertToGray()
		{
			return new DmdFrame(Dimensions, Data, BitLength);
		}

		public DmdFrame ConvertToGray(params byte[] mapping)
		{
			return new DmdFrame(Dimensions, FrameUtil.ConvertGrayToGray(Data, mapping), mapping.Length.GetBitLength());
		}

		/// <summary>
		/// Converts this colored frame to a bitmap frame.
		/// </summary>
		/// <returns>Converted bitmap frame</returns>
		public BmpFrame ConvertToBitmap() => new BmpFrame(ConvertToBitmapWithColors());

		/// <summary>
		/// Converts this colored frame to a RGB24 frame.
		/// </summary>
		/// <returns>Converted RGB24 frame</returns>
		public DmdFrame ConvertToRgb24() => new DmdFrame(Dimensions, ColorUtil.ColorizeRgb24(Dimensions, Data, Palette), 24);

		private BitmapSource ConvertToBitmapWithColors()
		{
			var rgb24 = ColorUtil.ColorizeRgb24(Dimensions, Data, Palette);
			return ImageUtil.ConvertFromRgb24(Dimensions, rgb24);
		}

		/// <summary>
		/// Converts the frame with a linear color palette, for resizing purpose.
		/// </summary>
		/// <returns>Bitmap</returns>
		private BitmapSource ConvertToBitmapWithoutColors()
		{
			switch (BitLength) {
				case 2: return ImageUtil.ConvertFromGray2(Dimensions, Data, 0, 1, 1);
				case 4: return ImageUtil.ConvertFromGray4(Dimensions, Data, 0, 1, 1);
				case 6: return ImageUtil.ConvertFromGray6(Dimensions, Data, 0, 1, 1);
				default:
					throw new ArgumentException("Cannot convert frame with bit length " + BitLength);
			}
		}

		/// <summary>
		/// Converts a bitmap with a linear color palette back to gray, after resizing.
		/// </summary>
		/// <param name="bmp"></param>
		/// <returns>Gray array</returns>
		private byte[] ConvertFromBitmapWithoutColors(BitmapSource bmp)
		{
			switch (BitLength) {
				case 2: return ImageUtil.ConvertToGray2(bmp, 0, 1, out _);
				case 4: return ImageUtil.ConvertToGray4(bmp);
				case 6: return ImageUtil.ConvertToGray6(bmp);
				default:
					throw new ArgumentException("Cannot convert frame with bit length " + BitLength);
			}
		}

		#endregion

		#region Transformations

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
		/// <returns>This frame, transformed.</returns>
		public ColoredFrame Transform(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			var targetDim = GetTargetDimensions(fixedDest, multiDest);

			// for dynamic or equal target dimensions, just flip
			if (targetDim == Dimensions.Dynamic || targetDim == Dimensions) {
				return Update(TransformationUtil.Flip(Dimensions, BytesPerPixel, Data, renderGraph.FlipHorizontally, renderGraph.FlipVertically));
			}

			// // if we need to scale down by factor 2, do it here more efficiently
			// if (Dimensions.IsDoubleSizeOf(targetDim) && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
			// 	return Update(targetDim, FrameUtil.ScaleDown(targetDim, Data));
			// }

			// otherwise, convert to grayscale bitmap, transform, convert back.
			var bmp = ConvertToBitmapWithoutColors();
			var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedData = ConvertFromBitmapWithoutColors(transformedBmp);

			Update(targetDim, transformedData);

			return this;
		}

		/// <summary>
		/// Up-scales the frame with the given algorithm, if the destination allows it.
		/// </summary>
		/// <param name="fixedDest">The fixed destination, null if dynamic. If fixed, DmdAllowHdScaling must be true, and the dimensions must be greater or equal the double of the frame size.</param>
		/// <param name="scalerMode">If and how to scale</param>
		/// <returns>Updated frame instance</returns>
		/// <exception cref="ArgumentException"></exception>
		public new ColoredFrame TransformHdScaling(IFixedSizeDestination fixedDest, ScalerMode scalerMode)
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

			// if source is already > 128x32, return
			if (Dimensions > new Dimensions(128, 32)) {
				return this;
			}

			// resize
			var data = scalerMode == ScalerMode.Doubler
				? FrameUtil.ScaleDouble(Dimensions, Data)
				: FrameUtil.Scale2X(Dimensions, Data);

			Update(Dimensions * 2, data);
			return this;
		}

		#endregion

		#region Equality

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) {
				return false;
			}

			if (ReferenceEquals(this, obj)) {
				return true;
			}

			if (obj.GetType() != this.GetType()) {
				return false;
			}

			return Equals(this, (ColoredFrame)obj);
		}

		public override int GetHashCode()
		{
			unchecked {
				var hashCode = Dimensions.GetHashCode();
				hashCode = (hashCode * 397) ^ BitLength;
				hashCode = (hashCode * 397) ^ (Rotations != null ? Rotations.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Palette != null ? Palette.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Data != null ? Data.GetHashCode() : 0);
				return hashCode;
			}
		}

		/// <summary>
		/// Checks whether frame data and bit length are both equal.
		/// </summary>
		/// <param name="a">First frame to compare</param>
		/// <param name="b">Second frame to compare</param>
		/// <returns>True if data and bit length are identical, false otherwise.</returns>
		private static bool Equals(ColoredFrame a, ColoredFrame b)
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
			return a.Dimensions == b.Dimensions
			       && a.RotateColors == b.RotateColors
			       && PaletteEquals(a.Palette, b.Palette)
			       && FrameUtil.CompareBuffersFast(a.Rotations, b.Rotations)
			       && FrameUtil.CompareBuffersFast(a.Data, b.Data);
		}

		private static bool PaletteEquals(IReadOnlyList<Color> a, IReadOnlyList<Color> b)
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
			if (a.Count != b.Count) {
				return false;
			}

			for (var i = 0; i < a.Count; i++) {
				if (a[i] != b[i]) {
					return false;
				}
			}
			return true;
		}

		bool IEqualityComparer<ColoredFrame>.Equals(ColoredFrame x, ColoredFrame y) => Equals(x, y);
		int IEqualityComparer<ColoredFrame>.GetHashCode(ColoredFrame obj) => obj.GetHashCode();

		#endregion

		#region Overrides

		public new object Clone() => new ColoredFrame(Dimensions, Data, Palette, Rotations);

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"Colored Frame {Dimensions}@{BitLength}, {Palette.Length} colors ({Data.Length} bytes):");
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
			} else {
				throw new ArgumentException("Cannot print frame with bit length " + BitLength);
			}

			sb.AppendLine("Palette: [");
			sb.Append(string.Join(", ", Palette.Select(c => c.ToString())));
			sb.AppendLine("]");

			return sb.ToString();
		}

		#endregion
	}
}
