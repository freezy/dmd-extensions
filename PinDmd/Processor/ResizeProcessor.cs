using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PinDmd.Processor
{
	public class ResizeProcessor : IProcessor
	{
		public int Width { get; set; }
		public int Height { get; set; }

		public Bitmap process(Bitmap bmp)
		{
			
			return bmp;
		}
	}
}
