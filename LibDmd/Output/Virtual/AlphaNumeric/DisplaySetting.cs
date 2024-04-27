using System;

namespace LibDmd.Output.Virtual.AlphaNumeric
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

		public RasterizeStyleDefinition StyleDefinition { get; set; } = new RasterizeStyleDefinition();
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

		public DisplaySetting(int display, SegmentType segmentType, RasterizeStyleDefinition styleDef, int numChars, int numLines)
		{
			Display = display;
			SegmentType = segmentType;
			NumChars = numChars;
			NumLines = numLines;
			StyleDefinition = styleDef;
		}

		/// <summary>
		/// Recalculates the segment dimensions based on a canvas size
		/// </summary>
		/// <param name="canvasWidth">Width the of the canvas in pixels</param>
		/// <param name="canvasHeight">Height of the canvas in pixels</param>
		public void SetDimensions(int canvasWidth, int canvasHeight)
		{
			Dim = new RasterizeDimensions(_res.GetSvgSize(SegmentType, StyleDefinition.SegmentWeight), canvasHeight, NumChars, NumLines, StyleDefinition.SkewAngle, StyleDefinition.LinePad / 100.0f, StyleDefinition.OuterPad / 100.0f);
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
