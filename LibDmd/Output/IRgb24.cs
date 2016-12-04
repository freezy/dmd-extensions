using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render RGB 24-bit frames.
	/// </summary>
	public interface IRgb24
	{

		/// <summary>
		/// Renders a frame in 24 bit RGB.
		/// </summary>
		/// <param name="frame">Array containing Width * Height * 3 bytes, with RGB values between 0 and 255 for every pixel.</param>
		void RenderRgb24(byte[] frame);

		/// <summary>
		/// Sets the color with which a grayscale source is rendered on the RGB display.
		/// </summary>
		/// <param name="color">Rendered color</param>
		void SetColor(Color color);
	}
}
