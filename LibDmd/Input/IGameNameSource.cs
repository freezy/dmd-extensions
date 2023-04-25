using System;

namespace LibDmd.Input
{
	public interface IGameNameSource
	{
		IObservable<string> GetGameName();
	}
}
