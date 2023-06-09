using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedGray2 : DestinationFixed<DmdFrame>, IGray2Destination
	{
		public string Name => "Destination[Fixed/Gray2]";
		public bool IsAvailable => true;

		public int NumFrames;
		
		public DestinationFixedGray2(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderGray2(DmdFrame frame)
		{
			NumFrames++;
			LastFrame.OnNext(frame);
		}
		
		public void Dispose()
		{
		}
	}
}
