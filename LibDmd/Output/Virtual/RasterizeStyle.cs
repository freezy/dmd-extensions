namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Defines how the different layers of a segments are rendered.
	/// </summary>
	public class RasterizeStyle
	{
		/// <summary>
		/// Angle in ° how much the segments is skewed
		/// </summary>
		public float SkewAngle { get; set; }

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

		/// <summary>
		/// Returns a copy of this style where all parameters are scaled by a
		/// given factor
		/// </summary>
		/// <param name="scaleFactor">Scale factor</param>
		/// <returns>A copy of this object with updated parameters</returns>
		public RasterizeStyle Scale(float scaleFactor)
		{
			return new RasterizeStyle {
				SkewAngle = SkewAngle,
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
		public RasterizeStyle Copy()
		{
			return new RasterizeStyle {
				SkewAngle = SkewAngle,
				Foreground = Foreground.Copy(),
				InnerGlow = InnerGlow.Copy(),
				OuterGlow = OuterGlow.Copy(),
				Background = Background.Copy()
			};
		}
	}
}