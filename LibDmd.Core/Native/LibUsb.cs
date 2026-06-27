using System;
using System.Runtime.InteropServices;

namespace LibDmd.Native
{
	/// <summary>
	/// Minimal libusb-1.0 P/Invoke surface for the PIN2DMD driver: enumerate, open, claim
	/// interface, and bulk transfer. Deliberately a thin direct binding (no managed USB
	/// dependency) so it stays netstandard2.1-clean and IL2CPP-safe, consistent with how
	/// libserum/libzedmd are bound. The native library ("libusb-1.0") is resolved per-OS by
	/// <see cref="NativeLibraryLoader"/> on .NET and by Unity's Plugins folders under netstandard2.1.
	/// </summary>
	internal static class LibUsb
	{
		private const string Lib = "libusb-1.0";

		public const int Success = 0;
		public const int ErrorAccess = -3; // LIBUSB_ERROR_ACCESS (typically a missing udev rule on Linux)

		// Endpoint addresses (direction in bit 7): OUT = 0x01, IN = 0x81.
		public const byte EndpointOut = 0x01;
		public const byte EndpointIn = 0x81;

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct DeviceDescriptor
		{
			public byte bLength;
			public byte bDescriptorType;
			public ushort bcdUSB;
			public byte bDeviceClass;
			public byte bDeviceSubClass;
			public byte bDeviceProtocol;
			public byte bMaxPacketSize0;
			public ushort idVendor;
			public ushort idProduct;
			public ushort bcdDevice;
			public byte iManufacturer;
			public byte iProduct;
			public byte iSerialNumber;
			public byte bNumConfigurations;
		}

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_init(out IntPtr ctx);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void libusb_exit(IntPtr ctx);

		/// <summary>Returns the device count (ssize_t) or a negative error; fills <paramref name="list"/>.</summary>
		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr libusb_get_device_list(IntPtr ctx, out IntPtr list);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void libusb_free_device_list(IntPtr list, int unrefDevices);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_get_device_descriptor(IntPtr dev, out DeviceDescriptor desc);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_open(IntPtr dev, out IntPtr handle);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void libusb_close(IntPtr handle);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_set_configuration(IntPtr handle, int configuration);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_claim_interface(IntPtr handle, int interfaceNumber);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_release_interface(IntPtr handle, int interfaceNumber);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_bulk_transfer(IntPtr handle, byte endpoint, byte[] data, int length, out int transferred, uint timeout);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int libusb_get_string_descriptor_ascii(IntPtr handle, byte descIndex, byte[] data, int length);
	}
}
