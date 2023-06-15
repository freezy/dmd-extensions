using System;

namespace LibDmd.Input
{
	/// <summary>
	/// A source that is able to deliver color-encoded bit planes for six bits
	/// per pixel.
	/// </summary>
	public interface IColoredGray6Source : ISource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of 6-bit sub frames
		/// with a 64-color palette.
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		IObservable<ColoredFrame> GetColoredGray6Frames();
	}
}
