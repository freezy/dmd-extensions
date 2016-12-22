using System;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
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
	public class PinDmd3 : BufferRenderer, IGray2Destination, IGray4Destination, IRgb24Destination, IBitmapDestination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "PinDMD v3";

		public override int Width { get; set; } = 128;
		public override int Height { get; set; } = 32;

		public int DmdWidth { get; } = 128;
		public int DmdHeight { get; } = 32;

		public static readonly Color DefaultColor = Colors.OrangeRed;

		const byte Rgb24CommandByte = 0x02;
		const byte Gray4CommandByte = 0x31;

		/// <summary>
		/// Firmware string read from the device if connected
		/// </summary>
		public string Firmware { get; private set; }

		/// <summary>
		/// Manually overriden port name. If set, don't loop through available
		/// ports in order to find the device.
		/// </summary>
		public string Port { get; set; }

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
		/// Returns the current instance of the PinDMD API.
		/// </summary>
		/// <param name="port">Don't loop through available ports but use this COM port name.</param>
		/// <returns>New or current instance</returns>
		public static PinDmd3 GetInstance(string port)
		{
			if (_instance == null) {
				_instance = new PinDmd3 { Port = port };
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
			_frameBufferRgb24[0] = Rgb24CommandByte;
			_frameBufferRgb24[Width * Height * 3 + 1] = Rgb24CommandByte;

			// 4 bits per pixel, 14 control bytes
			_frameBufferGray4 = new byte[Width * Height / 2 + 14];     
			_frameBufferGray4[0] = Gray4CommandByte;
			_frameBufferGray4[Width * Height / 2 + 13] = Gray4CommandByte;

			ClearColor();
		}

		public void Init()
		{
			if (Port != null && Port.Trim().Length > 0) {
				IsAvailable = Connect(Port, false);

			} else {
				var ports = SerialPort.GetPortNames();
				foreach (var portName in ports) {
					IsAvailable = Connect(portName, true);
					if (IsAvailable) {
						break;
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

			var result = new byte[20];
			_serialPort.Read(result, 0, 20); // no idea what this is
		}

		private bool Connect(string port, bool checkFirmware)
		{
			var firmwareRegex = new Regex(@"^rev-vpin-\d+$", RegexOptions.IgnoreCase);
			try {
				Logger.Debug("Checking port {0} for PinDMDv3...", port);
				_serialPort = new SerialPort(port, 921600, Parity.None, 8, StopBits.One);
				_serialPort.Open();
				_serialPort.Write(new byte[] { 0x42, 0x42 }, 0, 2);
				System.Threading.Thread.Sleep(100); // duh...

				var result = new byte[100];
				_serialPort.Read(result, 0, 100);
				Firmware = UTF8.GetString(result.Skip(2).TakeWhile(b => b != 0x00).ToArray());
				if (checkFirmware) {
					if (firmwareRegex.IsMatch(Firmware)) {
						Logger.Info("Found PinDMDv3 device on {0}.", port);
						Logger.Debug("   Firmware:    {0}", Firmware);
						Logger.Debug("   Resolution:  {0}x{1}", (int)result[0], (int)result[1]);
						return true;
					}
				} else {
					Logger.Info("Trusting that PinDMDv3 sits on port {0}.", port);
					Logger.Debug("   Firmware:    {0}", Firmware);
					Logger.Debug("   Resolution:  {0}x{1}", (int)result[0], (int)result[1]);
					return true;
				}

			} catch (Exception e) {
				Logger.Debug("Error: {0}", e.Message.Trim());
				if (_serialPort != null && _serialPort.IsOpen) {
					_serialPort.Close();
				}
			}
			return false;
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="bmp">Any bitmap</param>
		public void RenderBitmap(BitmapSource bmp)
		{
			Logger.Info("Rendering frame as bitmap");
			// make sure we can render
			AssertRenderReady(bmp);

			// copy bmp to rgb24 buffer
			ImageUtil.ConvertToRgb24(bmp, _frameBufferRgb24, 1);

			// send frame buffer to device
			RenderRaw(_frameBufferRgb24);
		}

		public void RenderGray2(byte[] frame)
		{
			// copy frame to frame buffer
			RenderGray4(FrameUtil.Map2To4(frame), _frameBufferGray4, 13);

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

		public void RenderRgb24(byte[] frame)
		{
			Logger.Info("Rendering {0}-byte frame as rgb24", frame.Length);

			// copy data to frame buffer
			Buffer.BlockCopy(frame, 0, _frameBufferRgb24, 1, frame.Length);

			// can directly be sent to the device.
			RenderRaw(_frameBufferRgb24);
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
			if (_serialPort.IsOpen) {
				_serialPort.Write(_frameBufferRgb24, 0, _frameBufferRgb24.Length);
			}
		}

		public void SetColor(Color color)
		{
			double hue, saturation, luminosity;
			byte r, g, b;
			ColorUtil.RgbToHsl(color.R, color.G, color.B, out hue, out saturation, out luminosity);

			_frameBufferGray4[1] = color.R; // 100%: red
			_frameBufferGray4[2] = color.G; // 100%: green
			_frameBufferGray4[3] = color.B; // 100%: blue

			ColorUtil.HslToRgb(hue, saturation, luminosity * 0.66, out r, out g, out b);
			_frameBufferGray4[4] = r;  // 66%: red
			_frameBufferGray4[5] = g;  // 66%: green
			_frameBufferGray4[6] = b;  // 66%: blue

			ColorUtil.HslToRgb(hue, saturation, luminosity * 0.33, out r, out g, out b);
			_frameBufferGray4[7] = r;  // 33%: red
			_frameBufferGray4[8] = g;  // 33%: green
			_frameBufferGray4[9] = b;  // 33%: blue

			_frameBufferGray4[10] = 0x0; // 0%: red
			_frameBufferGray4[11] = 0x0; // 0%: green
			_frameBufferGray4[12] = 0x0; // 0%: blue
		}

		public void SetPalette(Color[] colors)
		{
			var palette = ColorUtil.GetPalette(colors, 4);
			var pos = 1;
			for (var i = 0; i < 4; i++) {
				_frameBufferGray4[pos] = palette[3 - i].R;
				_frameBufferGray4[pos + 1] = palette[3 - i].G;
				_frameBufferGray4[pos + 2] = palette[3 - i].B;
				pos += 3;
			}
		}

		public void ClearPalette()
		{
			ClearColor();
		}

		public void ClearColor()
		{
			SetColor(DefaultColor);
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
