using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PinDmd.Processor
{
	public class GridProcessor : IProcessor
	{
		public int Width { get; set; }
		public int Height { get; set; }
		public double Padding { get; set; }

		public bool Enabled { get; set; } = true;
		public Func<Bitmap, Bitmap> Process => ProcessBitmap;

		private static Bitmap ProcessBitmap(Bitmap bmp)
		{
			return bmp;
		}
	}
}
