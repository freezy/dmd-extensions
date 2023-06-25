using System;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationDynamicRgb24 : DestinationDynamic<DmdFrame>, IRgb24Destination
	{
		public string Name => "Destination[Dynamic/RGB24]";
		public bool IsAvailable => true;

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
