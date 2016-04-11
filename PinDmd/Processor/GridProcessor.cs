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
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;
		public double Padding { get; set; }

		public bool Enabled { get; set; } = true;

		public Bitmap Process(Bitmap bmp)
		{
			return bmp;
		}
	}
}
