using System;
using System.Text;
using LibDmd.Native;

namespace LibDmd.Output.PinDmd1
{
	/// <summary>
	/// Cross-platform FTDI (D2XX) transport for <see cref="PinDmd1"/>, built on a thin direct
	/// <see cref="Ftd2xx"/> P/Invoke binding. This is the LibDmd.Core counterpart to the legacy
	/// <c>FTD2XX_NET</c> path in <c>PinDmd1.cs</c> (which is <c>#if !LIBDMD_CORE</c>); the device is
	/// identified by its FTDI serial number ("DMD1000"/"DMD1001").
	/// </summary>
	public partial class PinDmd1
	{
		private IntPtr _ftHandle;

		public void Init()
		{
			if (Ftd2xx.FT_CreateDeviceInfoList(out var count) != Ftd2xx.Ok || count == 0) {
				Logger.Info("PinDMDv1 device not found.");
				return;
			}

			for (uint i = 0; i < count; i++) {
				var serialBuffer = new byte[16];
				var descriptionBuffer = new byte[64];
				if (Ftd2xx.FT_GetDeviceInfoDetail(i, out _, out _, out _, out _, serialBuffer, descriptionBuffer, out _) != Ftd2xx.Ok) {
					continue;
				}

				var serialNumber = AsciiZ(serialBuffer);
				if (serialNumber != "DMD1000" && serialNumber != "DMD1001") {
					continue;
				}

				Logger.Info("Found PinDMDv1 device.");
				Logger.Debug("   Serial Number: {0}", serialNumber);
				Logger.Debug("   Description:   {0}", AsciiZ(descriptionBuffer));

				if (Ftd2xx.FT_OpenEx(serialNumber, Ftd2xx.OpenBySerialNumber, out _ftHandle) != Ftd2xx.Ok || _ftHandle == IntPtr.Zero) {
					Logger.Error("Failed to open PinDMDv1 (driver/permissions; on macOS the Apple VCP driver can claim it).");
					_ftHandle = IntPtr.Zero;
					return;
				}

				if (Ftd2xx.FT_SetBitMode(_ftHandle, 0xff, 0x1) != Ftd2xx.Ok) {
					Logger.Error("Failed to set bit mode.");
					Dispose();
					return;
				}
				if (Ftd2xx.FT_SetBaudRate(_ftHandle, 12000) != Ftd2xx.Ok) {
					Logger.Error("Failed to set baud rate.");
					Dispose();
					return;
				}

				IsAvailable = true;
				Logger.Info("Connected to PinDMDv1.");
				return;
			}

			Logger.Info("PinDMDv1 device not found.");
		}

		private static string AsciiZ(byte[] buffer)
		{
			var end = Array.IndexOf(buffer, (byte)0);
			return Encoding.ASCII.GetString(buffer, 0, end < 0 ? buffer.Length : end);
		}

		public void RenderRaw(byte[] data)
		{
			lock (locker) {
				if (_ftHandle == IntPtr.Zero) {
					return;
				}
				var status = Ftd2xx.FT_Write(_ftHandle, data, (uint)data.Length, out _);
				if (status != Ftd2xx.Ok) {
					Logger.Error("Error writing to FTDI device: " + status);
				}
			}
		}

		public void Dispose()
		{
			lock (locker) {
				if (_ftHandle != IntPtr.Zero) {
					Ftd2xx.FT_SetBitMode(_ftHandle, 0x00, 0x0);
					Ftd2xx.FT_Close(_ftHandle);
					_ftHandle = IntPtr.Zero;
					IsAvailable = false;
				}
			}
		}
	}
}
