using System;
using LibDmd.Frame;

namespace LibDmd.Input
{

	/// <summary>
	/// A source that is able to deliver 8-bit grayscale frames without conversion.
	/// </summary>
	public interface IGray8Source : ISource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of 8-bit frames.
		///
		/// The returned byte array contains Width * Height bytes, with values
		/// between 0 and 255 for every pixel.
		/// </summary>
		/// <param name="dedupe"></param>
		/// <remarks>When disposed, frame production must stop.</remarks>
		IObservable<DmdFrame> GetGray8Frames(bool dedupe);
	}
}
