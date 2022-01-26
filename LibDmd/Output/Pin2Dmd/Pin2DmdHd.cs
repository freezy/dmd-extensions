using System;
using System.Windows.Media;
using LibDmd.Common;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using NLog;
using System.Runtime.InteropServices;

namespace LibDmd.Output.Pin2DmdHd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2DmdHd : IRgb24Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "PIN2DMD HD";
		public bool IsAvailable { get; private set; }

		public int DmdWidth { get; private set; } = 256;
		public int DmdHeight { get; private set; } = 64;

		/// <summary>
		/// How long to wait after sending data, in milliseconds
		/// </summary>
		public int Delay { get; set; } = 25;

		private UsbDevice _pin2DmdDevice;
		private byte[] _frameBufferRgb24;
		private static Pin2DmdHd _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal unsafe struct configDescriptor
		{
			public byte mode;
			public byte palette;
			public fixed byte smartDMD[8];
			public fixed byte foo[6];
			public byte buffermode;
			public byte scaler;
			public byte copyPalToFlash;
			public byte wifi;
			public byte rgbseq;
			public byte invertclock;
			public byte rgbbrightness;
		}

		private configDescriptor pin2dmd_config;

		private Pin2DmdHd()
		{
		}
		

		/// <summary>
		/// Returns the current instance of the PIN2DMD API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static Pin2DmdHd GetInstance(int outputDelay)
		{
			if (_instance == null) {
				_instance = new Pin2DmdHd { Delay = outputDelay };
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
				Logger.Debug("PIN2DMD HD device not found.");
				IsAvailable = false;
				return;
			}

			try {
				_pin2DmdDevice.Open();

				if (_pin2DmdDevice.Info.ProductString.Equals("PIN2DMD HD"))
				{
					Logger.Info("Found PIN2DMD HD device.");
					Logger.Debug("   Manufacturer: {0}", _pin2DmdDevice.Info.ManufacturerString);
					Logger.Debug("   Product:      {0}", _pin2DmdDevice.Info.ProductString);
					Logger.Debug("   Serial:       {0}", _pin2DmdDevice.Info.SerialString);
					Logger.Debug("   Language ID:  {0}", _pin2DmdDevice.Info.CurrentCultureLangID);

					ReadConfig();

					// 18 bits per pixel plus 4 init bytes
					var size = (DmdWidth * DmdHeight / 2 * 6) + 4;
					_frameBufferRgb24 = new byte[size];
					_frameBufferRgb24[0] = 0x81; // frame sync bytes
					_frameBufferRgb24[1] = 0xC3;
					_frameBufferRgb24[2] = 0xE9;
					_frameBufferRgb24[3] = 00; // number of planes

				}
				else
				{
					Logger.Debug("Device found but it's not the correct PIN2DMD HD device ({0}).",
						_pin2DmdDevice.Info.ProductString);
					IsAvailable = false;
					Dispose();
					return;
				}

				if (_pin2DmdDevice is IUsbDevice usbDevice)
				{
					usbDevice.SetConfiguration(1);
					usbDevice.ClaimInterface(0);
				}
#endif
				IsAvailable = true;

			} catch (Exception e) {
				IsAvailable = false;
				Logger.Warn(e, "Probing PIN2DMD HD failed, skipping.");
			}
		}

		/// <summary>
		/// Creates outputbuffer from RGB24 frame
		/// </summary>
		/// <param name="width">Width of the frame</param>
		/// <param name="height">Height of the frame</param>
		/// <param name="frame">RGB24 data, top-left to bottom-right</param>
		/// <param name="frameBuffer">Destination buffer where planes are written</param>
		/// <param name="offset">Start writing at this offset</param>
		/// <returns>True if destination buffer changed, false otherwise.</returns>
		/// 
		public static readonly byte[] GAMMA_TABLE = 
			{ 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
			2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3,
			3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5,
			5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7,
			7, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 10, 10, 10, 10,
			11, 11, 11, 11, 11, 12, 12, 12, 12, 13, 13, 13, 13, 13, 14, 14,
			14, 14, 15, 15, 15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 18,
			19, 19, 19, 20, 20, 20, 21, 21, 21, 22, 22, 22, 23, 23, 23, 24,
			24, 24, 25, 25, 25, 26, 26, 27, 27, 27, 28, 28, 29, 29, 29, 30,
			30, 31, 31, 31, 32, 32, 33, 33, 34, 34, 35, 35, 35, 36, 36, 37,
			37, 38, 38, 39, 39, 40, 40, 41, 41, 42, 42, 43, 43, 44, 44, 45,
			45, 46, 47, 47, 48, 48, 49, 49, 50, 50, 51, 52, 52, 53, 53, 54,
			55, 55, 56, 56, 57, 58, 58, 59, 60, 60, 61, 62, 62, 63, 63, 63 };

		private static bool CreateRgb24(int width, int height, byte[] frame, byte[] frameBuffer, int offset, int rgbSequence)
		{
			var identical = true;
			int elements = width * height / 2;
			int pixel_r, pixel_g, pixel_b, pixel_rl, pixel_gl, pixel_bl;
			for (int l = 0; l < elements; l++)
			{
				int i = l * 3;
				switch (rgbSequence & 0x0F)
				{
					case 1:
						pixel_r = frame[i];
						pixel_b = frame[i + 2];
						pixel_g = frame[i + 1];
						// lower half of display
						pixel_rl = frame[i + (elements * 3)];
						pixel_bl = frame[i + 2 + (elements * 3)];
						pixel_gl = frame[i + 1 + (elements * 3)];
						break;
					case 2:
						pixel_b = frame[i];
						pixel_g = frame[i + 2];
						pixel_r = frame[i + 1];
						// lower half of display
						pixel_bl = frame[i + (elements * 3)];
						pixel_gl = frame[i + 2 + (elements * 3)];
						pixel_rl = frame[i + 1 + (elements * 3)];
						break;
					case 3:
						pixel_g = frame[i];
						pixel_b = frame[i + 2];
						pixel_r = frame[i + 1];
						// lower half of display
						pixel_gl = frame[i + (elements * 3)];
						pixel_bl = frame[i + 2 + (elements * 3)];
						pixel_rl = frame[i + 1 + (elements * 3)];
						break;
					case 4:
						pixel_b = frame[i];
						pixel_r = frame[i + 2];
						pixel_g = frame[i + 1];
						// lower half of display
						pixel_bl = frame[i + (elements * 3)];
						pixel_rl = frame[i + 2 + (elements * 3)];
						pixel_gl = frame[i + 1 + (elements * 3)];
						break;
					case 5:
						pixel_g = frame[i];
						pixel_r = frame[i + 2];
						pixel_b = frame[i + 1];
						// lower half of display
						pixel_gl = frame[i + (elements * 3)];
						pixel_rl = frame[i + 2 + (elements * 3)];
						pixel_bl = frame[i + 1 + (elements * 3)];
						break;
					default:
						pixel_r = frame[i];
						pixel_g = frame[i + 2];
						pixel_b = frame[i + 1];
						// lower half of display
						pixel_rl = frame[i + (elements * 3)];
						pixel_gl = frame[i + 2 + (elements * 3)];
						pixel_bl = frame[i + 1 + (elements * 3)];
						break;
				}

				// color correction
				pixel_r = GAMMA_TABLE[pixel_r];
				pixel_g = GAMMA_TABLE[pixel_g];
				pixel_b = GAMMA_TABLE[pixel_b];

				pixel_rl = GAMMA_TABLE[pixel_rl];
				pixel_gl = GAMMA_TABLE[pixel_gl];
				pixel_bl = GAMMA_TABLE[pixel_bl];

				int target_idx = l + offset;

				for (int k = 0; k < 6; k++)
				{
					byte val = (byte)(((pixel_gl & 1) << 5) | ((pixel_bl & 1) << 4) | ((pixel_rl & 1) << 3) | ((pixel_g & 1) << 2) | ((pixel_b & 1) << 1) | ((pixel_r & 1) << 0));
					identical = identical && frameBuffer[target_idx] == val;
					frameBuffer[target_idx] = val;
					pixel_r >>= 1;
					pixel_g >>= 1;
					pixel_b >>= 1;
					pixel_rl >>= 1;
					pixel_gl >>= 1;
					pixel_bl >>= 1;
					target_idx += elements;
				}
			}
			return !identical;
		}

		private static bool CreateRgb24HD(int width, int height, byte[] frame, byte[] frameBuffer, int offset, int rgbSequence, int buffermode)
		{
			var tmp = new byte[frameBuffer.Length];
			var identical = true;
			CreateRgb24(width, height, frame, tmp, offset, rgbSequence);
			var dest_idx = offset;
			var tmp_idx = offset;

			if (buffermode == 0)
			{
				for (int l = 0; l < (frameBuffer.Length - 4) / 2; l++)
				{
					identical = identical && frameBuffer[dest_idx] == tmp[tmp_idx] && frameBuffer[dest_idx + 1] == tmp[tmp_idx + (width / 2)];
					frameBuffer[dest_idx] = tmp[tmp_idx + (width / 2)];
					frameBuffer[dest_idx + 1] = (byte)(tmp[tmp_idx] << 1);
					dest_idx += 2;
					tmp_idx++;
					if ((dest_idx - offset) % width == 0)
						tmp_idx += width / 2;
				}
			}
			else
			{
				byte val;
				for (int i = 0; i < 32; i++)
				{  // 32 rows of source as we split into upper and lower half
					for (int j = 0; j < 16; j++)
					{ // 16 channels per driver IC with duplicate pixel
						for (int k = 0; k < 8; k++)
						{ // 8 led driver ICs per module
							for (int l = 5; l >= 0; l--)
							{
								tmp_idx = k * 16 + j + i * 256 + offset;
								val = tmp[tmp_idx + (width / 2) + (width * height / 2 * l)];
								identical = identical && frameBuffer[dest_idx] == val;
								frameBuffer[dest_idx++] = val;
								val = (byte)(tmp[tmp_idx + (width * height / 2 * l)] << 1);
								identical = identical && frameBuffer[dest_idx] == val;
								frameBuffer[dest_idx++] = val;
							}
						}
					}
				}
			}
			return !identical;
		}

		public void RenderRgb24(byte[] frame)
		{
			// split into sub frames
			var changed = CreateRgb24HD(DmdWidth, DmdHeight, frame, _frameBufferRgb24, 4, pin2dmd_config.rgbseq, pin2dmd_config.buffermode);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferRgb24);
			}
		}

		
		public void RenderRaw(byte[] frame)
		{
#if (!TEST_WITHOUT_PIN2DMD)
			try {
				var writer = _pin2DmdDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
				int bytesWritten;
				var error = writer.Write(frame, 2000, out bytesWritten);
				if (error != ErrorCode.None) {
					Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				}
			} catch (Exception e) { 
				Logger.Error(e, "Error sending data to PIN2DMD HD: {0}", e.Message);
			}
#endif
		}

		static configDescriptor ReadConfigUsingPointer(byte[] data)
		{
			unsafe
			{
				fixed (byte* packet = &data[10])
				{
					return *(configDescriptor*)packet;
				}
			}
		}

		public void ReadConfig()
		{
#if (!TEST_WITHOUT_PIN2DMD)
			try
			{
				byte[] frame = new byte[2052];
				frame[0] = 0x81;
				frame[1] = 0xc3;
				frame[2] = 0xe7;
				frame[3] = 0xff; // cmd
				frame[4] = 0x10;
				var writer = _pin2DmdDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
				int bytesWritten;
				var error = writer.Write(frame, 2000, out bytesWritten);
				if (error != ErrorCode.None)
				{
					Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				}
			}
			catch (Exception e)
			{
				Logger.Error(e, "Error sending data to PIN2DMD HD: {0}", e.Message);
			}
			try
			{
				byte[] config = new byte[64];
				var reader = _pin2DmdDevice.OpenEndpointReader(ReadEndpointID.Ep01);
				int bytesRead;
				var error = reader.Read(config, 2000, out bytesRead);
				if (error != ErrorCode.None)
				{
					Logger.Error("Error reading config from device: {0}", UsbDevice.LastErrorString);
				} else
				{
					pin2dmd_config = ReadConfigUsingPointer(config);
				}
			}
			catch (Exception e)
			{
				Logger.Error(e, "Error reading  config from PIN2DMD HD: {0}", e.Message);
			}
#endif
		}

		public void SetColor(Color color) //deprecated
		{
			SetSinglePalette(new[] { Colors.Black, color });
		}

		void SetSinglePalette(Color[] colors)
		{
		}

		public void SetPalette(Color[] colors, int index) 
		{
			SetSinglePalette(colors);
			return;
		}

		public void ClearPalette()
		{
			ClearColor();
		}

		public void ClearColor()
		{
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
			System.Threading.Thread.Sleep(Delay);
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
				System.Threading.Thread.Sleep(Delay);

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
