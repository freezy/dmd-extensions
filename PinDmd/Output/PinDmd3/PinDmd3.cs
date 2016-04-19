using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using NLog;
using PinDmd.Input;

namespace PinDmd.Output.PinDmd3
{
	/// <summary>
	/// Output target for PinDMD2 devices.
	/// </summary>
	/// <see cref="http://pindmd.com/"/>
	public class PinDmd3 : IFrameDestination
	{
		/// <summary>
		/// True if device is connected, false otherwise. Check this before accessing anything else.
		/// </summary>
		public bool IsAvailable { get; private set; }

		/// <summary>
		/// Firmware string read from the device if connected
		/// </summary>
		public string Firmware { get; private set; }

		/// <summary>
		/// Width in pixels of the display, 128 for PinDMD3
		/// </summary>
		public int Width { get; private set; }

		/// <summary>
		/// Height in pixels of the display, 32 for PinDMD3
		/// </summary>
		public int Height { get; private set; }

		private static PinDmd3 _instance;
		private readonly PixelRgb24[] _frameBuffer;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Returns the current instance of the PinDMD API.
		/// </summary>
		/// <returns></returns>
		public static PinDmd3 GetInstance()
		{
			if (_instance == null) {
				_instance = new PinDmd3();
			} 
			_instance.Init();
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private PinDmd3()
		{
			_frameBuffer = new PixelRgb24[Width * Height];
		}

		public void Init()
		{
			var port = Interop.Init(new Options() {
				DmdRed = 255,
				DmdGreen = 0,
				DmdBlue = 0,
				DmdColorize = 0
			});
			IsAvailable = port != 0;
			if (IsAvailable) {
				var info = GetInfo();
				Firmware = info.Firmware;
				Width = info.Width;
				Height = info.Height;
				Logger.Info("Found PinDMDv3 device.");
				Logger.Debug("   Firmware:    {0}", Firmware);
				Logger.Debug("   Resolution:  {0}x{1}", Width, Height);
			} else {
				Logger.Debug("PinDMDv3 device not found.");
			}
		}

		/// <summary>
		/// Returns width, height and firmware version of the connected DMD.
		/// 
		/// </summary>
		/// <remarks>Device must be connected, otherwise <seealso cref="SourceNotAvailableException"/> is thrown.</remarks>
		/// <returns>DMD info</returns>
		public DmdInfo GetInfo()
		{
			if (!IsAvailable) {
				throw new SourceNotAvailableException();
			}

			var info = new DeviceInfo();
			Interop.GetDeviceInfo(ref info);

			return new DmdInfo() {
				Width = info.Width,
				Height = info.Height,
				Firmware = info.Firmware
			};
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <remarks>Device must be connected, otherwise <seealso cref="SourceNotAvailableException"/> is thrown.</remarks>
		/// <param name="path">Path to the image, can be anything <see cref="T:System.Drawing.Bitmap"/> understands.</param>
		public void Render(string path)
		{
			if (!IsAvailable) {
				throw new SourceNotAvailableException();
			}
			Render(new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute)));
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
			Interop.RenderRgb24Frame(_frameBuffer);
		}

		public void Dispose()
		{
			Interop.DeInit();
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
