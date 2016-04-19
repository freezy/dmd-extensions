using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PinDmd.Input;

namespace PinDmd.Output
{
	/// <summary>
	/// A destination where frames are rendered, like a physical display or
	/// some sort of virtual DMD.
	/// </summary>
	public interface IFrameDestination : IDisposable
	{
		/// <summary>
		/// If true, destination is available and can be used as target.
		/// </summary>
		bool IsAvailable { get; }

		/// <summary>
		/// Renders a frame.
		/// </summary>
		/// <param name="bmp">Frame to render</param>
		void Render(BitmapSource bmp);

		/// <summary>
		/// Initializes the device. <see cref="IsAvailable"/> must be set
		/// after running this.
		/// </summary>
		void Init();
	}

	/// <summary>
	/// Thrown on operations that don't make sense without the display connected.
	/// </summary>
	/// <seealso cref="IFrameDestination.IsAvailable"/>
	public class SourceNotAvailableException : Exception
	{
	}
}
