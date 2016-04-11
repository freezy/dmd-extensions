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
		Action<Bitmap> Render { get; }
		void RenderBitmap(Bitmap bmp);
	}
}
