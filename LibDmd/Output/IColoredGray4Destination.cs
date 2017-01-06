using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 4-bit frames as colored bit planes.
	/// </summary>
	public interface IColoredGray4Destination : IRgb24Destination
	{
		/// <summary>
		/// Renders a colored frame in 4 bits.
		/// </summary>
		/// <param name="planes">Array for 4-bit planes</param>
		/// <param name="palette">Four colors for each bit</param>
		void RenderColoredGray4(byte[][] planes, Color[] palette);
	}
}
