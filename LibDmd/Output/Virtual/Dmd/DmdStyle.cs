using System.Windows.Media;
using System;

namespace LibDmd.Output.Virtual.Dmd
{
	public class DmdStyle
	{
		/// <summary>
		/// Size of dots (1.0 for full size). Note that size above 0.5 may exceed dot clip rect.
		/// </summary>
		public double DotSize { get; set; } = 0.92;

		/// <summary>
		/// Rounding factor: 0.0 for square dots, 1.0 for circle dots.
		/// </summary>
		public double DotRounding { get; set; } = 1.0;

		/// <summary>
		/// Sharpness factor: 0.0 for fuzzy dot borders, 1.0 for sharp borders.
		/// </summary>
		public double DotSharpness { get; set; } = 0.8;

		/// <summary>
		/// Color of unlit dots (use additive blending).
		/// </summary>
		public Color UnlitDot { get; set; } = Color.FromArgb(0, 0, 0, 0);
		public bool HasUnlitDot => UnlitDot.R > 0 || UnlitDot.G > 0 || UnlitDot.B > 0;

		/// <summary>
		/// Overall brightness of dots.
		/// </summary>
		public double Brightness { get; set; } = 0.95;
		public bool HasBrightness => Math.Abs(Brightness - 1.0) > 0.01;

		/// <summary>
		/// Glow caused by dots (blurry light directly around them).
		/// </summary>
		public double DotGlow { get; set; } = 0.0;
		public bool HasDotGlow => DotGlow > 0.01;

		/// <summary>
		/// Glow caused by DMD background (very blurry light that spreads far away from dots).
		/// </summary>
		public double BackGlow { get; set; } = 0.0;
		public bool HasBackGlow => BackGlow > 0.01;

		/// <summary>
		/// Gamma of color space (1.0 for legacy).
		/// </summary>
		public double Gamma { get; set; } = 1.0;
		public bool HasGamma => Math.Abs(Gamma - 1.0) > 0.01;

		/// <summary>
		/// Dot tinting (RGB of given color, alpha is the amount of tinting, alpha = 0 is the default, therefore disabled).
		/// </summary>
		public Color Tint { get; set; } = Color.FromArgb(0x00, 0xFF, 0x58, 0x20);
		public bool HasTint => Tint.A != 0;

		/// <summary>
		/// Path to the texture of the DMD glass (can be either absolute or relative).
		/// </summary>
		public string GlassTexture { get; set; } = null;
		public bool HasGlass => GlassTexture != null && (GlassLighting > 0.0 || GlassColor.R > 0 || GlassColor.G > 0 || GlassColor.B > 0);

		/// <summary>
		/// Padding of the DMD inside the glass. Fake unlit dots are rendered in this padding area.
		/// </summary>
		public System.Windows.Thickness GlassPadding { get; set; } = new System.Windows.Thickness();

		/// <summary>
		/// Glass tint used to tint the provided texture (RGB of given color).
		/// </summary>
		public Color GlassColor { get; set; } = Color.FromArgb(0, 0, 0, 0);

		/// <summary>
		/// Level of light interaction between glass and DMD.
		/// </summary>
		public double GlassLighting { get; set; } = 0.0;

		/// <summary>
		/// Path to the texture of a frame, rendered around the DMD (can be either absolute or relative).
		/// </summary>
		public string FrameTexture { get; set; } = null;

		/// <summary>
		/// Padding of the glass inside the frame.
		/// </summary>
		public System.Windows.Thickness FramePadding { get; set; } = new System.Windows.Thickness();

		/// <summary>
		/// Returns an exact copy of this style
		/// </summary>
		/// <returns></returns>
		public DmdStyle Copy()
		{
			return new DmdStyle
			{
				DotSize = DotSize,
				DotRounding = DotRounding,
				DotSharpness = DotSharpness,
				UnlitDot = Color.FromArgb(UnlitDot.A, UnlitDot.R, UnlitDot.G, UnlitDot.B),
				Brightness = Brightness,
				DotGlow = DotGlow,
				BackGlow = BackGlow,
				Gamma = Gamma,
				Tint = Tint,
				GlassTexture = GlassTexture,
				GlassPadding = new System.Windows.Thickness(GlassPadding.Left, GlassPadding.Top, GlassPadding.Right, GlassPadding.Bottom),
				GlassColor = Color.FromArgb(GlassColor.A, GlassColor.R, GlassColor.G, GlassColor.B),
				GlassLighting = GlassLighting,
				FrameTexture = FrameTexture,
				FramePadding = new System.Windows.Thickness(FramePadding.Left, FramePadding.Top, FramePadding.Right, FramePadding.Bottom)
			};
		}
	}
}
