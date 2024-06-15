using LibDmd.Frame;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 16-bit RGB565 frames.
	/// </summary>
	public interface IRgb565Destination : IRgb24Destination
	{
		/// <summary>
		/// Renders a 16-bit RGB565 frame.
		/// </summary>
		/// <param name="frame">Frame to render</param>
		void RenderRgb565(DmdFrame frame);
	}
}
