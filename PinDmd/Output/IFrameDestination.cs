using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PinDmd.Input;

namespace PinDmd.Output
{
	public interface IFrameDestination
	{
		void StartRendering(IFrameSource source);
		void StopRendering();
		void RenderBitmap(Bitmap bmp);
	}
}
