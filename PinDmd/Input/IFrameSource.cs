using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinDmd.Input
{
	/// <summary>
	/// Acts as source for any frames ending up on the DMD.
	/// </summary>
	/// <remarks>
	/// Since we want a contineous flow of frames, the method to override
	/// returns an observable. Note that the producer decides on the frequency
	/// in which frames are delivered to the consumer.
	/// </remarks>
	public interface IFrameSource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of frames.
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		/// <returns></returns>
		IObservable<Bitmap> GetFrames();
	}
}
