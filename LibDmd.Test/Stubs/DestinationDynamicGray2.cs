using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationDynamicGray2 : DestinationDynamic<DmdFrame>, IGray2Destination
	{
		public string Name => "Destination[Dynamic/Gray2]";
		public bool IsAvailable => true;

		public void RenderGray2(DmdFrame frame)
		{
			LastFrame.OnNext(frame);
		}
		
		public void Dispose()
		{
		}
	}
}
