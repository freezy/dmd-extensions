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
			IsEnabled = true,
			IsUnlit = true,
			Size = 0.5,
			UnlitColor = new SKColor(RenderGraph.DefaultColor.R, RenderGraph.DefaultColor.G, RenderGraph.DefaultColor.B, RenderGraph.DefaultColor.A),
			Opacity = 0.2,
		};

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

	public enum DmdLayer
	{
		OuterGlow, InnerGlow, Foreground, Background
	}
}