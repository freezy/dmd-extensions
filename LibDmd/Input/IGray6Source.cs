using System;
using LibDmd.Frame;

namespace LibDmd.Input
{

	/// <summary>
	/// A source that is able to deliver 6-bit frames without conversion.
	/// </summary>
	public interface IGray6Source : ISource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of 6-bit frames.
		/// 
		/// The returned byte array contains Width * Height bytes, with values 
		/// between 0 and 63 for every pixel.
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		IObservable<DmdFrame> GetGray6Frames();
	}
}
