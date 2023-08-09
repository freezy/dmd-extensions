using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedColored : DestinationFixed<DmdFrame>, IGray2Destination, IGray4Destination, IColoredGray2Destination,
		IColoredGray4Destination, IColoredGray6Destination, IColorRotationDestination, IRawOutput, IFixedSizeDestination
	{
		public string Name => "Destination[Fixed/Gray2+4+ColoredGray2+4+6+Raw]";
		public bool IsAvailable => true;
		public bool NeedsDuplicateFrames => false;
		public int NumFrames;

		public DestinationFixedColored(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void UpdatePalette(Color[] palette) { }

		public void RenderGray2(DmdFrame frame)
		{
			NumFrames++;
			LastFrame.OnNext(frame);
		}

		public void RenderGray4(DmdFrame frame)
		{
			NumFrames++;
			LastFrame.OnNext(frame);
		}
		
		public void Dispose()
		{
		}

		public void RenderRaw(byte[] data)
		{
		}

		public void RenderRgb24(DmdFrame frame)
		{
			NumFrames++;
			LastFrame.OnNext(frame);
		}

		public void SetColor(Color color)
		{
		}

		public void SetPalette(Color[] colors)
		{
		}

		public void ClearPalette()
		{
		}

		public void ClearColor()
		{
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
		}
	}
}
