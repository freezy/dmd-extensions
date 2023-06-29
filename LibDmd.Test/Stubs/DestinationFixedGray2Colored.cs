using System;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedGray2Colored : DestinationFixed<ColoredFrame>, IColoredGray2Destination
	{
		public string Name => "Destination[Fixed/ColoredGray2]";
		public bool IsAvailable => true;

		public int NumFrames;

		public DestinationFixedGray2Colored(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			LastFrame.OnNext(frame);
			NumFrames++;
		}

		public void RenderRgb24(DmdFrame frame)
		{
			LastFrame.OnNext(new ColoredFrame(frame, FrameGenerator.RandomPalette(2))); // don't care about the contents, but need to unblock
			NumFrames++;
		}

		public void SetColor(Color color)
		{
			throw new NotImplementedException();
		}

		public void SetPalette(Color[] colors)
		{
			throw new NotImplementedException();
		}

		public void ClearPalette()
		{
			throw new NotImplementedException();
		}

		public void ClearColor()
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
		}
	}
}
