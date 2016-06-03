using System;
using System.CodeDom;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using NLog;

namespace LibDmd.Output.PinDmd3
{
	/// <summary>
	/// Output target for PinDMDv3 devices.
	/// </summary>
	/// <see cref="http://pindmd.com/"/>
	public class PinDmd3 : BufferRenderer, IFrameDestination, IGray4, IRawOutput
	{
		public string Name { get; } = "PinDMD v3";
		public bool IsRgb { get; } = true;

		/// <summary>
		/// Firmware string read from the device if connected
		/// </summary>
		public string Firmware { get; private set; }

		/// <summary>
		/// Width in pixels of the display, 128 for PinDMD3
		/// </summary>
		public override sealed int Width { get; } = 128;

		/// <summary>
		/// Height in pixels of the display, 32 for PinDMD3
		/// </summary>
		public override sealed int Height { get; } = 32;

		private static PinDmd3 _instance;
		private readonly PixelRgb24[] _frameBufferRgb24;
		private readonly byte[] _frameBufferGray4;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// If device is initialized in raw mode, this is the raw device.
		/// </summary>
		private UsbDevice _pinDmd3Device;

		/// <summary>
		/// Returns the current instance of the PinDMD API.
		/// </summary>
		/// <param name="initThroughDll">If true, use pindmd.dll for initialization, otherwise try to identify the USB device</param>
		/// <returns>New or current instance</returns>
		public static PinDmd3 GetInstance(bool initThroughDll)
		{
			if (_instance == null) {
				_instance = new PinDmd3();
			} 
			_instance.Init(initThroughDll);
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private PinDmd3()
		{
			_frameBufferRgb24 = new PixelRgb24[Width * Height];
			_frameBufferGray4 = new byte[Width * Height];
		}

		public void Init()
		{
			Init(false);			
		}

		public void Init(bool initThroughDll)
		{
			if (initThroughDll) {
				InitDll();
			} else {
				InitRaw();
			}
		}

		private void InitDll()
		{
			Logger.Info("Initializing PinDMDv3 through DLL...");
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
				Logger.Info("Found PinDMDv3 device.");
				Logger.Debug("   Firmware:    {0}", Firmware);
				Logger.Debug("   Resolution:  {0}x{1}", Width, Height);

				if (info.Width != Width || info.Height != Height) {
					throw new UnsupportedResolutionException("Should be " + Width + "x" + Height + " but is " + info.Width + "x" + info.Height + ".");
				}
			} else {
				Logger.Debug("PinDMDv3 device not found.");
			}
		}

		private void InitRaw()
		{
			// find and open the usb device.
			Logger.Info("Initializing PinDMDv3 through USB bus...");
			var allDevices = UsbDevice.AllDevices;
			foreach (UsbRegistry usbRegistry in allDevices) {
				UsbDevice device;
				if (usbRegistry.Open(out device)) {
					Logger.Info("Found device {0}/{1}:", device.Info.Descriptor.VendorID, device.Info.Descriptor.ProductID);
					Logger.Debug("   Manufacturer: {0}", _pinDmd3Device.Info.ManufacturerString);
					Logger.Debug("   Product:      {0}", _pinDmd3Device.Info.ProductString);
					Logger.Debug("   Serial:       {0}", _pinDmd3Device.Info.SerialString);
					if (device.Info.Descriptor.VendorID == 0x0314 && (device.Info.Descriptor.ProductID & 0xFFFF) == 0xe457) {
						_pinDmd3Device = device;
						break;
					}
				}
			}

			// if the device is open and ready
			if (_pinDmd3Device == null) {
				Logger.Debug("PinDMDv3 raw device not found.");
				IsAvailable = false;
				return;
			}
			_pinDmd3Device.Open();

			if (_pinDmd3Device.Info.ProductString.Contains("pinDMD V3")) {
				Logger.Info("Found PinDMDv3 device.");
				Logger.Debug("   Manufacturer: {0}", _pinDmd3Device.Info.ManufacturerString);
				Logger.Debug("   Product:      {0}", _pinDmd3Device.Info.ProductString);
				Logger.Debug("   Serial:       {0}", _pinDmd3Device.Info.SerialString);
				Logger.Debug("   Language ID:  {0}", _pinDmd3Device.Info.CurrentCultureLangID);

			} else {
				Logger.Debug("Device found but it's not a PinDMDv3 device ({0}).", _pinDmd3Device.Info.ProductString);
				IsAvailable = false;
				Dispose();
				return;
			}

			var usbDevice = _pinDmd3Device as IUsbDevice;
			if (!ReferenceEquals(usbDevice, null)) {
				usbDevice.SetConfiguration(1);
				usbDevice.ClaimInterface(0);
			}
			IsAvailable = true;
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
		/// <param name="bmp">Any bitmap</param>
		public void Render(BitmapSource bmp)
		{
			// make sure we can render
			AssertRenderReady(bmp);

			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);

			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x++) {
					rect.X = x;
					rect.Y = y;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);
					_frameBufferRgb24[(y * Width) + x].Red = bytes[2];
					_frameBufferRgb24[(y * Width) + x].Green = bytes[1];
					_frameBufferRgb24[(y * Width) + x].Blue = bytes[0];
				}
			}

			// send frame buffer to device
			Interop.RenderRgb24Frame(_frameBufferRgb24);
		}

		public void RenderGray4(BitmapSource bmp)
		{
			// make sure we can render
			AssertRenderReady(bmp);

			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);

			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x++) {
					rect.X = x;
					rect.Y = y;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

					// convert to HSL
					double hue;
					double saturation;
					double luminosity;
					ColorUtil.RgbToHsl(bytes[2], bytes[1], bytes[0], out hue, out saturation, out luminosity);

					var pixel = (byte)(luminosity * 16);
					pixel = pixel == 16 ? (byte)15 : pixel; // special case lum == 1 and hence pixel = 16

					_frameBufferGray4[(y * Width) + x] = pixel;
				}
			}

			// send frame buffer to device
			Interop.Render16ShadeFrame(_frameBufferGray4);
		}

		public void RenderRaw(byte[] data)
		{
			var writer = _pinDmd3Device.OpenEndpointWriter(WriteEndpointID.Ep01);
			int bytesWritten;
			var error = writer.Write(data, 2000, out bytesWritten);
			if (error != ErrorCode.None) {
				Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				throw new RenderException(UsbDevice.LastErrorString);
			}
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
