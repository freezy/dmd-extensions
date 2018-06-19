using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibDmd.Converter.Colorize;
using System.Collections.Generic;
using NLog;

namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2Dmd : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "PIN2DMD";
		public bool IsAvailable { get; private set; }

		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get; private set; } = 32;

		private UsbDevice _pin2DmdDevice;
		private byte[] _frameBufferRgb24;
		private byte[] _frameBufferGray4;
		private readonly byte[] _colorPalette;
		private int _currentPreloadedPalette;
		private bool _paletteIsPreloaded;
		private static Pin2Dmd _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Pin2Dmd()
		{
			// color palette
			_colorPalette = new byte[2052];
			_colorPalette[0] = 0x81;
			_colorPalette[1] = 0xC3;
			_colorPalette[2] = 0xE7;
			_colorPalette[3] = 0xFF;
			_colorPalette[4] = 0x04;
			_paletteIsPreloaded = false;
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
#if (!TEST_WITHOUT_PIN2DMD)
			// find and open the usb device.
			var allDevices = UsbDevice.AllDevices;
			foreach (UsbRegistry usbRegistry in allDevices) {
				UsbDevice device;
				if (usbRegistry.Open(out device)) {
					if (device?.Info?.Descriptor?.VendorID == 0x0314 && (device.Info.Descriptor.ProductID & 0xFFFF) == 0xe457) {
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

			if (_pin2DmdDevice.Info.ProductString.Contains("PIN2DMD")) {
				Logger.Info("Found PIN2DMD device.");
				Logger.Debug("   Manufacturer: {0}", _pin2DmdDevice.Info.ManufacturerString);
				Logger.Debug("   Product:      {0}", _pin2DmdDevice.Info.ProductString);
				Logger.Debug("   Serial:       {0}", _pin2DmdDevice.Info.SerialString);
				Logger.Debug("   Language ID:  {0}", _pin2DmdDevice.Info.CurrentCultureLangID);

				if (_pin2DmdDevice.Info.ProductString.Contains("PIN2DMD XL")) {
					DmdWidth = 192;
					DmdHeight = 64;
				}

				// 15 bits per pixel plus 4 init bytes
				var size = (DmdWidth * DmdHeight * 15 / 8) + 4;
				_frameBufferRgb24 = new byte[size];
				_frameBufferRgb24[0] = 0x81; // frame sync bytes
				_frameBufferRgb24[1] = 0xC3;
				_frameBufferRgb24[2] = 0xE8;
				_frameBufferRgb24[3] = 15;   // number of planes

				// 4 bits per pixel plus 4 init bytes
				size = (DmdWidth * DmdHeight * 4 / 8) + 4;
				_frameBufferGray4 = new byte[size];
				_frameBufferGray4[0] = 0x81; // frame sync bytes
				_frameBufferGray4[1] = 0xC3;
				_frameBufferGray4[2] = 0xE7;
				_frameBufferGray4[3] = 0x00;

			} else {
				Logger.Debug("Device found but it's not a PIN2DMD device ({0}).", _pin2DmdDevice.Info.ProductString);
				IsAvailable = false;
				Dispose();
				return;
			}

			if (_pin2DmdDevice is IUsbDevice usbDevice) {
				usbDevice.SetConfiguration(1);
				usbDevice.ClaimInterface(0);
			}
#else
			DmdWidth = 192;
			DmdHeight = 64;
			// 15 bits per pixel plus 4 init bytes
			var size = (DmdWidth * DmdHeight * 15 / 8) + 4;
			_frameBufferRgb24 = new byte[size];
			_frameBufferRgb24[0] = 0x81; // frame sync bytes
			_frameBufferRgb24[1] = 0xC3;
			_frameBufferRgb24[2] = 0xE8;
			_frameBufferRgb24[3] = 15;   // number of planes

			// 4 bits per pixel plus 4 init bytes
			size = (DmdWidth * DmdHeight * 4 / 8) + 4;
			_frameBufferGray4 = new byte[size];
			_frameBufferGray4[0] = 0x81; // frame sync bytes
			_frameBufferGray4[1] = 0xC3;
			_frameBufferGray4[2] = 0xE7;
			_frameBufferGray4[3] = 0x00;
#endif
			IsAvailable = true;
			_currentPreloadedPalette = -1;
		}

		public void RenderGray2(byte[] frame)
		{
			// 2-bit frames are rendered as 4-bit
			RenderGray4(FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x1, 0x4, 0xf }));
		}

		public void RenderGray4(byte[] frame)
		{
			// convert to bit planes
			var planes = FrameUtil.Split(DmdWidth, DmdHeight, 4, frame);

			// copy to buffer
			var changed = FrameUtil.Copy(planes, _frameBufferGray4, 4);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferGray4);
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			// split into sub frames
			var changed = FrameUtil.SplitRgb24(DmdWidth, DmdHeight, frame, _frameBufferRgb24, 4);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferRgb24);
			}
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			SetPalette(frame.Palette, frame.PaletteIndex);

			// copy to buffer
			var changed = FrameUtil.Copy(frame.Planes, _frameBufferGray4, 4);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferGray4);
			}
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			SetPalette(frame.Palette, frame.PaletteIndex);

			var joinedFrame = FrameUtil.Join(DmdWidth, DmdHeight, frame.Planes);

			// send frame buffer to device
			RenderGray4(FrameUtil.ConvertGrayToGray(joinedFrame, new byte[] { 0x0, 0x1, 0x4, 0xf }));
		}

		public void RenderRaw(byte[] frame)
		{
#if (!TEST_WITHOUT_PIN2DMD)
			var writer = _pin2DmdDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
			int bytesWritten;
			var error = writer.Write(frame, 2000, out bytesWritten);
			if (error != ErrorCode.None) {
				Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
			}
#endif
		}

		public void SetColor(Color color)
		{
			SetSinglePalette(new[] { Colors.Black, color });
		}

		public void SetSinglePalette(Color[] colors)
		{
			var palette = ColorUtil.GetPalette(colors, 16);
			var identical = true;
			var pos = 7;
			_colorPalette[5] = 0x00;
			_colorPalette[6] = 0x01;
			for (var i = 0; i < 16; i++) {
				var color = palette[i];
				identical = identical && _colorPalette[pos] == color.R && _colorPalette[pos + 1] == color.G && _colorPalette[pos + 2] == color.B;
				_colorPalette[pos] = color.R;
				_colorPalette[pos + 1] = color.G;
				_colorPalette[pos + 2] = color.B;
				pos += 3;
			}
			if (!identical) {
				RenderRaw(_colorPalette);
				System.Threading.Thread.Sleep(50);
			}
		}

		public void SetPalette(Color[] colors, int index)
		{
			if (index >= 0 && _paletteIsPreloaded) {
				if (index == _currentPreloadedPalette)
					return;
				Logger.Debug("[Pin2DMD] Switch to index " + index.ToString());
				SwitchToPreloadedPalette((ushort)index);
				_currentPreloadedPalette = index;
			} else { // We have a palette request not associated with an index
				Logger.Debug("[Pin2DMD] Palette switch without index");

				SetSinglePalette(colors);
				_currentPreloadedPalette = -1;
				if (_paletteIsPreloaded) {
					Logger.Warn("[Pin2DMD] Request to change without index, preloaded palette lost.");
					_paletteIsPreloaded = false;
				}
			}
		}

		public void PreloadPalettes(Coloring coloring)
		{
			Logger.Debug("[Pin2DMD] Preloading " + coloring.Palettes.Length + "palettes.");
			foreach (var palette in coloring.Palettes) {
				var pos = 7;
				for (var i = 0; i < 16; i++) {
					var color = palette.Colors[i];
					_colorPalette[pos] = color.R;
					_colorPalette[pos + 1] = color.G;
					_colorPalette[pos + 2] = color.B;
					pos += 3;
				}
				_colorPalette[5] = (byte)palette.Index;
				_colorPalette[6] = (byte)palette.Type;

				RenderRaw(_colorPalette);
				System.Threading.Thread.Sleep(100);
			}
			_paletteIsPreloaded = true;
		}

		public void SwitchToPreloadedPalette(uint index)
		{
			var hexIndexStr = index.ToString("X2");

			var buffer = new byte[64];
			buffer[0] = 0x01;
			buffer[1] = 0xC3;
			buffer[2] = 0xE7;
			buffer[3] = (byte)hexIndexStr[0];
			buffer[4] = (byte)hexIndexStr[1];
			RenderRaw(buffer);
		}

		public void ClearPalette()
		{
			ClearColor();
		}

		public void ClearColor()
		{
			// Skip if a palette is preloaded, as it will wipe it out, 
			// and we know palettes will be selected by the colorizer.
			if (!_paletteIsPreloaded)
				SetColor(RenderGraph.DefaultColor);
		}

		public void ClearDisplay()
		{
			var buffer = new byte[2052];
			buffer[0] = 0x81;
			buffer[1] = 0xC3;
			buffer[2] = 0xE7;
			buffer[3] = 0x00;
			RenderRaw(buffer);
			System.Threading.Thread.Sleep(50);
		}

		public void Dispose()
		{
			if (_pin2DmdDevice != null) {
				var buffer = new byte[2052];

				// reset settings
				buffer[0] = 0x81;
				buffer[1] = 0xC3;
				buffer[2] = 0xE7;
				buffer[3] = 0xFF;
				buffer[4] = 0x07;
				RenderRaw(buffer);
				System.Threading.Thread.Sleep(50);

				// close device
				if (_pin2DmdDevice.IsOpen) {
					var wholeUsbDevice = _pin2DmdDevice as IUsbDevice;
					wholeUsbDevice?.ReleaseInterface(0);
					_pin2DmdDevice.Close();
				}
			}
			_pin2DmdDevice = null;
			UsbDevice.Exit();
		}
	}
}
