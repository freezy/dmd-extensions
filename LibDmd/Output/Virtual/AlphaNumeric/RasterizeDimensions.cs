using System;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	/// <summary>
	/// A class holding dimensions of the segments depending on the display size.
	/// </summary>
	public class RasterizeDimensions : IEquatable<RasterizeDimensions>
	{
		/// <summary>
		/// Width in pixels of the display. This is calculated based on the canvas height.
		/// </summary>
		public int CanvasWidth { get; }

		/// <summary>
		/// Height in pixels of the display
		/// </summary>
		public int CanvasHeight { get; }

		/// <summary>
		/// Scaled width of the segment SVG
		/// </summary>
		public float SvgWidth { get; }

		/// <summary>
		/// Scaled height of the SVG
		/// </summary>
		public float SvgHeight { get; }

		/// <summary>
		/// Scale factor of the SVG to fit all characters into the display, including padding.
		/// </summary>
		public float SvgScale { get; }

		/// <summary>
		/// Dimensions of the surface, i.e. the rasterized SVG including padding
		/// </summary>
		public SKImageInfo SvgInfo { get; }

		/// <summary>
		/// The padding around the display in pixels
		/// </summary>
		public int OuterPadding { get; }

		/// <summary>
		/// The padding around each SVG in pixels
		/// </summary>
		public int SegmentPadding { get; }

		/// <summary>
		/// The padding between each line in pixels
		/// </summary>
		public int LinePadding { get; }

		/// <summary>
		/// Where to translate the canvas to center the segment
		/// </summary>
		public SKPoint Translate;

		/// <summary>
		/// The scale matrix
		/// </summary>
		public SKMatrix SvgMatrix;

		/// <summary>
		/// Line padding as percentage of the line height
		/// </summary>
		public float LinePaddingPercentage { get; set; } = 0.04f;

		/// <summary>
		/// Outer padding as percentage of the display height
		/// </summary>
		public float OuterPaddingPercentage { get; set; } = 0.04f;

		/// <summary>
		/// Segment padding as percentage of the segment's diagonal
		/// </summary>
		public float SegmentPaddingPercentage { get; set; } = 0.3f;

		public RasterizeDimensions(SKRect svgSize, int canvasHeight, int numChars, int numLines, float skewAngle)
		{
			OuterPadding = (int)Math.Round(OuterPaddingPercentage * canvasHeight);
			SvgHeight = canvasHeight - 2 * OuterPadding;
			SvgScale = SvgHeight / svgSize.Height;
			SvgWidth = svgSize.Width * SvgScale;
			LinePadding = (int)Math.Round(SvgHeight * LinePaddingPercentage);
			SvgMatrix = SKMatrix.CreateScale(SvgScale, SvgScale);
			var svgSkewedWidth = SkewedWidth(SvgWidth, SvgHeight, skewAngle);
			SegmentPadding = (int)Math.Round(Math.Sqrt(SvgWidth * SvgWidth + SvgHeight * SvgHeight) * SegmentPaddingPercentage);
			SvgInfo = new SKImageInfo((int)(svgSkewedWidth + 2 * SegmentPadding), (int)(SvgHeight + 2 * SegmentPadding));
			var skewedWidth = SkewedWidth(SvgWidth, SvgHeight, skewAngle);
			CanvasWidth = (int)Math.Round(2 * OuterPadding + (numChars - 1) * SvgWidth + skewedWidth);
			CanvasHeight = (int)Math.Round(OuterPadding * 2 + numLines * SvgHeight + (numLines - 1) * LinePadding);
			Translate = new SKPoint(svgSkewedWidth - SvgWidth + SegmentPadding, SegmentPadding);
		}

		private static float SkewedWidth(float width, float height, float angle)
		{
			var skew = (float)Math.Tan(Math.PI * angle / 180);
			return width + Math.Abs(skew * height);
		}

		public bool Equals(RasterizeDimensions other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return SvgHeight.Equals(other.SvgHeight);
		}
	}
}
