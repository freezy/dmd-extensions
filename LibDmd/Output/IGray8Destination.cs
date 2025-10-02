using LibDmd.Frame;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 8 bit frames.
	/// </summary>
	public interface IGray8Destination : IDestination
	{
		/// <summary>
		/// Renders a frame in 8 bit grayscale.
		/// </summary>
		/// <param name="frame">Array containing Width * Height bytes, with values between 0 and 255 for every pixel.</param>
		void RenderGray8(DmdFrame frame);
	}
}
