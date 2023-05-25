using System;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedRgb24 : DestinationFixed<DmdFrame>, IRgb24Destination
	{
		public string Name => "Fixed RGB24";
		public bool IsAvailable => true;

		public DestinationFixedRgb24(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderRgb24(DmdFrame frame)
		{
			LastFrame.OnNext(frame);
		}
		
		public void Dispose()
		{
		}

		public void ClearPalette()
		{
			throw new NotImplementedException();
		}

		public void ClearColor()
		{
			throw new NotImplementedException();
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			throw new NotImplementedException();
		}

		public void SetColor(Color color)
		{
			throw new NotImplementedException();
		}

	}
}
