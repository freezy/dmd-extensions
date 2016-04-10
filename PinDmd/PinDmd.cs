using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
		/// <summary>
		/// True if device is connected, false otherwise. Check this before accessing anything else.
		/// </summary>
		public bool DeviceConnected { get; }

		/// <summary>
		/// Firmware string read from the device if connected
		/// </summary>
		public string Firmware { get; }

		/// <summary>
		/// Width in pixels of the display, 128 for PinDMD3
		/// </summary>
		public int Width { get; }

		/// <summary>
		/// Height in pixels of the display, 32 for PinDMD3
		/// </summary>
		public int Height { get; }

		private static PinDmd _instance;

		public static PinDmd GetInstance()
		{
			return _instance ?? (_instance = new PinDmd());
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private PinDmd()
		{
			var port = Interop.Init(new Options() {
				DmdRed = 255,
				DmdGreen = 0,
				DmdBlue = 0,
				DmdColorize = 0
			});
			Console.WriteLine("Enabled PinDMD: {0}", port);
			DeviceConnected = port != 0;

			if (DeviceConnected)
			{
				var info = GetInfo();
				Firmware = info.Firmware;
				Width = info.Width;
				Height = info.Height;
				Console.WriteLine("Display found at {0}x{1}.", Width, Height);
			}
		}

		/// <summary>
		/// Returns width, height and firmware version of the connected DMD.
		/// 
		/// </summary>
		/// <remarks>Device must be connected, otherwise <seealso cref="DeviceNotConnectedException"/> is thrown.</remarks>
		/// <returns>DMD info</returns>
		public DmdInfo GetInfo()
		{
			if (!DeviceConnected) {
				throw new DeviceNotConnectedException();
			}

			var info = new DeviceInfo();
			Interop.GetDeviceInfo(ref info);

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
		/// <remarks>Device must be connected, otherwise <seealso cref="DeviceNotConnectedException"/> is thrown.</remarks>
		/// <param name="path">Path to the image, can be anything <see cref="T:System.Drawing.Bitmap"/> understands.</param>
		public void RenderImage(string path)
		{
			if (!DeviceConnected) {
				throw new DeviceNotConnectedException();
			}
			RenderImage(new Bitmap(path));
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="img">Any bitmap</param>
		public void RenderImage(Bitmap img)
		{
			if (img.Width != Width || img.Height != Height) {
				throw new Exception($"Image must have the same dimensions as the display ({Width}x{Height}).");
			}
			Console.WriteLine("Read bitmap at {0}x{1}.", img.Width, img.Height);

			var frame = new PixelRgb24[4096];
			var n = 0;
			var sw = new Stopwatch();
			sw.Start();
			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x++) {
					var color = img.GetPixel(x, y);
					frame[(y * Width) + x].Red = color.R;
					frame[(y * Width) + x].Green = color.G;
					frame[(y * Width) + x].Blue = color.B;
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

	/// <summary>
	/// Thrown on operations that don't make sense without the display connected.
	/// </summary>
	/// <seealso cref="PinDmd.DeviceConnected"/>
	public class DeviceNotConnectedException : Exception
	{
	}
}
