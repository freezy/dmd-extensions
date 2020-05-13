using System;
using System.Threading;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using NLog;

namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public abstract class Pin2DmdBase
	{
		public bool IsAvailable { get; private set; }

		/// <summary>
		/// How long to wait after sending data, in milliseconds
		/// </summary>
		public int Delay { get; set; } = 25;

		private UsbDevice _pin2DmdDevice;
		private byte[] _frameBufferRgb24;
		private readonly byte[] _colorPalette;
		private int _currentPreloadedPalette;
		private bool _paletteIsPreloaded;

		private Dimensions _size128x32 = new Dimensions(128, 32);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected abstract bool HasValidName(string name);
		protected abstract void SetupFrameBuffers();
		public abstract string Name { get; }

		protected Pin2DmdBase()
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

		public void Init()
		{
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

			try {
				_pin2DmdDevice.Open();

				if (HasValidName(_pin2DmdDevice.Info.ProductString)) {

					Logger.Info($"Found device {Name}.");
					Logger.Debug("   Manufacturer: {0}", _pin2DmdDevice.Info.ManufacturerString);
					Logger.Debug("   Product:      {0}", _pin2DmdDevice.Info.ProductString);
					Logger.Debug("   Serial:       {0}", _pin2DmdDevice.Info.SerialString);
					Logger.Debug("   Language ID:  {0}", _pin2DmdDevice.Info.CurrentCultureLangID);

					// 15 bits per pixel plus 4 init bytes
					const int size = (32 * 128 * 15 / 8) + 4;

					// both pin2dmd and pin2dmd xl only take in 128x32 rgb24 frames, so do that here.
					_frameBufferRgb24 = new byte[size];
					_frameBufferRgb24[0] = 0x81; // frame sync bytes
					_frameBufferRgb24[1] = 0xC3;
					_frameBufferRgb24[2] = 0xE8;
					_frameBufferRgb24[3] = 15; // number of planes

					// let the children set up their own buffers
					SetupFrameBuffers();

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

				IsAvailable = true;
				_currentPreloadedPalette = -1;

			} catch (Exception e) {
				IsAvailable = false;
				Logger.Warn(e, "Probing PIN2DMD failed, skipping.");
			}
		}

		public void RenderRgb24(DmdFrame frame)
		{
			// split into sub frames
			var changed = FrameUtil.SplitRgb24(_size128x32, frame.Data, _frameBufferRgb24, 4);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferRgb24);
			}
		}

		public void RenderRaw(byte[] frame)
		{
			try {
				var writer = _pin2DmdDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
				var error = writer.Write(frame, 2000, out var bytesWritten);
				if (error != ErrorCode.None) {
					Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				}
			} catch (Exception e) {
				Logger.Error(e, "Error sending data to PIN2DMD: {0}", e.Message);
			}
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
				Thread.Sleep(Delay);
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
				Thread.Sleep(Delay);
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
				Thread.Sleep(Delay);

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
