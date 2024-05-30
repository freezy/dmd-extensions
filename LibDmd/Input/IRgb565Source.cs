using System;
using LibDmd.Frame;

namespace LibDmd.Input
{

	/// <summary>
	/// A source that is able to deliver 16-bit RGB565 frames without conversion.
	/// </summary>
	public interface IRgb565Source : ISource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of 16-bit RGB565 frames.
		/// 
		/// The returned byte array contains Width * Height * 2 bytes.
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		IObservable<DmdFrame> GetRgb565Frames();
	}
}
