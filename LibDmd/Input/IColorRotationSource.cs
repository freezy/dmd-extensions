using System;
using System.Windows.Media;

namespace LibDmd.Input
{
	/// <summary>
	/// A source emitting color rotation, meaning palette changes
	/// without the frame data changing.
	/// </summary>
	public interface IColorRotationSource : ISource
	{
		IObservable<Color[]> GetPaletteChanges();
	}
}
