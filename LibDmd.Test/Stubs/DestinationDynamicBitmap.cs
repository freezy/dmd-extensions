using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationDynamicBitmap : DestinationDynamic<BmpFrame>, IBitmapDestination
	{
		public string Name => "Dynamic Bitmap";
		public bool IsAvailable => true;

		public void RenderBitmap(BmpFrame frame)
		{
			LastFrame.OnNext(frame);
		}

		public void Dispose()
		{
		}
	}
}
