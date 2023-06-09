using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedBitmap : DestinationFixed<BmpFrame>, IBitmapDestination
	{
		public string Name => "Destination[Fixed/Bitmap]";
		public bool IsAvailable => true;

		public DestinationFixedBitmap(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderBitmap(BmpFrame frame)
		{
			LastFrame.OnNext(frame);
		}

		public void Dispose()
		{
		}
	}
}
