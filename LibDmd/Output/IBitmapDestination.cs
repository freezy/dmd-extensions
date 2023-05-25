using System.Windows.Media.Imaging;
using LibDmd.Frame;

namespace LibDmd.Output
{
	/// <summary>
	/// Output devices implementing this interface are able to
	/// render 2 bit frames.
	/// </summary>
	public interface IBitmapDestination : IDestination
	{
		/// <summary>
		/// Renders a bitmap frame.
		/// </summary>
		/// <param name="frame">Frame to render</param>
		void RenderBitmap(BmpFrame frame);
	}
}
