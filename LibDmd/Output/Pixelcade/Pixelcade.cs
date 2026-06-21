using System;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.Pixelcade
{
	/// <summary>
	/// Output target for Pixelcade devices.
	/// </summary>
	///
	/// <remarks>
	/// Supports both the original ("v1") boards as well as the "v2" boards that
	/// shipped with new firmware. The protocol variant is auto-detected from the
	/// firmware descriptor returned during the connection handshake:
	///
	/// <list type="bullet">
	///   <item>v1: frame command 0x1F followed by bit-planes (color matrix applied).</item>
	///   <item>v2, firmware &lt; 23: a raw command (<c>[cmd][data]</c>) carrying raw
	///         RGB565 (0x30) or RGB888 (0x40), no plane splitting.</item>
	///   <item>v2, firmware &gt;= 23: the same payloads, but wrapped in a length-prefixed
	///         frame (<c>0xFE 0xFE | len | cmd | data | 0xAA</c>). A one-time 0xEF init
	///         command switches the board into this framed mode.</item>
	/// </list>
	///
	/// This mirrors the reference implementation in libdmdutil
	/// (https://github.com/vpinball/libdmdutil/pull/90).
	/// </remarks>
	/// <see cref="http://pixelcade.org/"/>
	public class Pixelcade : IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		public string Name => "Pixelcade";
		public bool IsAvailable { get; private set; }
		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => false;
		public int Delay { get; set; } = 100;

		/// <summary>
		/// Size of the panel. 128x32 for classic and "X" boards, 64x32 for "M" boards.
		/// Detected during connection; defaults to standard until then.
		/// </summary>
		public Dimensions FixedSize { get; private set; } = Dimensions.Standard;

		public bool DmdAllowHdScaling { get; set; } = true;

		private const int ReadTimeoutMs = 100;
		private const byte ResponseEstablishConnection = 0x00;

		// v1 / v2-pre-23 command bytes
		private const byte RgbLedMatrixFrameCommandByte = 0x1F;
		private const byte RgbLedMatrixEnableCommandByte = 0x1E;

		// v2 command bytes
		private const byte RgbLedMatrixEnableV23CommandByte = 0x2E; // enable command for v2 boards with framed (>=23) firmware
		private const byte V23InitCommandByte = 0xEF;               // one-time init that switches v23+ boards into the framed protocol
		private const byte Rgb565CommandByte = 0x30;
		private const byte Rgb888CommandByte = 0x40;
		private const byte FrameStartMarker = 0xFE;
		private const byte FrameEndDelimiter = 0xAA;

		/// <summary>
		/// Firmware string read from the device if connected
		/// </summary>
		public string Firmware { get; private set; }

		/// <summary>
		/// Manually overriden port name. If set, don't loop through available
		/// ports in order to find the device.
		/// </summary>
		public string Port { get; set; }

		/// <summary>
		/// Color matrix to use. <see cref="ColorMatrix.Rbg"/> swaps the green and blue
		/// channels, which most Pixelcade panels require. Applies to both v1 and v2.
		/// </summary>
		public ColorMatrix ColorMatrix { get; set; } = ColorMatrix.Rgb;

		private static Pixelcade _instance;

		// detected protocol state
		private bool _isV2;
		private int _firmwareVersion;
		private bool _useFraming;

		// v1 wire buffer ([cmd][planes...])
		private byte[] _frameBuffer;

		// v2 scratch buffers (allocated once the size/protocol is known)
		private byte[] _wireBuffer;   // assembled bytes written to the wire
		private byte[] _lastPayload;  // previous payload, for change detection
		private byte[] _swapBuffer;   // holds the green/blue-swapped payload when ColorMatrix is Rbg
		private byte _lastCommand;    // previous payload command, for change detection

		private bool _lastFrameFailed;

		private int MaxDataSize => FixedSize.Surface * 3;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// If device is initialized in raw mode, this is the raw device.
		/// </summary>
		private SerialPort _serialPort;

		/// <summary>
		/// Returns the current instance of the Pixelcade API.
		/// </summary>
		/// <param name="port">Don't loop through available ports but use this COM port name.</param>
		/// <param name="colorMatrix">RGB or RBG</param>
		/// <returns>New or current instance</returns>
		public static Pixelcade GetInstance(string port, ColorMatrix colorMatrix)
		{
			if (_instance == null) {
				_instance = new Pixelcade { Port = port, ColorMatrix = colorMatrix };
			}
			_instance.Init();
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private Pixelcade()
		{
		}

		private void Init()
		{
			if (Port != null && Port.Trim().Length > 0) {
				IsAvailable = Connect(Port);

			} else {
				var ports = SerialPort.GetPortNames();
				foreach (var portName in ports) {
					IsAvailable = Connect(portName);
					if (IsAvailable) {
						break;
					}
				}
			}

			if (!IsAvailable) {
				Logger.Info("Pixelcade device not found.");
				return;
			}

			AllocateBuffers();

			// put the matrix into stream mode
			InitializeMatrix();
		}

		private bool Connect(string port)
		{
			try {
				Logger.Info("Checking port {0} for Pixelcade...", port);

				// since pixelcade is USB, we don't need to set the baud rate, parity bit, etc
				_serialPort = new SerialPort(port);
				_serialPort.ReceivedBytesThreshold = 1;
				_serialPort.DtrEnable = true;
				_serialPort.ReadTimeout = ReadTimeoutMs;
				System.Threading.Thread.Sleep(Delay);
				_serialPort.Open();

				// let's assume opening a connection results in what IncomingState.handleEstablishConnection() receives...
				var result = new byte[1 + 4 + 8 + 8 + 8];
				_serialPort.Read(result, 0, 1 + 4 + 8 + 8 + 8);

				if (result[0] != ResponseEstablishConnection) {
					throw new Exception($"Expected new connection to return 0x0, but got {result[0]}");
				}

				var magic = Encoding.UTF8.GetString(result.Skip(1).Take(4).ToArray());

				if (magic != "IOIO") {
					throw new Exception($"Expected magic code to equal IOIO but got {magic}");
				}

				var hardwareId = Encoding.UTF8.GetString(result.Skip(1+4).Take(8).ToArray());
				var bootloaderId = Encoding.UTF8.GetString(result.Skip(1+4+8).Take(8).ToArray());
				Firmware = Encoding.UTF8.GetString(result.Skip(1+4+8+8).Take(8).ToArray());

				// detect protocol variant, version and panel size from the firmware descriptor
				ParseFirmware(result, 1 + 4 + 8 + 8);

				Logger.Info("Found Pixelcade device on {0}.", port);
				Logger.Debug(" Hardware ID: {0}", hardwareId);
				Logger.Debug(" Bootloader ID: {0}", bootloaderId);
				Logger.Debug(" Firmware: {0}", Firmware);
				Logger.Info(" Detected: size={0}, v2={1}, firmwareVersion={2}, framed={3}", FixedSize, _isV2, _firmwareVersion, _useFraming);
				return true;

			} catch (Exception e) {
				Logger.Error("Error: {0}", e.Message.Trim());
				if (_serialPort != null && _serialPort.IsOpen) {
					_serialPort.Close();
					System.Threading.Thread.Sleep(Delay); // otherwise the next device will fail
				}
			}
			return false;
		}

		/// <summary>
		/// Decodes the 8-byte firmware descriptor into protocol/version/size.
		/// </summary>
		/// <remarks>
		/// Layout (see libdmdutil PR #90):
		/// [0]=='P' marks a descriptor, [2] is the panel size ('X'=128x32, 'M'=64x32),
		/// [3]=='R' marks a v2 board, and [6][7] are the two-digit firmware version.
		/// Anything that doesn't match falls back to a classic 128x32 v1 board.
		/// </remarks>
		private void ParseFirmware(byte[] response, int offset)
		{
			var width = 128;
			var height = 32;
			_isV2 = false;
			_firmwareVersion = 0;

			var f0 = response[offset];
			var f1 = response[offset + 1];
			var f2 = response[offset + 2];
			var f3 = response[offset + 3];
			var f6 = response[offset + 6];
			var f7 = response[offset + 7];

			if (f0 == (byte)'P' && f1 != 0 && f2 != 0 && f3 != 0) {
				if (f2 == (byte)'X') {
					width = 128;
					height = 32;
				} else if (f2 == (byte)'M') {
					width = 64;
					height = 32;
				}

				_isV2 = f3 == (byte)'R';

				if (f6 != 0 && f7 != 0) {
					_firmwareVersion = (f6 - '0') * 10 + (f7 - '0');
				}
			}

			FixedSize = new Dimensions(width, height);
			_useFraming = _isV2 && _firmwareVersion >= 23;
		}

		private void AllocateBuffers()
		{
			if (_isV2) {
				// largest payload is RGB888; framed wire adds 6 bytes of overhead
				_wireBuffer = new byte[MaxDataSize + 6];
				_lastPayload = new byte[MaxDataSize];
				_swapBuffer = new byte[MaxDataSize];
				_lastCommand = 0; // not a valid frame command, so the first frame always sends
			} else {
				_frameBuffer = new byte[FixedSize.Surface * 3 / 2 + 1];
				_frameBuffer[0] = RgbLedMatrixFrameCommandByte;
			}
		}

		private void InitializeMatrix()
		{
			if (_useFraming) {
				// one-time command that switches v23+ boards into the framed protocol
				_serialPort.Write(new[] { V23InitCommandByte }, 0, 1);
			}
			EnableRgbLedMatrix(FixedSize.Width / 32, FixedSize.Height);
		}

		public void RenderRgb24(DmdFrame frameRgb24)
		{
			if (_isV2) {
				// v2 boards accept raw RGB888 directly (R,G,B per pixel), no plane splitting.
				// Honor the color matrix by swapping green/blue for RBG-wired panels.
				var data = ColorMatrix == ColorMatrix.Rbg ? SwapRgb888GreenBlue(frameRgb24.Data) : frameRgb24.Data;
				SendV2Frame(Rgb888CommandByte, data);
				return;
			}

			// convert rgb24 to rgb565
			var frame565 = ColorUtil.ConvertRgb24ToRgb565(FixedSize, frameRgb24.Data, new ushort[FixedSize.Surface]);

			// split into planes to send over the wire
			var newFrame = new byte[FixedSize.Surface * 3 / 2];
			FrameUtil.SplitIntoRgbPlanes(frame565, FixedSize.Width, 16, newFrame, ColorMatrix);

			// copy to frame buffer
			var changed = FrameUtil.Copy(newFrame, _frameBuffer, 1);

			// send to device if changed
			if (changed) {
				RenderRaw(_frameBuffer);
			}
		}


		public void RenderRgb565(DmdFrame frame)
		{
			if (_isV2) {
				// frame.Data is already little-endian RGB565, which is what the v2 firmware expects.
				// Honor the color matrix by swapping green/blue for RBG-wired panels.
				var data = ColorMatrix == ColorMatrix.Rbg ? SwapRgb565GreenBlue(frame.Data) : frame.Data;
				SendV2Frame(Rgb565CommandByte, data);
				return;
			}

			var frame565 = FrameUtil.CastToUShort(frame.Data);

			// split into planes to send over the wire
			var newFrame = new byte[FixedSize.Surface * 3 / 2];
			FrameUtil.SplitIntoRgbPlanes(frame565, FixedSize.Width, 16, newFrame, ColorMatrix);

			// copy to frame buffer
			var changed = FrameUtil.Copy(newFrame, _frameBuffer, 1);

			// send to device if changed
			if (changed) {
				RenderRaw(_frameBuffer);
			}
		}

		/// <summary>
		/// Assembles and sends a v2 payload, skipping frames identical to the last one.
		/// </summary>
		private void SendV2Frame(byte command, byte[] data)
		{
			// FrameUtil.Copy writes data into _lastPayload and reports whether it changed.
			var changed = FrameUtil.Copy(data, _lastPayload, 0) || command != _lastCommand;
			_lastCommand = command;
			if (!changed) {
				return;
			}

			var wireLength = _useFraming
				? BuildFrame(_wireBuffer, command, data, data.Length)
				: BuildRawCommand(_wireBuffer, command, data, data.Length);

			if (wireLength > 0) {
				RenderRaw(_wireBuffer, wireLength);
			}
		}

		/// <summary>
		/// Swaps the green and blue channels of an RGB888 buffer into <see cref="_swapBuffer"/>.
		/// Equivalent to <see cref="ColorMatrix.Rbg"/> for the v2 raw path. Never mutates the
		/// source, which may be shared with other destinations (e.g. the virtual DMD).
		/// </summary>
		private byte[] SwapRgb888GreenBlue(byte[] src)
		{
			for (var i = 0; i + 2 < src.Length; i += 3) {
				_swapBuffer[i] = src[i];         // R
				_swapBuffer[i + 1] = src[i + 2]; // G <- B
				_swapBuffer[i + 2] = src[i + 1]; // B <- G
			}
			return _swapBuffer;
		}

		/// <summary>
		/// Swaps the green and blue channels of a little-endian RGB565 buffer into
		/// <see cref="_swapBuffer"/>, adjusting for the differing channel bit widths.
		/// </summary>
		private byte[] SwapRgb565GreenBlue(byte[] src)
		{
			for (var i = 0; i + 1 < src.Length; i += 2) {
				var v = (ushort)(src[i] | (src[i + 1] << 8));
				var r = (v >> 11) & 0x1F;
				var g = (v >> 5) & 0x3F;
				var b = v & 0x1F;
				var newG = (b << 1) | (b >> 4); // 5-bit blue -> 6-bit green slot
				var newB = g >> 1;              // 6-bit green -> 5-bit blue slot
				var swapped = (ushort)((r << 11) | (newG << 5) | newB);
				_swapBuffer[i] = (byte)(swapped & 0xFF);
				_swapBuffer[i + 1] = (byte)(swapped >> 8);
			}
			return _swapBuffer;
		}

		/// <summary>
		/// Builds a length-prefixed frame: <c>0xFE 0xFE | len_lo len_hi | cmd | data | 0xAA</c>.
		/// </summary>
		/// <returns>Number of bytes written, or -1 if it doesn't fit.</returns>
		private int BuildFrame(byte[] buffer, byte command, byte[] data, int dataLength)
		{
			var frameSize = 6 + dataLength;
			if (frameSize > buffer.Length || dataLength > MaxDataSize) {
				return -1;
			}

			var payloadLength = (ushort)(1 + dataLength);
			buffer[0] = FrameStartMarker;
			buffer[1] = FrameStartMarker;
			buffer[2] = (byte)(payloadLength & 0xFF);
			buffer[3] = (byte)((payloadLength >> 8) & 0xFF);
			buffer[4] = command;
			if (dataLength > 0) {
				Buffer.BlockCopy(data, 0, buffer, 5, dataLength);
			}
			buffer[5 + dataLength] = FrameEndDelimiter;
			return frameSize;
		}

		/// <summary>
		/// Builds an unframed command: <c>cmd | data</c>.
		/// </summary>
		/// <returns>Number of bytes written, or -1 if it doesn't fit.</returns>
		private int BuildRawCommand(byte[] buffer, byte command, byte[] data, int dataLength)
		{
			var commandSize = 1 + dataLength;
			if (commandSize > buffer.Length || dataLength > MaxDataSize) {
				return -1;
			}

			buffer[0] = command;
			if (dataLength > 0) {
				Buffer.BlockCopy(data, 0, buffer, 1, dataLength);
			}
			return commandSize;
		}

		public void RenderRaw(byte[] data)
		{
			RenderRaw(data, data.Length);
		}

		public void RenderRaw(byte[] data, int length)
		{
			try {
				_serialPort.Write(data, 0, length);
				_lastFrameFailed = false;

			} catch (Exception e) {
				if (!_lastFrameFailed) {
					Logger.Error("Error writing to serial port: {0}", e.Message);
					_lastFrameFailed = true;
				}
			}
		}

		public void ClearDisplay()
		{
			if (_serialPort == null || !_serialPort.IsOpen) {
				return;
			}

			if (_isV2) {
				// send a black RGB565 frame in the active wire format
				var blank = new byte[FixedSize.Surface * 2];
				var wireLength = _useFraming
					? BuildFrame(_wireBuffer, Rgb565CommandByte, blank, blank.Length)
					: BuildRawCommand(_wireBuffer, Rgb565CommandByte, blank, blank.Length);
				if (wireLength > 0) {
					_serialPort.Write(_wireBuffer, 0, wireLength);
				}
				_lastCommand = 0; // force the next frame to be sent
				return;
			}

			for (var i = 1; i < _frameBuffer.Length - 1; i++) {
				_frameBuffer[i] = 0;
			}
			_serialPort.Write(_frameBuffer, 0, _frameBuffer.Length);
		}

		public void SetColor(Color color)
		{
			// no palette support here.
		}

		public void SetPalette(Color[] colors)
		{
			// no palette support here.
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
			if (_serialPort != null && _serialPort.IsOpen) {
				_serialPort.Close();
			}
		}

		private void EnableRgbLedMatrix(int shifterLen32, int rows)
		{
			var configData = (byte)(shifterLen32 & 0x0F | ((rows == 8 ? 0 : 1) << 4));

			if (_isV2) {
				var command = _useFraming ? RgbLedMatrixEnableV23CommandByte : RgbLedMatrixEnableCommandByte;
				var buffer = new byte[8];
				var data = new[] { configData };
				var length = _useFraming
					? BuildFrame(buffer, command, data, 1)
					: BuildRawCommand(buffer, command, data, 1);
				if (length > 0) {
					_serialPort.Write(buffer, 0, length);
				}
			} else {
				_serialPort.Write(new[] {
					RgbLedMatrixEnableCommandByte,
					configData
				}, 0, 2);
			}
		}
	}
}
