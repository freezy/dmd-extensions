using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RavuAlHemio.PbmNet;

namespace PinDmd
{
	/// <summary>
	/// A .NET friendly API for accessing PinDMD3.
	/// 
	/// Work in progress!
	/// 
	/// </summary>
	public class PinDmd
	{
		private int _width;
		private int _height;

		/// <summary>
		/// Initializes the DMD.
		/// </summary>
		/// <returns>True if DMD found, false otherwise</returns>
		public bool Init()
		{
			var port = Interop.Init(new Options()
			{
				DmdRed = 255,
				DmdGreen = 0,
				DmdBlue = 0,
				DmdColorize = 0
			});
			Console.WriteLine("Enabled PinDMD: {0}", port);
			return port != 0;
		}

		/// <summary>
		/// Returns width, height and firmware version of the connected DMD.
		/// Must be run first
		/// </summary>
		/// <returns>DMD info</returns>
		public DmdInfo GetInfo()
		{
			var info = new DeviceInfo();
			Interop.GetDeviceInfo(ref info);
			_width = info.Width;
			_height = info.Height;
			Console.WriteLine("Display at {0}x{1}.", _width, _height);

			return new DmdInfo()
			{
				Width = info.Width,
				Height = info.Height,
				Firmware = info.Firmware
			};
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="path">Path to the image, can be anything <see cref="T:System.Drawing.Bitmap"/> understands.</param>
		public void RenderImage(string path)
		{
			var img = new Bitmap(path);
			if (img.Width != _width || img.Height != _height)
			{
				throw new Exception($"Image must have the same dimensions as the display ({_width}x{_height}).");
			}
			Console.WriteLine("Read bitmap at {0}x{1}.", img.Width, img.Height);

			var frame = new PixelRgb24[4096];
			var n = 0;
			var sw = new Stopwatch();
			sw.Start();
			for (var y = 0; y < _height; y++)
			{
				for (var x = 0; x < _width; x++)
				{
					var color = img.GetPixel(x, y);
					frame[(y * _width) + x].Red = color.R;
					frame[(y * _width) + x].Green = color.G;
					frame[(y * _width) + x].Blue = color.B;
					n++;
				}
			}
			Interop.RenderRgb24Frame(frame);
			sw.Stop();
			Console.WriteLine("{0} pixels written in {1}ms.", n, sw.ElapsedMilliseconds);
		}
	}

	/// <summary>
	/// Defines width, height and firmware of the DMD.
	/// </summary>
	public class DmdInfo
	{
		public byte Width;
		public byte Height;
		public string Firmware;
	}
}
