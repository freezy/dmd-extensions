using LibDmd.Output.Virtual.AlphaNumeric;
using SkiaSharp;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	public class DmdStyleDefinition
	{
		/// <summary>
		/// The background color of the display
		/// </summary>
		public SKColor BackgroundColor { get; set; } = SKColors.Black;

		/// <summary>
		/// The top layer
		/// </summary>
		public DmdLayerStyleDefinition Foreground { get; set; } = new DmdLayerStyleDefinition {
			IsEnabled = true,
			Size = 0.8,
			Opacity = 1.0,
			IsRoundedEnabled = false,
			Rounded = 1.0,
			IsBlurEnabled = false
		};

		/// <summary>
		/// The second-to-top layer, usually used for an inner glow effect
		/// </summary>
		public DmdLayerStyleDefinition InnerGlow { get; set; } = new DmdLayerStyleDefinition {
			IsEnabled = false,
		};

		/// <summary>
		/// The third layer, usually used for an outer glow effect
		/// </summary>
		public DmdLayerStyleDefinition OuterGlow { get; set; } = new DmdLayerStyleDefinition {
			IsEnabled = false,
		};

		/// <summary>
		/// The background layer, displaying all segments in an unlit style
		/// </summary>
		public DmdLayerStyleDefinition Background { get; set; } = new DmdLayerStyleDefinition {
			IsEnabled = false,
		};

		/// <summary>
		/// Returns a copy of this style where all parameters are scaled by a
		/// given factor
		/// </summary>
		/// <param name="scaleFactor">Scale factor</param>
		/// <returns>A copy of this object with updated parameters</returns>
		public DmdStyle Scale(float scaleFactor)
		{
			return new DmdStyle {
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
		public DmdStyleDefinition Copy()
		{
			return new DmdStyleDefinition {
				BackgroundColor = new SKColor(BackgroundColor.Red, BackgroundColor.Green, BackgroundColor.Blue, BackgroundColor.Alpha),
				Foreground = Foreground.Copy(),
				InnerGlow = InnerGlow.Copy(),
				OuterGlow = OuterGlow.Copy(),
				Background = Background.Copy()
			};
		}

		public override string ToString()
		{
			return $"BackgroundColor:{BackgroundColor.ToString()},Foreground:{Foreground},InnerGlow:{InnerGlow},OuterGlow:{OuterGlow},Background:{Background}";
		}
	}

	public class DmdStyle
	{		
		/// <summary>
		/// The top layer
		/// </summary>
		public DmdLayerStyle Foreground { get; set; }

		/// <summary>
		/// The second-to-top layer, usually used for an inner glow effect
		/// </summary>
		public DmdLayerStyle InnerGlow { get; set; }

		/// <summary>
		/// The third layer, usually used for an outer glow effect
		/// </summary>
		public DmdLayerStyle OuterGlow { get; set; }

		/// <summary>
		/// The background layer, displaying all segments in an unlit style
		/// </summary>
		public DmdLayerStyle Background { get; set; }
	}

	public enum DmdLayer
	{
		OuterGlow, InnerGlow, Foreground, Background
	}
}