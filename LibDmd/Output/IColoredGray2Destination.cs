using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 2-bit frames as colored bit planes.
	/// </summary>
	public interface IColoredGray2Destination : IDestination
	{
		/// <summary>
		/// Renders a colored frame in 2 bits.
		/// </summary>
		/// <param name="planes">Array for 2-bit planes</param>
		/// <param name="palette">Two colors for each bit</param>
		void RenderColoredGray2(byte[][] planes, Color[] palette);
	}
}
