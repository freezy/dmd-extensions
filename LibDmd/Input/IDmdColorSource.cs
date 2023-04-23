using System;
using System.Windows.Media;

namespace LibDmd.Input
{
	public interface IDmdColorSource
	{
		IObservable<Color> GetDmdColor();
	}
}
