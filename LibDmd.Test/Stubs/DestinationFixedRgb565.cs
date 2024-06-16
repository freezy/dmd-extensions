using System;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationFixedRgb565 : DestinationFixed<DmdFrame>, IRgb565Destination
	{
		public string Name => "Destination[Fixed/RGB565]";
		public bool IsAvailable => true;
		public bool NeedsDuplicateFrames => false;

		public DestinationFixedRgb565(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderRgb565(DmdFrame frame)
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

		public void SetPalette(Color[] colors)
		{
			throw new NotImplementedException();
		}

		public void SetColor(Color color)
		{
			throw new NotImplementedException();
		}

	}
}
