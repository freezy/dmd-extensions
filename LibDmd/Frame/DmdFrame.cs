﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Output;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace LibDmd.Frame
{
	/// <summary>
	/// A frame sent without a palette or color.
	/// </summary>RGB24 buffer must be divisible by 3
	public class DmdFrame : BaseFrame, ICloneable, IEqualityComparer<DmdFrame>
	{
		/// <summary>
		/// The frame data, from top left to bottom right.
		/// It's usually one byte per pixel, but three bytes for RGB24.
		/// </summary>
		public byte[] Data { get; protected set; }

		/// <summary>
		/// The bit length of each pixel.
		/// </summary>
		public int BitLength { get; protected set; }

		/// <summary>
		/// The number of colors resulting from <see cref="BitLength"/>.
		/// </summary>
		public int NumColors => (int)Math.Pow(2, BitLength);

		/// <summary>
		/// Converts <see cref="Data"/> to bit planes, which is an array for each color,
		/// where each each pixel is either 0 or 1, 8 pixels per byte.
		/// </summary>
		public byte[][] BitPlanes => FrameUtil.Split(Dimensions, BitLength, Data);

		/// <summary>
		/// The length of every bit plane.
		/// </summary>
		public int BitPlaneLength => Dimensions.Surface / 8;

		public static bool operator == (DmdFrame x, DmdFrame y) => Equals(x, y);
		public static bool operator != (DmdFrame x, DmdFrame y) => !Equals(x, y);

		private static readonly ObjectPool Pool = new ObjectPool();

		public FrameFormat Format {
			get {
				switch (BitLength) {
					case 2: return FrameFormat.Gray2;
					case 4: return FrameFormat.Gray4;
					case 16: return FrameFormat.Rgb565;
					case 24: return FrameFormat.Rgb24;
					default: throw new InvalidOperationException($"Invalid bit length {BitLength}");
				}
			}
		}

		private bool IsGray => BitLength <= 8;
		private bool IsRgb24 => BitLength == 24;
		private bool IsRgb565 => BitLength == 16;

		public int BytesPerPixel => BitLength <= 8 ? 1 : BitLength / 8;

		#region Constructors

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

		#endregion

		#region Update

		public DmdFrame Update(DmdFrame frame)
		{
			Dimensions = frame.Dimensions;
			Data = frame.Data;
			BitLength = frame.BitLength;

			#if DEBUG
			AssertData();
			#endif
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

		#endregion

		#region Transfers

		public void CopyPlanesTo(byte[] dest, int offset)
		{
			var planes = BitPlanes;
			foreach (var plane in planes) {
				Buffer.BlockCopy(plane, 0, dest, offset, plane.Length);
				offset += plane.Length;
			}
		}

		public void CopyDataTo(byte[] dest, int offset)
		{
			Buffer.BlockCopy(Data, 0, dest, offset, Data.Length);
		}

		#endregion

		#region Utilities

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
		protected void AssertData()
		{
			if (Dimensions.Surface * BytesPerPixel != Data.Length) {
				throw new ArgumentException($"Data length does not match dimensions and bit length: {Dimensions} * {BytesPerPixel} = {Dimensions.Surface} * {BytesPerPixel} != {Data.Length}.");
			}
		}
#endif

		#endregion

		#region Conversions

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
					case 16:
						return Update(ImageUtil.ConvertRgb565ToGray(Dimensions, Data, 4), 2);
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
					case 3:
						return Update(FrameUtil.ConvertGrayToGray(Data, 0x0, 0x1, 0x4, 0xf), 4);
					case 4:
						throw new ArgumentException("Frame is already gray4.");
					case 6:
						return Update(FrameUtil.ConvertGrayToGray(Data, 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3, 0x4, 0x4, 0x4, 0x4, 0x5, 0x5, 0x5, 0x5, 0x6, 0x6, 0x6, 0x6, 0x7, 0x7, 0x7, 0x7, 0x8, 0x8, 0x8, 0x8, 0x9, 0x9, 0x9, 0x9, 0xa, 0xa, 0xa, 0xa, 0xb, 0xb, 0xb, 0xb, 0xc, 0xc, 0xc, 0xc, 0xd, 0xd, 0xd, 0xd, 0xe, 0xe, 0xe, 0xe, 0xf, 0xf, 0xf, 0xf), 4);
					case 16:
						return Update(ImageUtil.ConvertRgb565ToGray(Dimensions, Data, 16), 4);
					case 24:
						return Update(ImageUtil.ConvertToGray(Dimensions, Data, 16), 4);
					default:
						throw new ArgumentException($"Invalid bit length ({BitLength}).");
				}
			}
		}

		/// <summary>
		/// Converts this gray frame to Bitmap.
		/// </summary>
		/// <param name="palette">Palette to use for conversion</param>
		/// <returns>New bitmap frame</returns>
		public BmpFrame ConvertGrayToBmp(Color[] palette) => ConvertGrayToRgb24(palette).ConvertRgbToBmp();

		/// <summary>
		/// Converts this RGB frame to Bitmap.
		/// </summary>
		/// <returns>New bitmap frame</returns>
		/// <exception cref="ArgumentException">Thrown if executed on a grayscale frame</exception>
		public BmpFrame ConvertRgbToBmp()
		{
			using (Profiler.Start("DmdFrame.ConvertRgbToBmp")) {

				#if DEBUG
				if (!IsRgb24 && !IsRgb565) {
					throw new ArgumentException("Cannot convert from gray to bitmap. Convert to RGB24 or RGB565 first.");
				}
				#endif
				return IsRgb24
					? new BmpFrame(ImageUtil.ConvertFromRgb24(Dimensions, Data))
					: new BmpFrame(ImageUtil.ConvertFromRgb565(Dimensions, Data));
			}
		}

		/// <summary>
		/// Converts a grayscale frame to RGB24.
		/// </summary>
		///
		/// <param name="palette">Palette, must cover the bit length of the frame.</param>
		/// <returns>This updated instance.</returns>
		/// <exception cref="ArgumentException">If this frame already is RGB24, or palette doesn't match bit length.</exception>
		public DmdFrame ConvertGrayToRgb24(Color[] palette) => ConvertGrayToRgb(palette, 3);

		/// <summary>
		/// Converts a grayscale frame to RGB565.
		/// </summary>
		///
		/// <param name="palette">Palette, must cover the bit length of the frame.</param>
		/// <returns>This updated instance.</returns>
		/// <exception cref="ArgumentException">If this frame already is RGB24, or palette doesn't match bit length.</exception>
		public DmdFrame ConvertGrayToRgb565(Color[] palette) => ConvertGrayToRgb(palette, 2);

		/// <summary>
		/// Converts a grayscale frame to RGB24.
		/// </summary>
		///
		/// <param name="palette">Palette, must cover the bit length of the frame.</param>
		/// <param name="bytesPerPixel">Number of RGB bytes per pixel. 2 or 3.</param>
		/// <returns>This updated instance.</returns>
		/// <exception cref="ArgumentException">If this frame already is RGB24, or palette doesn't match bit length.</exception>
		private DmdFrame ConvertGrayToRgb(Color[] palette, int bytesPerPixel)
		{
			using (Profiler.Start("DmdFrame.ConvertToRgb24")) {

#if DEBUG
				if (!IsGray) {
					throw new ArgumentException($"Cannot convert a {BitLength}-bit frame to RGB24.");
				}

				if (BitLength != bytesPerPixel && palette.Length.GetBitLength() != BitLength) {
					throw new ArgumentException($"Cannot convert a {BitLength}-bit frame with {palette.Length} colors to RGB24.");
				}
#endif

				Data = ColorUtil.ColorizeRgb(Dimensions, Data, palette, bytesPerPixel);
				BitLength = 24;

				#if DEBUG
				AssertData();
				#endif

				return this;
			}
		}

		public DmdFrame ConvertRgb565ToRgb24()
		{
			using (Profiler.Start("DmdFrame.ConvertRgb565ToRgb24")) {

#if DEBUG
				if (BitLength != 16) {
					throw new ArgumentException("Cannot convert non-RGB565 frame to RGB24.");
				}
#endif
				Data = ColorUtil.ConvertRgb565ToRgb24(Dimensions, Data);
				BitLength = 24;

				#if DEBUG
				AssertData();
				#endif

				return this;
			}
		}

		public DmdFrame ConvertRgb24ToRgb565()
		{
			using (Profiler.Start("DmdFrame.ConvertRgb24ToRgb565")) {

#if DEBUG
				if (BitLength != 24) {
					throw new ArgumentException("Cannot convert non-RGB24 frame to RGB565.");
				}
#endif
				Data = ColorUtil.ConvertRgb24ToRgb565(Dimensions, Data);
				BitLength = 16;

				#if DEBUG
				AssertData();
				#endif

				return this;
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

		public DmdFrame TransformRgb565(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			using (Profiler.Start("DmdFrame.TransformRgb24")) {

				// do anything at all?
				if (fixedDest == null && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
					return this;
				}

				// just flip?
				if (fixedDest == null || fixedDest.FixedSize == Dimensions) {
					Data = TransformationUtil.Flip(Dimensions, 2, Data, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
					return this;
				}

				// resize
				var bmp = ImageUtil.ConvertFromRgb565(Dimensions, Data);
				var transformedBmp = TransformationUtil.Transform(bmp, fixedDest.FixedSize, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
				var transformedFrame = ImageUtil.ConvertToRgb565(transformedBmp);
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
				if (Dimensions > Dimensions.Standard) {
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
						switch (BitLength) {
							case 24: return Update(Dimensions * 2, FrameUtil.ScaleDouble(Dimensions, Data, 3));
							case 16: return Update(Dimensions * 2, FrameUtil.ScaleDouble(Dimensions, Data, 2));
							default: return Update(Dimensions * 2, FrameUtil.ScaleDouble(Dimensions, Data, 1));
						}

					case ScalerMode.Scale2x:
						switch (BitLength) {
							case 24: return Update(Dimensions * 2, FrameUtil.Scale2X(Dimensions, Data, 3));
							case 16: return Update(Dimensions * 2, FrameUtil.Scale2X(Dimensions, Data, 2));
							default: return Update(Dimensions * 2, FrameUtil.Scale2X(Dimensions, Data, 1));
						}

					default:
						throw new ArgumentOutOfRangeException(nameof(scalerMode), scalerMode, null);
				}
			}
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

			return Equals(this, (DmdFrame)obj);
		}

		public override int GetHashCode()
		{
			unchecked {
				var hashCode = Dimensions.GetHashCode();
				hashCode = (hashCode * 397) ^ BitLength;
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

		bool IEqualityComparer<DmdFrame>.Equals(DmdFrame x, DmdFrame y) => Equals(x, y);
		int IEqualityComparer<DmdFrame>.GetHashCode(DmdFrame obj) => obj.GetHashCode();

		#endregion

		#region Overrides

		/// <summary>
		/// Flat-clones the frame (i.e. the data is still the same, but now you
		/// can replace it without affecting other references of the frame).
		/// </summary>
		/// <returns></returns>
		public object Clone() => new DmdFrame(Dimensions, Data, BitLength);

		public DmdFrame CloneFrame() => new DmdFrame(Dimensions, Data, BitLength);

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
			} else if (BitLength == 16) {
				for (var y = 0; y < Dimensions.Height; y++) {
					for (var x = 0; x < Dimensions.Width; x++) {
						sb.Append(Data[y * Dimensions.Width * 2 + x * 2].ToString("X2"));
						sb.Append(Data[y * Dimensions.Width * 2 + x * 2 + 1].ToString("X2"));
						sb.Append(" ");
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

		#endregion

		#region Pool

		private class ObjectPool
		{
			private readonly ConcurrentBag<DmdFrame> _objects = new ConcurrentBag<DmdFrame>();

			public DmdFrame Get() => _objects.TryTake(out DmdFrame item) ? item : new DmdFrame();

			public void Return(DmdFrame item) => _objects.Add(item);
		}

		public static DmdFrame GetFromPool() => Pool.Get();
		public void ReturnToPool() => Pool.Return(this);

		#endregion

		#region Debug

		public void Dump(string path, string prefix = null)
		{
			using (StreamWriter sw = File.Exists(path) ? File.AppendText(path) : File.CreateText(path)) {
				if (prefix != null) {
					sw.WriteLine(prefix);
				}
				sw.WriteLine(ToString());
			}
		}

		#endregion
	}
}
