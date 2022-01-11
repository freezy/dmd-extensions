namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 6-bit frames as colored bit planes.
	/// </summary>
	public interface IColoredGray6Destination : IRgb24Destination
	{
		/// <summary>
		/// Renders a colored frame in 6 bits.
		/// </summary>
		/// <param name="frame">Frame to render</param>
		void RenderColoredGray6(ColoredFrame frame);
	}
}
