using System.Windows.Media.Imaging;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 2 bit frames.
	/// </summary>
	public interface IGray2
	{
		/// <summary>
		/// Renders a frame in 2 bit.
		/// </summary>
		/// <param name="bmp">Frame to render</param>
		void RenderGray2(BitmapSource bmp);

		/// <summary>
		/// Renders a frame in 2 bit.
		/// </summary>
		/// <param name="frame">Array containing Width * Height bytes, with values between 0 and 3 for every pixel.</param>
		void RenderGray2(byte[] frame);
	}
}
