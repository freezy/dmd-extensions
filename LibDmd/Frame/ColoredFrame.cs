using System;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd
{
	public class ColoredFrame : BaseFrame, ICloneable
	{
		/// <summary>
		/// Frame data, split into bit planes
		/// </summary>
		public byte[][] Planes { get; private set; }

		/// <summary>
		/// Color palette
		/// </summary>
		public Color[] Palette { get; private set; }

		/// <summary>
		/// Palette index from animation or -1 if not set.
		/// </summary>
		public int PaletteIndex { get; }

		/// <summary>
		/// Colour Rotation descriptions.
		/// </summary>
		/// <remarks>
		/// Size: 8*3 bytes: 8 colour rotations available per frame, 1 byte for the first colour,
		/// 1 byte for the number of colours, 1 byte for the time interval between 2 rotations in 10ms
		/// </remarks>
		public byte[] Rotations { get; }

		/// <summary>
		/// If set, colors defined in <see cref="Rotations" are rotated./>
		/// </summary>
		public readonly bool RotateColors;

		private int BitLength => Planes.Length;

		private byte[] Data => FrameUtil.Join(Dimensions, Planes);
		
		public ColoredFrame()
		{
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette, int paletteIndex = -1, byte[] rotations = null, bool rotateColors = false)
		{
			Dimensions = dim;
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
			Rotations = rotations;
			RotateColors = rotateColors;

			#if DEBUG
			if (planes.Length != palette.Length.GetBitLength()) {
				throw new ArgumentException($"Number of planes must match palette size ({planes.Length} != {palette.Length.GetBitLength()})");
			}
			#endif
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette, byte[] rotations)
			: this(dim, planes, palette, -1, rotations) { }

		public ColoredFrame(DmdFrame frame, Color color)
		{
			Dimensions = frame.Dimensions;
			Planes = FrameUtil.Split(Dimensions, frame.BitLength, frame.Data);
			Palette = ColorUtil.GetPalette(new[] { Colors.Black, color }, frame.NumColors);
			RotateColors = false;
		}

		public ColoredFrame(DmdFrame frame, params Color[] palette)
		{
			Dimensions = frame.Dimensions;
			Planes = FrameUtil.Split(frame.Dimensions, frame.BitLength, frame.Data);
			Palette = palette;
			RotateColors = false;
		}

		public object Clone() => new ColoredFrame(Dimensions, Planes, Palette, PaletteIndex);
		
		/// <summary>
		/// Creates a new grayscale frame with the only the grayscale data and no color conversion.
		/// </summary>
		/// <returns>New DMD frame</returns>
		public DmdFrame ConvertToGray()
		{
			return new DmdFrame(Dimensions, FrameUtil.Join(Dimensions, Planes), Planes.Length);
		}
		
		public DmdFrame ConvertToGray(params byte[] mapping)
		{
			var data = FrameUtil.Join(Dimensions, Planes);
			return new DmdFrame(Dimensions, FrameUtil.ConvertGrayToGray(data, mapping), mapping.Length.GetBitLength());
		}

		public ColoredFrame Update(byte[][] planes, Color[] palette)
		{
			Planes = planes;
			Palette = palette;

			#if DEBUG
			if (planes.Length != Palette.Length.GetBitLength()) {
				throw new ArgumentException("Number of planes must match palette size");
			}
			#endif
			return this;
		}

		private ColoredFrame Update(byte[][] planes)
		{
			Planes = planes;

			#if DEBUG
			if (planes.Length != Palette.Length.GetBitLength()) {
				throw new ArgumentException("Number of planes must match palette size");
			}
			#endif
			return this;
		}

		private ColoredFrame Update(Dimensions dim, byte[][] planes)
		{
			Dimensions = dim;
			Planes = planes;

			#if DEBUG
			if (planes.Length != Palette.Length.GetBitLength()) {
				throw new ArgumentException("Number of planes must match palette size");
			}
			#endif
			return this;
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
		public DmdFrame ConvertToRgb24() => new DmdFrame(Dimensions, ColorUtil.ColorizeRgb24(Dimensions, FrameUtil.Join(Dimensions, Planes), Palette), 24);

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
				return Update(TransformationUtil.Flip(Dimensions, Planes, renderGraph.FlipHorizontally, renderGraph.FlipVertically));
			}

			// if we need to scale down by factor 2, do it here more efficiently
			if (Dimensions.IsDoubleSizeOf(targetDim) && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				return Update(targetDim, FrameUtil.ScaleDown(targetDim, Planes));
			}

			// otherwise, convert to grayscale bitmap, transform, convert back.
			var bmp = ConvertToBitmapWithoutColors();
			var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedData = ConvertFromBitmapWithoutColors(transformedBmp);

			return Update(targetDim, FrameUtil.Split(targetDim, BitLength, transformedData));
		}

		/// <summary>
		/// Up-scales the frame with the given algorithm, if the destination allows it.
		/// </summary>
		/// <param name="fixedDest">The fixed destination, null if dynamic. If fixed, DmdAllowHdScaling must be true, and the dimensions must be greater or equal the double of the frame size.</param>
		/// <param name="scalerMode">If and how to scale</param>
		/// <returns>Updated frame instance</returns>
		/// <exception cref="ArgumentException"></exception>
		public ColoredFrame TransformHdScaling(IFixedSizeDestination fixedDest, ScalerMode scalerMode)
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

			// resize
			var data = scalerMode == ScalerMode.Doubler
				? FrameUtil.ScaleDouble(Dimensions, FrameUtil.Join(Dimensions, Planes))
				: FrameUtil.Scale2X(Dimensions, FrameUtil.Join(Dimensions, Planes));

			return Update(Dimensions * 2, FrameUtil.Split(Dimensions * 2, BitLength, data));
		}

		public override string ToString()
		{
			var bitLength = Planes.Length;
			var data = FrameUtil.Join(Dimensions, Planes);
			var sb = new StringBuilder();
			sb.AppendLine($"Colored Frame {Dimensions}@{bitLength}, {Palette.Length} colors ({data.Length} bytes):");
			if (bitLength <= 4) {
				for (var y = 0; y < Dimensions.Height; y++) {
					for (var x = 0; x < Dimensions.Width; x++) {
						sb.Append(data[y * Dimensions.Width + x].ToString("X"));
					}
					sb.AppendLine();
				}

			} else if (bitLength <= 8) {
				for (var y = 0; y < Dimensions.Height; y++) {
					for (var x = 0; x < Dimensions.Width; x++) {
						sb.Append(Data[y * Dimensions.Width + x].ToString("X2") + " ");
					}
					sb.AppendLine();
				}
			} else {
				throw new ArgumentException("Cannot print frame with bit length " + bitLength);
			}

			sb.AppendLine("Palette: [");
			sb.Append(string.Join(", ", Palette.Select(c => c.ToString())));
			sb.AppendLine("]");

			return sb.ToString();
		}

		private BitmapSource ConvertToBitmapWithColors()
		{
			var rgb24 = ColorUtil.ColorizeRgb24(Dimensions, FrameUtil.Join(Dimensions, Planes), Palette);
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
	}
}
