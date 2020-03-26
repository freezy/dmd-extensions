﻿using System;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;
using static System.Text.Encoding;

namespace LibDmd.Output.PinDmd3
{
	/// <summary>
	/// Output target for PinDMDv3 devices.
	/// </summary>
	/// <see cref="http://pindmd.com/"/>
	public class PinDmd3 : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "PinDMD v3";
		public bool IsAvailable { get; private set; }

		public int Delay { get; set; } = 100;
		public int DmdWidth { get; } = 128;
		public int DmdHeight { get; } = 32;

		const byte Rgb24CommandByte = 0x02;
		const byte Gray2CommandByte = 0x30;
		const byte Gray4CommandByte = 0x31;
		const byte ColoredGray4CommandByte = 0x32;

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
		private readonly byte[] _frameBufferGray2;
		private readonly byte[] _frameBufferColoredGray4;
		private bool _lastFrameFailed = false;
		private bool _supportsColoredGray4 = false;

		//private readonly byte[] _lastBuffer;
		//private long _lastTick;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// If device is initialized in raw mode, this is the raw device.
		/// </summary>
		private SerialPort _serialPort;

		// lock object, to protect against closing the serial port while in the
		// middle of a raw write
		private object locker = new object();

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
			_frameBufferRgb24 = new byte[DmdWidth * DmdHeight * 3 + 2];
			_frameBufferRgb24[0] = Rgb24CommandByte;
			_frameBufferRgb24[DmdWidth * DmdHeight * 3 + 1] = Rgb24CommandByte;

			// 4 bits per pixel, 14 control bytes
			_frameBufferGray4 = new byte[DmdWidth * DmdHeight / 2 + 14];     
			_frameBufferGray4[0] = Gray4CommandByte;
			_frameBufferGray4[DmdWidth * DmdHeight / 2 + 13] = Gray4CommandByte;
			
			// 2 bits per pixel, 14 control bytes
			_frameBufferGray2 = new byte[DmdWidth * DmdHeight / 4 + 14];     
			_frameBufferGray2[0] = Gray2CommandByte;
			_frameBufferGray2[DmdWidth * DmdHeight / 4 + 13] = Gray2CommandByte;

			// 16 colors, 4 bytes of pixel, 1 control byte
			_frameBufferColoredGray4 = new byte[DmdWidth * DmdHeight / 4 + 1 + 48];
			_frameBufferColoredGray4[0] = ColoredGray4CommandByte;

			//_lastBuffer = new byte[DmdWidth * DmdHeight * 3 + 2];

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
				Logger.Info("PinDMDv3 device not found.");
				return;
			}

			// TODO decrypt these
			_serialPort.Write(new byte[] { 0x43, 0x13, 0x55, 0xdb, 0x5c, 0x94, 0x4e, 0x0, 0x0, 0x43 }, 0, 10);
			System.Threading.Thread.Sleep(Delay); // duuh...

			var result = new byte[20];
			_serialPort.Read(result, 0, 20); // no idea what this is
		}

		private bool Connect(string port, bool checkFirmware)
		{
			var firmwareRegex = new Regex(@"^rev-vpin-\d+$", RegexOptions.IgnoreCase);
			try {
				Logger.Info("Checking port {0} for PinDMDv3...", port);
				_serialPort = new SerialPort(port, 8176000, Parity.None, 8, StopBits.One);
				_serialPort.Open();
				_serialPort.Write(new byte[] { 0x42, 0x42 }, 0, 2);
				System.Threading.Thread.Sleep(Delay); // duh...

				var result = new byte[100];
				_serialPort.Read(result, 0, 100);
				Firmware = UTF8.GetString(result.Skip(2).TakeWhile(b => b != 0x00).ToArray());
				if (checkFirmware) {
					if (firmwareRegex.IsMatch(Firmware)) {
						Logger.Info("Found PinDMDv3 device on {0}.", port);
						Logger.Debug("   Firmware:    {0}", Firmware);
						Logger.Debug("   Resolution:  {0}x{1}", (int)result[0], (int)result[1]);
						_parseFirmware();
						return true;
					}
				} else {
					Logger.Info("Trusting that PinDMDv3 sits on port {0}.", port);
					Logger.Debug("   Firmware:    {0}", Firmware);
					Logger.Debug("   Resolution:  {0}x{1}", (int)result[0], (int)result[1]);
					_parseFirmware();
					return true;
				}

			} catch (Exception e) {
				Logger.Error("Error: {0}", e.Message.Trim());
				if (_serialPort != null && _serialPort.IsOpen) {
					_serialPort.Close();
				}
			}
			return false;
		}

		public void RenderGray2(byte[] frame)
		{
			// split to sub frames
			var planes = FrameUtil.Split(DmdWidth, DmdHeight, 2, frame);

			// copy to frame buffer
			var changed = FrameUtil.Copy(planes, _frameBufferGray2, 13);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferGray2);
			}
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			// update palette
			SetPalette(frame.Palette);

			// copy to frame buffer
			var changed = FrameUtil.Copy(frame.Planes, _frameBufferGray2, 13);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferGray2);
			}
		}

		public void RenderGray4(byte[] frame)
		{
			// split to sub frames
			var planes = FrameUtil.Split(DmdWidth, DmdHeight, 4, frame);

			// copy to frame buffer
			var changed = FrameUtil.Copy(planes, _frameBufferGray4, 13);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferGray4);
			}
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			// fall back if firmware doesn't support colored gray 4
			if (!_supportsColoredGray4) {
				var rgb24Frame = ColorUtil.ColorizeFrame(DmdWidth, DmdHeight,
					FrameUtil.Join(DmdWidth, DmdHeight, frame.Planes), frame.Palette);
				RenderRgb24(rgb24Frame);
				return;
			}

			// copy palette
			var paletteChanged = false;
			for (var i = 0; i < 16; i++) {
				var color = frame.Palette[i];
				paletteChanged = paletteChanged || (_frameBufferColoredGray4[i + 1] != color.R || _frameBufferColoredGray4[i + 2] != color.G || _frameBufferColoredGray4[i + 3] != color.B);
				_frameBufferColoredGray4[i + 1] = color.R;
				_frameBufferColoredGray4[i + 2] = color.G;
				_frameBufferColoredGray4[i + 3] = color.B;
			}

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferColoredGray4, 49);

			// send frame buffer to device
			if (frameChanged || paletteChanged) {
				RenderRaw(_frameBufferColoredGray4);
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			// copy data to frame buffer
			var changed = FrameUtil.Copy(frame, _frameBufferRgb24, 1);

			// can directly be sent to the device.
			if (changed) {
				RenderRaw(_frameBufferRgb24);
			}
		}

		public void RenderRaw(byte[] data)
		{
			lock (locker) {
				if (_serialPort.IsOpen) {
					//var start = DateTime.Now.Ticks;
					//var lastFrame = start - _lastTick;
					try
					{
						_serialPort.Write(data, 0, data.Length);
						_lastFrameFailed = false;

					} catch (Exception e) {
						if (!_lastFrameFailed) {
							Logger.Error("Error writing to serial port: {0}", e.Message);
							_lastFrameFailed = true;
						}
					}

					/*var ticks = DateTime.Now.Ticks - start;
					var seconds = (double)ticks / TimeSpan.TicksPerSecond;
					Logger.Debug("{0}ms for {1} bytes ({2} baud), {3}ms ({4} fps)", 
						Math.Round((double)ticks / TimeSpan.TicksPerMillisecond * 1000) / 1000, 
						data.Length, 
						(double)data.Length * 8 / seconds,
						Math.Round((double)lastFrame / TimeSpan.TicksPerMillisecond * 1000) / 1000, 
						Math.Round((double)TimeSpan.TicksPerSecond / lastFrame * 1000) / 1000
						);
					_lastTick = start;*/
				}
			}
		}

		public void ClearDisplay()
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

			_frameBufferGray2[1] = color.R; // 100%: red
			_frameBufferGray4[1] = color.R; 
			_frameBufferGray2[2] = color.G; // 100%: green
			_frameBufferGray4[2] = color.G;
			_frameBufferGray2[3] = color.B; // 100%: blue
			_frameBufferGray4[3] = color.B;

			ColorUtil.HslToRgb(hue, saturation, luminosity * 0.66, out r, out g, out b);
			_frameBufferGray2[4] = r;  // 66%: red
			_frameBufferGray4[4] = r;
			_frameBufferGray2[5] = g;  // 66%: green
			_frameBufferGray4[5] = g;
			_frameBufferGray2[6] = b;  // 66%: blue
			_frameBufferGray4[6] = b;

			ColorUtil.HslToRgb(hue, saturation, luminosity * 0.33, out r, out g, out b);
			_frameBufferGray2[7] = r;  // 33%: red
			_frameBufferGray4[7] = r;
			_frameBufferGray2[8] = g;  // 33%: green
			_frameBufferGray4[8] = g;
			_frameBufferGray2[9] = b;  // 33%: blue
			_frameBufferGray4[9] = b;

			_frameBufferGray2[10] = 0x0; // 0%: red
			_frameBufferGray4[10] = 0x0;
			_frameBufferGray2[11] = 0x0; // 0%: green
			_frameBufferGray4[11] = 0x0;
			_frameBufferGray2[12] = 0x0; // 0%: blue
			_frameBufferGray4[12] = 0x0;
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			var palette = ColorUtil.GetPalette(colors, 4);
			var pos = 1;
			for (var i = 0; i < 4; i++) {
				_frameBufferGray2[pos] = palette[3 - i].R;
				_frameBufferGray4[pos] = palette[3 - i].R;
				_frameBufferGray2[pos + 1] = palette[3 - i].G;
				_frameBufferGray4[pos + 1] = palette[3 - i].G;
				_frameBufferGray2[pos + 2] = palette[3 - i].B;
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
			SetColor(RenderGraph.DefaultColor);
		}

		public void Dispose()
		{
			lock (locker) {
				if (_serialPort.IsOpen) {
					_serialPort.Close();
				}
			}
		}

		private void _parseFirmware()
		{
			// parse firmware
			var match = Regex.Match(Firmware, @"REV-vPin-(\d+)$", RegexOptions.IgnoreCase);
			if (match.Success) {
				var revision = Int32.Parse(match.Groups[1].Value);
				Logger.Debug("   Revision:    {0}", revision);
				_supportsColoredGray4 = revision >= 1013;

			} else {
				Logger.Warn("Could not parse revision from firmware.");
			}

			if (_supportsColoredGray4) {
				Logger.Info("Colored 4-bit frames for PinDMDv3 enabled.");
			}
		}
	}
}
