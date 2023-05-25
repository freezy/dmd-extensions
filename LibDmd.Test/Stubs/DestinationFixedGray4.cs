using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedGray4 : DestinationFixed<DmdFrame>, IGray4Destination
	{
		public string Name => "Fixed Gray4";
		public bool IsAvailable => true;

		public DestinationFixedGray4(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
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
