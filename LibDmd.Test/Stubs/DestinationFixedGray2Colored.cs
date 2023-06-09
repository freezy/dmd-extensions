using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;
using NLog;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedGray2Colored : DestinationFixed<ColoredFrame>, IColoredGray2Destination
	{
		public string Name => "Destination[Fixed/ColoredGray4]";
		public bool IsAvailable => true;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public int NumFrames;

		public DestinationFixedGray2Colored(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			Logger.Info("[DestinationFixedGray2Colored] New Frame!");
			LastFrame.OnNext(frame);
			NumFrames++;
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
