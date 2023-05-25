using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationBitmapFixed : DestinationFixed<BmpFrame>, IBitmapDestination
	{
		public string Name => "Fixed Bitmap";
		public bool IsAvailable => true;

		public DestinationBitmapFixed(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
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
