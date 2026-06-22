namespace LibDmd.Common
{
	/// <summary>
	/// Scaler mode determines whether "HD up-scaling" is enabled.
	/// </summary>
	/// <remarks>
	/// Extracted from <c>ImageUtil.cs</c> so the cross-platform core (LibDmd.Core) can
	/// reference it without pulling in the WPF/bitmap code that lives in ImageUtil.
	/// </remarks>
	public enum ScalerMode
	{
		/// <summary>
		/// Don't upscale
		/// </summary>
		None,

		/// <summary>
		/// Double the pixels
		/// </summary>
		Doubler,

		/// <summary>
		/// Use scale2x algorithm
		/// </summary>
		Scale2x
	}
}
