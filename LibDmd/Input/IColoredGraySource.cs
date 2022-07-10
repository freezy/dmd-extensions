using System;

namespace LibDmd.Input
{
	/// <summary>
	/// A source that is able to deliver color-encoded bit planes
	/// </summary>
	public interface IColoredGraySource : ISource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of RGB 24-bit frames.
		/// 
		/// The returned byte array contains Width * Height * 3 bytes, with values 
		/// for RGB between 0 and 255 for every pixel.
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		IObservable<DMDFrame> GetColoredGrayFrames();
	}
}
