using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using NLog;

namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2Dmd : BufferRenderer, IFrameDestination, IGray4, IRgb24, IRawOutput
	{
		public string Name { get; } = "PIN2DMD";
		public bool IsRgb { get; } = true;

		public override sealed int Width { get; } = 128;
		public override sealed int Height { get; } = 32;

		private UsbDevice _pin2DmdDevice;
		private readonly byte[] _frameBufferRgb24;
		private readonly byte[] _frameBufferGray4;
		private readonly byte[] _colorPalette;

		private static Pin2Dmd _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Pin2Dmd()
		{
			// 15 bits per pixel plus 4 init bytes
			var size = (Width * Height * 15 / 8) + 4;
			_frameBufferRgb24 = new byte[size];
			_frameBufferRgb24[0] = 0x81; // frame sync bytes
			_frameBufferRgb24[1] = 0xC3;
			_frameBufferRgb24[2] = 0xE8;
			_frameBufferRgb24[3] = 15;   // number of planes

			// 4 bits per pixel plus 4 init bytes
			size = (Width * Height * 4 / 8) + 4;
			_frameBufferGray4 = new byte[size];
			_frameBufferGray4[0] = 0x81; // frame sync bytes
			_frameBufferGray4[1] = 0xC3;
			_frameBufferGray4[2] = 0xE7;
			_frameBufferGray4[3] = 0x00;

			// color palette
			_colorPalette = new byte[2052];
			_colorPalette[0] = 0x81;
			_colorPalette[1] = 0xC3;
			_colorPalette[2] = 0xE7;
			_colorPalette[3] = 0xFF;
			_colorPalette[4] = 0x04;
			_colorPalette[5] = 0x00;
			_colorPalette[6] = 0x01;
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
			var allDevices = UsbDevice.AllDevices;
			foreach (UsbRegistry usbRegistry in allDevices) {
				UsbDevice device;
				if (usbRegistry.Open(out device)) {
					if (device.Info.Descriptor.VendorID == 0x0314 && (device.Info.Descriptor.ProductID & 0xFFFF) == 0xe457) {
						_pin2DmdDevice = device;
						break;
					}
				}
			}

			// if the device is open and ready
			if (_pin2DmdDevice == null) {
				Logger.Debug("PIN2DMD device not found.");
				IsAvailable = false;
				return;
			}
			_pin2DmdDevice.Open();

			if (_pin2DmdDevice.Info.ManufacturerString.Contains("PIN2DMD")) {
				Logger.Info("Found PIN2DMD device.");
				Logger.Debug("   Manufacturer: {0}", _pin2DmdDevice.Info.ManufacturerString);
				Logger.Debug("   Product:      {0}", _pin2DmdDevice.Info.ProductString);
				Logger.Debug("   Serial:       {0}", _pin2DmdDevice.Info.SerialString);
				Logger.Debug("   Language ID:  {0}", _pin2DmdDevice.Info.CurrentCultureLangID);
			} else {
				Logger.Debug("Device found but it's not a PIN2DMD device ({0}).", _pin2DmdDevice.Info.ProductString);
				IsAvailable = false;
				Dispose();
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
			// copy bitmap to frame buffer
			RenderRgb24(bmp, _frameBufferRgb24, 4);

			// send frame buffer to device
			RenderRaw(_frameBufferRgb24);
		}

		/// <summary>
		/// Renders an image in 4 bit to the display.
		/// </summary>
		/// <param name="bmp"></param>
		public void RenderGray4(BitmapSource bmp)
		{
			// copy bitmap to frame buffer
			RenderGray4(bmp, _frameBufferGray4, 4);

			// send frame buffer to device
			RenderRaw(_frameBufferGray4);
		}

		public void RenderGray4(byte[] frame)
		{
			// copy frame to frame buffer
			RenderGray4(frame, _frameBufferGray4, 4);

			// send frame buffer to device
			RenderRaw(_frameBufferGray4);
		}

		public void RenderRaw(byte[] frame)
		{
			var writer = _pin2DmdDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
			int bytesWritten;
			var error = writer.Write(frame, 2000, out bytesWritten);
			if (error != ErrorCode.None) {
				Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				throw new Exception(UsbDevice.LastErrorString);
			}
		}

		public void SetColor(Color color)
		{
			double hue, saturation, luminosity;
			ColorUtil.RgbToHsl(color.R, color.G, color.B, out hue, out saturation, out luminosity);
			const int offset = 7;

			for (var i = 0; i < 16; i++) {
				var lum = (double)i / 15;
				byte r, g, b;
				ColorUtil.HslToRgb(hue, saturation, luminosity * lum, out r, out g, out b);
				_colorPalette[offset + (i * 3)] = r;
				_colorPalette[offset + (i * 3) + 1] = g;
				_colorPalette[offset + (i * 3) + 2] = b;
			}
			RenderRaw(_colorPalette);
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
