using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LibDmd.Input
{
	/// <summary>
	/// A source that produces bitmaps.
	/// </summary>
	public interface IBitmapSource : ISource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of frames.
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		IObservable<BitmapSource> GetBitmapFrames();
	}
}
