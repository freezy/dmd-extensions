using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedGray6Colored : DestinationFixed<ColoredFrame>, IColoredGray6Destination
	{
		public string Name => "Destination[Fixed/ColoredGray6]";
		public bool IsAvailable => true;

		public int NumFrames;

		public DestinationFixedGray6Colored(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			NumFrames++;
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
