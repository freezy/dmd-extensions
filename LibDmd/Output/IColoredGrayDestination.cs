using System.Windows.Media;

namespace LibDmd.Output
{
	/// <summary>
	/// Adding this output device or the coloring functions of the pin2color.dll to
	/// a new output device is not allowed without the written permission of the
	/// pin2dmd.com team.
	/// </summary>
	public interface IColoredGrayDestination : IDestination
	{

		/// <summary>
		/// Renders a colored frame in 24 bit RGB.
		/// </summary>
		/// <param name="frame">Array containing Width * Height * 3 bytes, with RGB values between 0 and 255 for every pixel.</param>
		void RenderColoredGray(byte[] frame);

		/// <summary>
		/// Sets the color with which a grayscale source is rendered on the RGB display.
		/// </summary>
		/// <param name="color">Rendered color</param>
		void SetColor(Color color);

		/// <summary>
		/// Sets the palette for rendering grayscale images.
		/// </summary>
		/// <param name="colors"></param>
		void SetPalette(Color[] colors, int index = -1);

		/// <summary>
		/// Removes a previously set palette
		/// </summary>
		void ClearPalette();

		/// <summary>
		/// Resets the color
		/// </summary>
		void ClearColor();
	}
}
