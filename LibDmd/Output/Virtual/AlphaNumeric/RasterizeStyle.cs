using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	public class RasterizeStyleDefinition
	{
		/// <summary>
		/// Angle in ° how much the segments is skewed
		/// </summary>
		public float SkewAngle { get; set; } = -12;

		/// <summary>
		/// The background color of the display
		/// </summary>
		public SKColor BackgroundColor { get; set; } = SKColors.Black;

		/// <summary>
		/// The top layer
		/// </summary>
		public RasterizeLayerStyleDefinition Foreground { get; set; } = new RasterizeLayerStyleDefinition {
			IsEnabled = true,
			Color = new SKColor(0xfb, 0xe6, 0xcb, 0xff),
			IsBlurEnabled = true,
			Blur = new SKPoint(2, 2),
			IsDilateEnabled = false
		};

		/// <summary>
		/// The second-to-top layer, usually used for an inner glow effect
		/// </summary>
		public RasterizeLayerStyleDefinition InnerGlow { get; set; } = new RasterizeLayerStyleDefinition
		{
			IsEnabled = true,
			Color = new SKColor(0xdd, 0x6a, 0x03, 0xa0),
			IsBlurEnabled = true,
			Blur = new SKPoint(15, 13),
			IsDilateEnabled = true,
			Dilate = new SKPoint(15, 10)
		};

		/// <summary>
		/// The third layer, usually used for an outer glow effect
		/// </summary>
		public RasterizeLayerStyleDefinition OuterGlow { get; set; } = new RasterizeLayerStyleDefinition
		{
			IsEnabled = true,
			Color = new SKColor(0xb6, 0x58, 0x29, 0x40),
			IsBlurEnabled = true,
			Blur = new SKPoint(50, 50),
			IsDilateEnabled = true,
			Dilate = new SKPoint(90, 40)
		};

		/// <summary>
		/// The background layer, displaying all segments in an unlit style
		/// </summary>
		public RasterizeLayerStyleDefinition Background { get; set; } = new RasterizeLayerStyleDefinition
		{
			IsEnabled = true,
			Color = new SKColor(0xff, 0xff, 0xff, 0x20),
			IsBlurEnabled = true,
			Blur = new SKPoint(7, 7),
			IsDilateEnabled = false
		};

		/// <summary>
		/// Returns a copy of this style where all parameters are scaled by a
		/// given factor
		/// </summary>
		/// <param name="scaleFactor">Scale factor</param>
		/// <returns>A copy of this object with updated parameters</returns>
		public RasterizeStyle Scale(float scaleFactor)
		{
			return new RasterizeStyle {
				Foreground = Foreground.Scale(scaleFactor),
				InnerGlow = InnerGlow.Scale(scaleFactor),
				OuterGlow = OuterGlow.Scale(scaleFactor),
				Background = Background.Scale(scaleFactor),
			};
		}

		/// <summary>
		/// Returns an exact copy of this style
		/// </summary>
		/// <returns></returns>
		public RasterizeStyleDefinition Copy()
		{
			return new RasterizeStyleDefinition {
				SkewAngle = SkewAngle,
				BackgroundColor = new SKColor(BackgroundColor.Red, BackgroundColor.Green, BackgroundColor.Blue, BackgroundColor.Alpha),
				Foreground = Foreground.Copy(),
				InnerGlow = InnerGlow.Copy(),
				OuterGlow = OuterGlow.Copy(),
				Background = Background.Copy()
			};
		}

		public override string ToString()
		{
			return $"SkewAngle:{SkewAngle},BackgroundColor:{BackgroundColor.ToString()},Foreground:{Foreground},InnerGlow:{InnerGlow},OuterGlow:{OuterGlow},Background:{Background}";
		}
	}

	public class RasterizeStyle
	{		
		/// <summary>
		/// The top layer
		/// </summary>
		public RasterizeLayerStyle Foreground { get; set; }

		/// <summary>
		/// The second-to-top layer, usually used for an inner glow effect
		/// </summary>
		public RasterizeLayerStyle InnerGlow { get; set; }

		/// <summary>
		/// The third layer, usually used for an outer glow effect
		/// </summary>
		public RasterizeLayerStyle OuterGlow { get; set; }

		/// <summary>
		/// The background layer, displaying all segments in an unlit style
		/// </summary>
		public RasterizeLayerStyle Background { get; set; }
	}
}