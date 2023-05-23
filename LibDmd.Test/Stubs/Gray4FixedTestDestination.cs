using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class Gray4FixedTestDestination : FixedTestDestination<DmdFrame>, IGray4Destination
	{
		public string Name => "Test Destination (Fixed Gray4)";
		public bool IsAvailable => true;

		public Gray4FixedTestDestination(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderGray4(DmdFrame frame)
		{
			LastFrame.OnNext(frame);
		}
		
		public void Dispose()
		{
		}
	}
}
