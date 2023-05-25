using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedGray2Colored : DestinationFixed<ColoredFrame>, IColoredGray2Destination
	{
		public string Name => "Fixed Gray4 Colored";
		public bool IsAvailable => true;

		public DestinationFixedGray2Colored(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			LastFrame.OnNext(frame);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			throw new System.NotImplementedException();
		}

		public void SetColor(Color color)
		{
			throw new System.NotImplementedException();
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			throw new System.NotImplementedException();
		}

		public void ClearPalette()
		{
			throw new System.NotImplementedException();
		}

		public void ClearColor()
		{
			throw new System.NotImplementedException();
		}

		public void Dispose()
		{
		}
	}
}
