using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;

namespace PinDmd.Output.PinDmd2
{
	/// <summary>
	/// Output target for PinDMD2 devices.
	/// </summary>
	public class PinDmd2 : IFrameDestination
	{
		public bool IsAvailable { get; private set; }

		// TODO set these from init in order to support arbitrary resolution displays
		public int Width { get; } = 128;
		public int Height { get; } = 32;

		private static PinDmd2 _instance;
		private readonly byte[] _frameBuffer;

		private PinDmd2() {
			IsAvailable = Init() == 2;
			_frameBuffer = new byte[Width * Height];
		}

		/// <summary>
		/// Returns the current instance of the PinDMD2 API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static PinDmd2 GetInstance()
		{
			var instance = _instance ?? (_instance = new PinDmd2());
			instance.IsAvailable = Init() == 2;
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
					var luminosity = (byte) (0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
					_frameBuffer[(y * Width) + x] = luminosity;
				}
			}
			RenderGray8Frame(_frameBuffer);
		}

		#region Dll imports

		[DllImport("pin2dmd.dll", EntryPoint = "pin2dmdInit", CallingConvention = CallingConvention.Cdecl)]
		public static extern int Init();

		[DllImport("pin2dmd.dll", EntryPoint = "pin2dmdDeInit", CallingConvention = CallingConvention.Cdecl)]
		public static extern bool DeInit();

		[DllImport("pin2dmd.dll", EntryPoint = "pin2dmdRender8bit", CallingConvention = CallingConvention.Cdecl)]
		public static extern void RenderGray8Frame(byte[] currbuffer);

		#endregion
	}
}
