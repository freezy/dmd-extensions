using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LibDmd.Native;

namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Cross-platform USB transport for <see cref="Pin2DmdBase"/>, built on a thin direct
	/// libusb-1.0 binding (see <see cref="LibUsb"/>). This is the LibDmd.Core counterpart to the
	/// legacy LibUsbDotNet 2.x path in <c>Pin2DmdBase.cs</c> (which is <c>#if !LIBDMD_CORE</c>);
	/// the two never compile together.
	/// </summary>
	public abstract partial class Pin2DmdBase
	{
		private IntPtr _usbContext;
		private IntPtr _deviceHandle;

		private const ushort VendorId = 0x0314;
		private const ushort ProductId = 0xe457;

		public void Init()
		{
			if (LibUsb.libusb_init(out _usbContext) != LibUsb.Success) {
				Logger.Warn("[PIN2DMD] libusb_init failed.");
				IsAvailable = false;
				return;
			}

			var count = (long)LibUsb.libusb_get_device_list(_usbContext, out var list);
			if (count < 0 || list == IntPtr.Zero) {
				Logger.Warn("[PIN2DMD] libusb_get_device_list failed ({0}).", count);
				IsAvailable = false;
				return;
			}

			try {
				for (long i = 0; i < count; i++) {
					var dev = Marshal.ReadIntPtr(list, (int)(i * IntPtr.Size));
					if (dev == IntPtr.Zero
						|| LibUsb.libusb_get_device_descriptor(dev, out var desc) != LibUsb.Success
						|| desc.idVendor != VendorId
						|| (desc.idProduct & 0xFFFF) != ProductId) {
						continue;
					}

					var error = LibUsb.libusb_open(dev, out _deviceHandle);
					if (error != LibUsb.Success || _deviceHandle == IntPtr.Zero) {
						_deviceHandle = IntPtr.Zero;
						Logger.Warn(error == LibUsb.ErrorAccess
							? "[PIN2DMD] Found device but access was denied (add a udev rule on Linux)."
							: $"[PIN2DMD] Found device but libusb_open failed ({error}).");
						continue;
					}

					var product = GetStringDescriptor(desc.iProduct);
					if (!string.Equals(product, ProductString, StringComparison.Ordinal)) {
						Logger.Debug($"[PIN2DMD] Device found but product '{product}' is not '{ProductString}'.");
						LibUsb.libusb_close(_deviceHandle);
						_deviceHandle = IntPtr.Zero;
						continue;
					}

					Logger.Info($"Found {ProductString} device.");
					LibUsb.libusb_set_configuration(_deviceHandle, 1);
					LibUsb.libusb_claim_interface(_deviceHandle, 0);
					ReadConfig();
					InitFrameBuffers();
					IsAvailable = true;
					return;
				}

				Logger.Info($"[PIN2DMD] No {ProductString} device found.");
				IsAvailable = false;
			} catch (Exception e) {
				IsAvailable = false;
				Logger.Warn(e, "Probing PIN2DMD failed, skipping.");
			} finally {
				LibUsb.libusb_free_device_list(list, 1);
			}
		}

		private string GetStringDescriptor(byte index)
		{
			if (index == 0 || _deviceHandle == IntPtr.Zero) {
				return string.Empty;
			}
			var buffer = new byte[256];
			var length = LibUsb.libusb_get_string_descriptor_ascii(_deviceHandle, index, buffer, buffer.Length);
			return length > 0 ? Encoding.ASCII.GetString(buffer, 0, length) : string.Empty;
		}

		public void RenderRaw(byte[] frame)
		{
			if (_deviceHandle == IntPtr.Zero) {
				return;
			}
			try {
				var error = LibUsb.libusb_bulk_transfer(_deviceHandle, LibUsb.EndpointOut, frame, frame.Length, out _, 2000);
				if (error != LibUsb.Success) {
					Logger.Error("Error sending data to device: libusb error {0}", error);
				}
			} catch (Exception e) {
				Logger.Error(e, $"Error sending data to {ProductString}: {e.Message}");
			}
		}

		public void ReadConfig()
		{
			if (_deviceHandle == IntPtr.Zero) {
				return;
			}
			try {
				var frame = new byte[2052];
				frame[0] = 0x81;
				frame[1] = 0xc3;
				frame[2] = 0xe7;
				frame[3] = 0xff; // cmd
				frame[4] = 0x10;
				var error = LibUsb.libusb_bulk_transfer(_deviceHandle, LibUsb.EndpointOut, frame, frame.Length, out _, 2000);
				if (error != LibUsb.Success) {
					Logger.Error("Error sending data to device: libusb error {0}", error);
				}
			} catch (Exception e) {
				Logger.Error(e, $"Error sending data to {ProductString}: {e.Message}");
			}
			try {
				var config = new byte[64];
				var error = LibUsb.libusb_bulk_transfer(_deviceHandle, LibUsb.EndpointIn, config, config.Length, out _, 2000);
				if (error != LibUsb.Success) {
					Logger.Error("Error reading config from device: libusb error {0}", error);
				} else {
					pin2dmdConfig = ReadConfigUsingPointer(config);
				}
			} catch (Exception e) {
				Logger.Error(e, $"Error reading config from {ProductString}: {e.Message}");
			}
		}

		public void Dispose()
		{
			if (_deviceHandle != IntPtr.Zero) {
				// reset settings
				var buffer = new byte[2052];
				buffer[0] = 0x81;
				buffer[1] = 0xC3;
				buffer[2] = 0xE7;
				buffer[3] = 0xFF;
				buffer[4] = 0x07;
				RenderRaw(buffer);
				Thread.Sleep(Delay);

				LibUsb.libusb_release_interface(_deviceHandle, 0);
				LibUsb.libusb_close(_deviceHandle);
				_deviceHandle = IntPtr.Zero;
			}
			if (_usbContext != IntPtr.Zero) {
				LibUsb.libusb_exit(_usbContext);
				_usbContext = IntPtr.Zero;
			}
		}
	}
}
