using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PinDmd
{
	public class ScreenGrabber
	{
		public Bitmap CaptureImage(int left, int top, int width, int height)
		{
			return NativeMethods.GetDesktopBitmap(left, top, width, height);
		}
	}
}
