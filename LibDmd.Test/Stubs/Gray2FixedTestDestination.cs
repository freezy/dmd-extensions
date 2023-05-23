using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class Gray2FixedTestDestination : FixedTestDestination<DmdFrame>, IGray2Destination
	{
		public string Name => "Test Destination (Fixed Gray2)";
		public bool IsAvailable => true;
		
		public Gray2FixedTestDestination(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderGray2(DmdFrame frame)
		{
			LastFrame.OnNext(frame);
		}
		
		public void Dispose()
		{
		}
	}
}
