using System.Windows.Media;
using LibDmd.Frame;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render RGB 24-bit frames.
	/// </summary>
	public interface IRgb24Destination : IPaletteDestination
	{

		/// <summary>
		/// Renders a frame in 24 bit RGB.
		/// </summary>
		/// <param name="frame">Array containing Width * Height * 3 bytes, with RGB values between 0 and 255 for every pixel.</param>
		void RenderRgb24(DmdFrame frame);

		/// <summary>
		/// Sets the color with which a grayscale source is rendered on the RGB display.
		/// </summary>
		/// <param name="color">Rendered color</param>
		void SetColor(Color color);

		/// <summary>
		/// Resets the color
		/// </summary>
		void ClearColor();
	}
}
