using System.Reactive.Subjects;

namespace LibDmd.Test.Stubs
{
	public interface ITestDestination<out TFrame>
	{
		IConnectableObservable<TFrame> Frame { get; }
		void Reset();
	}
}
