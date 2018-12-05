using System;
using SkiaSharp;

namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Describes all parameters of a display.
	/// </summary>
	public class DisplaySetting
	{
		/// <summary>
		/// Display number.
		/// </summary>
		/// 
		/// <summary>
		/// Since segments are cached for every display separately, we need to
		/// identify the display.
		/// </summary>
		public int Display { get; set; }

		/// <summary>
		/// Segment type, e.g. if it's a 7-, 9- or 14-segment display (plus the dot)
		/// </summary>
		public SegmentType SegmentType { get; set; }

		/// <summary>
		/// Dimensions of the rasterized segments
		/// </summary>
		public RasterizeDimensions Dim { get; private set; }

		public RasterizeStyleDefinition StyleDefinition { get; set; } = new RasterizeStyleDefinition {
			SkewAngle = -12,
			Background = new RasterizeLayerStyleDefinition { Color = new SKColor(0xff, 0xff, 0xff, 0x20), Blur = new SKPoint(7, 7), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = false },
			OuterGlow = new RasterizeLayerStyleDefinition { Color = new SKColor(0xb6, 0x58, 0x29, 0x40), Blur = new SKPoint(50, 50), Dilate = new SKPoint(90, 40), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = true },
			InnerGlow = new RasterizeLayerStyleDefinition { Color = new SKColor(0xdd, 0x6a, 0x03, 0xa0), Blur = new SKPoint(15, 13), Dilate = new SKPoint(15, 10), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = true },
			Foreground = new RasterizeLayerStyleDefinition { Color = new SKColor(0xfb, 0xe6, 0xcb, 0xff), Blur = new SKPoint(2, 2), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = false },
		};
		public RasterizeStyle Style { get; set; }

		/// <summary>
		/// Number of characters in the display
		/// </summary>
		public int NumChars { get; set; }

		/// <summary>
		/// Number of lines in the display
		/// </summary>
		public int NumLines { get; set; }

		private readonly AlphaNumericResources _res = AlphaNumericResources.GetInstance();

		public DisplaySetting()
		{
		}

		public DisplaySetting(int display, SegmentType segmentType, RasterizeStyleDefinition styleDef, int numChars, int numLines, int canvasWidth, int canvasHeight)
		{
			Display = display;
			SegmentType = segmentType;
			NumChars = numChars;
			NumLines = numLines;
			StyleDefinition = styleDef;
			SetDimensions(canvasWidth, canvasHeight);
		}

		/// <summary>
		/// Recalculates the segment dimensions based on a canvas size
		/// </summary>
		/// <param name="canvasWidth">Width the of the canvas in pixels</param>
		/// <param name="canvasHeight">Height of the canvas in pixels</param>
		public void SetDimensions(int canvasWidth, int canvasHeight)
		{
			Dim = new RasterizeDimensions(_res.GetSvgSize(SegmentType), canvasWidth, canvasHeight, NumChars, NumLines, StyleDefinition.SkewAngle);
			Style = StyleDefinition.Scale(Dim.SvgScale);
		}

		public void ApplyStyle(RasterizeStyleDefinition styleDef)
		{
			StyleDefinition = styleDef;
			Style = StyleDefinition.Scale(Dim.SvgScale);
		}

		public void ApplyLayerStyle(RasterizeLayer layer, RasterizeLayerStyleDefinition layerStyleDef)
		{
			switch (layer)
			{
				case RasterizeLayer.OuterGlow:
					StyleDefinition.OuterGlow = layerStyleDef;
					break;
				case RasterizeLayer.InnerGlow:
					StyleDefinition.InnerGlow = layerStyleDef;
					break;
				case RasterizeLayer.Foreground:
					StyleDefinition.Foreground = layerStyleDef;
					break;
				case RasterizeLayer.Background:
					StyleDefinition.Background = layerStyleDef;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
			}
			Style = StyleDefinition.Scale(Dim.SvgScale);
		}
	}
}