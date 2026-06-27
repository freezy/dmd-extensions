using System;
using System.Runtime.InteropServices;
using System.Text;
using LibDmd.Native;

namespace LibDmd.Output.PinDmd2
{
	/// <summary>
	/// Cross-platform libusb-1.0 USB transport for <see cref="PinDmd2"/> (the LibDmd.Core
	/// counterpart to the legacy LibUsbDotNet 2.x path in <c>PinDmd2.cs</c>, which is
	/// <c>#if !LIBDMD_CORE</c>). PinDMD2 shares the PIN2DMD vendor/product id and is told apart
	/// by its product string ("pinDMD V2").
	/// </summary>
	public partial class PinDmd2
	{
		private IntPtr _usbContext;
		private IntPtr _deviceHandle;

		private const ushort VendorId = 0x0314;
		private const ushort ProductId = 0xe457;

		public void Init()
		{
			if (LibUsb.libusb_init(out _usbContext) != LibUsb.Success) {
				Logger.Warn("[PinDMDv2] libusb_init failed.");
				IsAvailable = false;
				return;
			}

			var count = (long)LibUsb.libusb_get_device_list(_usbContext, out var list);
			if (count < 0 || list == IntPtr.Zero) {
				Logger.Warn("[PinDMDv2] libusb_get_device_list failed ({0}).", count);
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
							? "[PinDMDv2] Found device but access was denied (add a udev rule on Linux)."
							: $"[PinDMDv2] Found device but libusb_open failed ({error}).");
						continue;
					}

					var product = GetStringDescriptor(desc.iProduct);
					if (product.IndexOf("pinDMD V2", StringComparison.Ordinal) < 0) {
						Logger.Info("Device found but it's not a PinDMDv2 device ({0}).", product);
						LibUsb.libusb_close(_deviceHandle);
						_deviceHandle = IntPtr.Zero;
						continue;
					}

					Logger.Info("Found PinDMDv2 device.");
					LibUsb.libusb_set_configuration(_deviceHandle, 1);
					LibUsb.libusb_claim_interface(_deviceHandle, 0);
					IsAvailable = true;
					return;
				}

				Logger.Info("PinDMDv2 device not found.");
				IsAvailable = false;
			} catch (Exception e) {
				IsAvailable = false;
				Logger.Warn(e, "Probing PinDMDv2 failed, skipping.");
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

		public void RenderRaw(byte[] data)
		{
			lock (locker) {
				if (_deviceHandle == IntPtr.Zero) {
					Logger.Warn("Ignoring frame for already closed USB device.");
					return;
				}
				try {
					var error = LibUsb.libusb_bulk_transfer(_deviceHandle, LibUsb.EndpointOut, data, data.Length, out _, 2000);
					if (error != LibUsb.Success) {
						Logger.Error("Error sending data to device: libusb error {0}", error);
					}
				} catch (Exception e) {
					IsAvailable = false;
					Logger.Error(e, "Error sending data to PinDMDv2: {0}", e.Message);
				}
			}
		}

		public void Dispose()
		{
			lock (locker) {
				if (_deviceHandle != IntPtr.Zero) {
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
}
