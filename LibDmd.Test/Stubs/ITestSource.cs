using LibDmd.Frame;

namespace LibDmd.Test.Stubs
{
	public interface ITestSource<in TFrame>
	{
		void AddFrame(TFrame frame);
	}
}
