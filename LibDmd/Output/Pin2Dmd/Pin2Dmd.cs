using System;
using System.Windows;
using System.Windows.Media.Imaging;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using NLog;

namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2Dmd : IFrameDestination
	{
		public bool IsAvailable { get; private set; }

		public int Width { get; } = 128;
		public int Height { get; } = 32;

		private UsbDevice _pin2DmdDevice;
		private readonly byte[] _frameBuffer;

		private static Pin2Dmd _instance;
		private static readonly UsbDeviceFinder Pin2DmdFinder = new UsbDeviceFinder(0x0314, 0xe457);
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Pin2Dmd()
		{
			_frameBuffer = new byte[7684];
			_frameBuffer[0] = 0x81; // frame sync bytes
			_frameBuffer[1] = 0xC3;
			_frameBuffer[2] = 0xE8;
			_frameBuffer[3] = 15;   // number of planes
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
			}
			_instance.Init();
			return _instance;
		}

		public void Init()
		{
			// find and open the usb device.
			_pin2DmdDevice = UsbDevice.OpenUsbDevice(Pin2DmdFinder);

			// if the device is open and ready
			if (_pin2DmdDevice == null) {
				Logger.Debug("PIN2DMD device not found.");
				IsAvailable = false;
				return;
			}

			if (_pin2DmdDevice.Info.ManufacturerString.Contains("PIN2DMD")) {
				Logger.Info("Found PIN2DMD device.");
				Logger.Debug("   Manufacturer: {0}", _pin2DmdDevice.Info.ManufacturerString);
				Logger.Debug("   Product:      {0}", _pin2DmdDevice.Info.ProductString);
				Logger.Debug("   Serial:       {0}", _pin2DmdDevice.Info.SerialString);
				Logger.Debug("   Language ID:  {0}", _pin2DmdDevice.Info.CurrentCultureLangID);
			} else {
				Logger.Debug("Device found but it's not a PIN2DMD device ({0}).", _pin2DmdDevice.Info.ProductString);
				IsAvailable = false;
				return;
			}

			var usbDevice = _pin2DmdDevice as IUsbDevice;
			if (!ReferenceEquals(usbDevice, null)) {
				usbDevice.SetConfiguration(1);
				usbDevice.ClaimInterface(0);
			}
			IsAvailable = true;
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
			var byteIdx = 4;

			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x += 8) {
					byte r3 = 0;
					byte r4 = 0;
					byte r5 = 0;
					byte r6 = 0;
					byte r7 = 0;

					byte g3 = 0;
					byte g4 = 0;
					byte g5 = 0;
					byte g6 = 0;
					byte g7 = 0;

					byte b3 = 0;
					byte b4 = 0;
					byte b5 = 0;
					byte b6 = 0;
					byte b7 = 0;
					for (var v = 7; v >= 0; v--) {
						rect.X = x + v;
						rect.Y = y;
						bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

						// convert to HSL
						var pixelr = bytes[2];
						var pixelg = bytes[1];
						var pixelb = bytes[0];

						r3 <<= 1;
						r4 <<= 1;
						r5 <<= 1;
						r6 <<= 1;
						r7 <<= 1;
						g3 <<= 1;
						g4 <<= 1;
						g5 <<= 1;
						g6 <<= 1;
						g7 <<= 1;
						b3 <<= 1;
						b4 <<= 1;
						b5 <<= 1;
						b6 <<= 1;
						b7 <<= 1;

						if ((pixelr & 8) != 0) r3 |= 1;
						if ((pixelr & 16) != 0) r4 |= 1;
						if ((pixelr & 32) != 0) r5 |= 1;
						if ((pixelr & 64) != 0) r6 |= 1;
						if ((pixelr & 128) != 0) r7 |= 1;

						if ((pixelg & 8) != 0) g3 |= 1;
						if ((pixelg & 16) != 0) g4 |= 1;
						if ((pixelg & 32) != 0) g5 |= 1;
						if ((pixelg & 64) != 0) g6 |= 1;
						if ((pixelg & 128) != 0) g7 |= 1;

						if ((pixelb & 8) != 0) b3 |= 1;
						if ((pixelb & 16) != 0) b4 |= 1;
						if ((pixelb & 32) != 0) b5 |= 1;
						if ((pixelb & 64) != 0) b6 |= 1;
						if ((pixelb & 128) != 0) b7 |= 1;
					}

					_frameBuffer[byteIdx + 5120] = r3;
					_frameBuffer[byteIdx + 5632] = r4;
					_frameBuffer[byteIdx + 6144] = r5;
					_frameBuffer[byteIdx + 6656] = r6;
					_frameBuffer[byteIdx + 7168] = r7;

					_frameBuffer[byteIdx + 2560] = g3;
					_frameBuffer[byteIdx + 3072] = g4;
					_frameBuffer[byteIdx + 3584] = g5;
					_frameBuffer[byteIdx + 4096] = g6;
					_frameBuffer[byteIdx + 4608] = g7;

					_frameBuffer[byteIdx + 0] = b3;
					_frameBuffer[byteIdx + 512] = b4;
					_frameBuffer[byteIdx + 1024] = b5;
					_frameBuffer[byteIdx + 1536] = b6;
					_frameBuffer[byteIdx + 2048] = b7;
					byteIdx++;
				}
			}

			var writer = _pin2DmdDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
			int bytesWritten;
			var error = writer.Write(_frameBuffer, 2000, out bytesWritten);
			if (error != ErrorCode.None) {
				Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				throw new Exception(UsbDevice.LastErrorString);
			}
		}

		public void Dispose()
		{
			if (_pin2DmdDevice != null) {
				if (_pin2DmdDevice.IsOpen) {
					var wholeUsbDevice = _pin2DmdDevice as IUsbDevice;
					if (!ReferenceEquals(wholeUsbDevice, null)) {
						wholeUsbDevice.ReleaseInterface(0);
					}
					_pin2DmdDevice.Close();
				}
			}
			_pin2DmdDevice = null;
			UsbDevice.Exit();
		}
	}
}
