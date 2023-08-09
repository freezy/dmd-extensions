﻿using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationDynamicBitmap : DestinationDynamic<BmpFrame>, IBitmapDestination
	{
		public string Name => "Destination[Dynamic/Bitmap]";
		public bool IsAvailable => true;
		public bool NeedsDuplicateFrames => false;

		public void RenderBitmap(BmpFrame frame)
		{
			LastFrame.OnNext(frame);
		}

		public void Dispose()
		{
		}
	}
}
