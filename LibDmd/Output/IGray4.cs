using System.Windows.Media.Imaging;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 4 bit frames.
	/// </summary>
	public interface IGray4
	{
		/// <summary>
		/// Renders a frame in 4 bit.
		/// </summary>
		/// <param name="bmp">Frame to render</param>
		void RenderGray4(BitmapSource bmp);

		/// <summary>
		/// Renders a frame in 4 bit.
		/// </summary>
		/// <param name="bmp">Array containing Width * Height bytes, with values between 0 and 15 for every pixel.</param>
		void RenderGray4(byte[] bmp);
	}
}
