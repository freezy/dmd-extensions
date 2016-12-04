using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinMameDevice
{
	public class DmdExt
	{

		public void Init()
		{
			
		}

		public void RenderGray2(int width, int height, byte[] frame)
		{
			Console.WriteLine("[dmdext] Render_4_Shades()");
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x++) {
					Console.Write(frame[width * y + x].ToString("X1"));
				}
				Console.WriteLine();
			}
		}
	}
}
