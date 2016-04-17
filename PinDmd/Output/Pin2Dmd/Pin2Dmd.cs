using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using PinDmd.Input;

namespace PinDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2Dmd : IFrameDestination
	{
		public bool IsAvailable { get; private set; }

		// TODO set these from init in order to support arbitrary resolution displays
		public int Width { get; } = 128;
		public int Height { get; } = 32;

		private static Pin2Dmd _instance;
		private readonly PixelRgb24[] _frameBuffer;

		private Pin2Dmd() {
			//IsAvailable = Init() == 1;
			_frameBuffer = new PixelRgb24[Width * Height];
		}

		/// <summary>
		/// Returns the current instance of the PIN2DMD API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static Pin2Dmd GetInstance()
		{
			if (_instance == null) {
				_instance = new Pin2Dmd();
			} else {
				//_instance.IsAvailable = Init() == 1;
			}
			return _instance;
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="bmp">Any bitmap</param>
		public void Render(BitmapSource bmp)
		{
			if (!IsAvailable) {
				throw new SourceNotAvailableException();
			}
			if (bmp.PixelWidth != Width || bmp.PixelHeight != Height) {
				throw new Exception($"Image must have the same dimensions as the display ({Width}x{Height}).");
			}

			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);

			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x++) {
					rect.X = x;
					rect.Y = y;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);
					var color = Color.FromArgb(0xFF, bytes[2], bytes[1], bytes[0]);
					_frameBuffer[(y * Width) + x].Red = color.R;
					_frameBuffer[(y * Width) + x].Green = color.G;
					_frameBuffer[(y * Width) + x].Blue = color.B;
				}
			}
			RenderRgb24Frame(_frameBuffer);
		}

		void IFrameDestination.Init()
		{
		}

		public void Destroy()
		{
		}

		#region Dll imports

		[DllImport("pin2dmd.dll", EntryPoint = "pin2dmdInit", CallingConvention = CallingConvention.Cdecl)]
		public static extern int Init();

		[DllImport("pin2dmd.dll", EntryPoint = "pin2dmdDeInit", CallingConvention = CallingConvention.Cdecl)]
		public static extern bool DeInit();

		[DllImport("pin2dmd.dll", EntryPoint = "pin2dmdRenderRGB24", CallingConvention = CallingConvention.Cdecl)]
		public static extern void RenderRgb24Frame(PixelRgb24[] currbuffer);

		[StructLayout(LayoutKind.Sequential)]
		public struct PixelRgb24
		{
			public byte Red;
			public byte Green;
			public byte Blue;
		}

		#endregion
	}
}
