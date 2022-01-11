namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 6 bit frames.
	/// </summary>
	public interface IGray6Destination : IDestination
	{
		/// <summary>
		/// Renders a frame in 6 bit.
		/// </summary>
		/// <param name="frame">Array containing Width * Height bytes, with values between 0 and 15 for every pixel.</param>
		void RenderGray6(byte[] frame);
	}
}
