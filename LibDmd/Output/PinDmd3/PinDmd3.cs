using System;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using NLog;
using NLog.LayoutRenderers;
using static System.Text.Encoding;

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

		const byte Rgb24CommandByte = 0x02;
		const byte Gray4CommandByte = 0x31;

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
		private readonly byte[] _frameBufferRgb24;
		private readonly byte[] _frameBufferGray4;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// If device is initialized in raw mode, this is the raw device.
		/// </summary>
		private SerialPort _serialPort;

		/// <summary>
		/// Returns the current instance of the PinDMD API.
		/// </summary>
		/// <returns>New or current instance</returns>
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
			// 3 bytes per pixel, 2 control bytes
			_frameBufferRgb24 = new byte[Width * Height * 3 + 2];
			_frameBufferRgb24[0] = 0x02;
			_frameBufferRgb24[Width * Height * 3 + 1] = Rgb24CommandByte;

			// 4 bits per pixel, 14 control bytes
			_frameBufferGray4 = new byte[Width * Height / 2 + 14];     
			_frameBufferGray4[0] = Gray4CommandByte; // command byte
			_frameBufferGray4[1] = 0xff; // 100%: red
			_frameBufferGray4[2] = 0xff; // 100%: green
			_frameBufferGray4[3] = 0xff; // 100%: blue
			_frameBufferGray4[4] = 0xaa;  // 66%: red
			_frameBufferGray4[5] = 0xaa;  // 66%: green
			_frameBufferGray4[6] = 0xaa;  // 66%: blue
			_frameBufferGray4[7] = 0x55;  // 33%: red
			_frameBufferGray4[8] = 0x55;  // 33%: green
			_frameBufferGray4[9] = 0x55;  // 33%: blue
			_frameBufferGray4[10] = 0x0; // 0%: red
			_frameBufferGray4[11] = 0x0; // 0%: green
			_frameBufferGray4[12] = 0x0; // 0%: blue
			_frameBufferGray4[Width * Height / 2 + 13] = Gray4CommandByte; // command byte
		}

		public void Init()
		{
			byte[] result;
			var ports = SerialPort.GetPortNames();
			var firmwareRegex = new Regex(@"^rev-vpin-\d+$", RegexOptions.IgnoreCase);
			foreach (var portName in ports) {
				try {
					Logger.Debug("Checking port {0} for PinDMDv3...", portName);
					_serialPort = new SerialPort(portName, 921600, Parity.None, 8, StopBits.One);
					_serialPort.Open();
					_serialPort.Write(new byte[] {0x42, 0x42}, 0, 2);
					System.Threading.Thread.Sleep(100); // duh...

					result = new byte[100];
					_serialPort.Read(result, 0, 100);
					Firmware = UTF8.GetString(result.Skip(2).TakeWhile(b => b != 0x00).ToArray());
					if (firmwareRegex.IsMatch(Firmware)) {
						Logger.Info("Found PinDMDv3 device on {0}.", portName);
						Logger.Debug("   Firmware:    {0}", Firmware);
						Logger.Debug("   Resolution:  {0}x{1}", (int) result[0], (int) result[0]);
						IsAvailable = true;
						break;
					}

				} catch (Exception e) {
					Logger.Debug("Error: {0}", e.Message.Trim());
					if (_serialPort != null && _serialPort.IsOpen) {
						_serialPort.Close();
					}
				}
			}

			if (!IsAvailable) {
				Logger.Debug("PinDMDv3 device not found.");
				return;
			}

			// TODO decrypt these
			_serialPort.Write(new byte[] { 0x43, 0x13, 0x55, 0xdb, 0x5c, 0x94, 0x4e, 0x0, 0x0, 0x43 }, 0, 10);
			System.Threading.Thread.Sleep(100); // duuh...

			result = new byte[20];
			_serialPort.Read(result, 0, 20); // no idea what this is
		
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
					var pos = (y * Width * 3) + (x * 3) + 1;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);
					_frameBufferRgb24[pos] = bytes[2];      // r
					_frameBufferRgb24[pos + 1] = bytes[1];  // g
					_frameBufferRgb24[pos + 2] = bytes[0];  // b
				}
			}

			// send frame buffer to device
			RenderRaw(_frameBufferRgb24);
		}

		public void RenderGray4(BitmapSource bmp)
		{
			// copy bitmap to frame buffer
			RenderGray4(bmp, _frameBufferGray4, 13);

			// send frame buffer to device
			RenderRaw(_frameBufferGray4);
		}

		public void RenderGray4(byte[] frame)
		{
			// copy frame to frame buffer
			RenderGray4(frame, _frameBufferGray4, 13);

			// send frame buffer to device
			RenderRaw(_frameBufferGray4);
		}

		public void RenderRaw(byte[] data)
		{
			_serialPort.Write(data, 0, data.Length);
		}

		public void RenderBlank()
		{
			for (var i = 1; i < _frameBufferRgb24.Length - 1; i++) {
				_frameBufferRgb24[i] = 0;
			}
			_serialPort.Write(_frameBufferRgb24, 0, _frameBufferRgb24.Length);
		}

		public void Dispose()
		{
			RenderBlank();
			if (_serialPort.IsOpen) {
				_serialPort.Close();
			}
		}
	}
}
