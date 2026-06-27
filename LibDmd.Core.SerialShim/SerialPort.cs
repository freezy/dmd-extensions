using System.Collections.Generic;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace System.IO.Ports
{
	public enum Parity { None = 0, Odd = 1, Even = 2, Mark = 3, Space = 4 }

	public enum StopBits { None = 0, One = 1, Two = 2, OnePointFive = 3 }

	/// <summary>
	/// Minimal SerialPort compatible with the surface LibDmd's Pixelcade / PinDMD3 drivers use,
	/// backed by the native libserialport. Exists so those drivers work under Unity's Mono runtime,
	/// which cannot load the real net6.0+ System.IO.Ports. Not a full SerialPort implementation —
	/// only the members actually called are provided.
	/// </summary>
	public sealed class SerialPort
	{
		public string PortName { get; }
		public int BaudRate { get; set; }
		public Parity Parity { get; set; }
		public int DataBits { get; set; }
		public StopBits StopBits { get; set; }
		public int ReadTimeout { get; set; } = -1;
		public int WriteTimeout { get; set; } = -1;
		public int ReceivedBytesThreshold { get; set; } = 1; // no-op (no event model); kept for source compat
		public bool IsOpen => _port != IntPtr.Zero;

		private bool _dtrEnable;
		public bool DtrEnable {
			get => _dtrEnable;
			set {
				_dtrEnable = value;
				if (_port != IntPtr.Zero) {
					Sp.sp_set_dtr(_port, value ? Sp.SP_DTR_ON : Sp.SP_DTR_OFF);
				}
			}
		}

		private IntPtr _port;

		public SerialPort(string portName)
			: this(portName, 9600, Parity.None, 8, StopBits.One) { }

		public SerialPort(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
		{
			PortName = portName;
			BaudRate = baudRate;
			Parity = parity;
			DataBits = dataBits;
			StopBits = stopBits;
		}

		public static string[] GetPortNames()
		{
			var names = new List<string>();
			if (Sp.sp_list_ports(out var list) != Sp.SP_OK || list == IntPtr.Zero) {
				return names.ToArray();
			}
			try {
				for (var i = 0; ; i++) {
					var port = Marshal.ReadIntPtr(list, i * IntPtr.Size);
					if (port == IntPtr.Zero) {
						break;
					}
					var namePtr = Sp.sp_get_port_name(port);
					if (namePtr != IntPtr.Zero) {
						names.Add(Marshal.PtrToStringAnsi(namePtr));
					}
				}
			} finally {
				Sp.sp_free_port_list(list);
			}
			return names.ToArray();
		}

		public void Open()
		{
			if (_port != IntPtr.Zero) {
				return;
			}
			Check(Sp.sp_get_port_by_name(PortName, out _port), "sp_get_port_by_name");
			var rc = Sp.sp_open(_port, Sp.SP_MODE_READ_WRITE);
			if (rc != Sp.SP_OK) {
				Sp.sp_free_port(_port);
				_port = IntPtr.Zero;
				throw new IOException($"Could not open serial port '{PortName}' (libserialport {rc}).");
			}
			Sp.sp_set_baudrate(_port, BaudRate);
			Sp.sp_set_bits(_port, DataBits);
			Sp.sp_set_parity(_port, (int)Parity);
			Sp.sp_set_stopbits(_port, StopBits == StopBits.Two ? 2 : 1);
			Sp.sp_set_dtr(_port, _dtrEnable ? Sp.SP_DTR_ON : Sp.SP_DTR_OFF);
		}

		public void Close()
		{
			if (_port == IntPtr.Zero) {
				return;
			}
			Sp.sp_close(_port);
			Sp.sp_free_port(_port);
			_port = IntPtr.Zero;
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			EnsureOpen();
			var slice = offset == 0 && count == buffer.Length ? buffer : Slice(buffer, offset, count);
			var written = Sp.sp_blocking_write(_port, slice, new UIntPtr((uint)count), TimeoutOrZero(WriteTimeout));
			if (written < 0) {
				throw new IOException($"Serial write failed (libserialport {written}).");
			}
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			EnsureOpen();
			var temp = offset == 0 ? buffer : new byte[count];
			var read = Sp.sp_blocking_read(_port, temp, new UIntPtr((uint)count), TimeoutOrZero(ReadTimeout));
			if (read < 0) {
				throw new IOException($"Serial read failed (libserialport {read}).");
			}
			if (read == 0) {
				throw new TimeoutException();
			}
			if (offset != 0) {
				Buffer.BlockCopy(temp, 0, buffer, offset, read);
			}
			return read;
		}

		public void DiscardInBuffer()
		{
			if (_port != IntPtr.Zero) {
				Sp.sp_flush(_port, Sp.SP_BUF_INPUT);
			}
		}

		public void DiscardOutBuffer()
		{
			if (_port != IntPtr.Zero) {
				Sp.sp_flush(_port, Sp.SP_BUF_OUTPUT);
			}
		}

		private void EnsureOpen()
		{
			if (_port == IntPtr.Zero) {
				throw new InvalidOperationException("The serial port is not open.");
			}
		}

		private static uint TimeoutOrZero(int timeout) => timeout > 0 ? (uint)timeout : 0u; // 0 == block forever

		private static byte[] Slice(byte[] buffer, int offset, int count)
		{
			var slice = new byte[count];
			Buffer.BlockCopy(buffer, offset, slice, 0, count);
			return slice;
		}

		private static void Check(int rc, string what)
		{
			if (rc != Sp.SP_OK) {
				throw new IOException($"libserialport {what} failed ({rc}).");
			}
		}
	}

	/// <summary>Thin libserialport P/Invoke surface (cdecl on all platforms).</summary>
	internal static class Sp
	{
		private const string Lib = "libserialport";

		public const int SP_OK = 0;
		public const int SP_MODE_READ_WRITE = 3;
		public const int SP_DTR_OFF = 0;
		public const int SP_DTR_ON = 1;
		public const int SP_BUF_INPUT = 1;
		public const int SP_BUF_OUTPUT = 2;

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern int sp_get_port_by_name(string portname, out IntPtr port);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void sp_free_port(IntPtr port);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_list_ports(out IntPtr portList);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void sp_free_port_list(IntPtr ports);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr sp_get_port_name(IntPtr port);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_open(IntPtr port, int flags);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_close(IntPtr port);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_set_baudrate(IntPtr port, int baudrate);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_set_bits(IntPtr port, int bits);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_set_parity(IntPtr port, int parity);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_set_stopbits(IntPtr port, int stopbits);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_set_dtr(IntPtr port, int dtr);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_blocking_write(IntPtr port, byte[] buf, UIntPtr count, uint timeoutMs);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_blocking_read(IntPtr port, byte[] buf, UIntPtr count, uint timeoutMs);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int sp_flush(IntPtr port, int buffers);
	}
}
