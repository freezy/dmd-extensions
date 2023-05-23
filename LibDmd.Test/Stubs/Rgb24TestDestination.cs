using System;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class Rgb24TestDestination : FixedTestDestination<DmdFrame>, IRgb24Destination
	{
		public string Name => "Test Destination (Fixed RGB24)";
		public bool IsAvailable => true;

		public Rgb24TestDestination(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
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
