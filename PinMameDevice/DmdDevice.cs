﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using NLog;

namespace PinMameDevice
{
	/// <summary>
	/// Äs DLL womr cha ubr C/C++ inäladä und wo vo VPinMAME bruicht wird um DMD
	/// datä z schickä. Drbi wird äs API implementiärt.
	/// </summary>
	/// <remarks>
	/// Diä Klass beinhautet fasch kä Logik sondrn tuät fascht auäs diräkt a
	/// <see cref="LibDmd.DmdDevice.DmdDevice"/> weytrleitä.
	/// </remarks>
	/// <see href="https://sourceforge.net/p/pinmame/code/HEAD/tree/trunk/ext/dmddevice/dmddevice.h"/>
	public static class DmdDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly DeviceInstance DefaultDevice = new DeviceInstance();
		private static readonly List<DeviceInstance> DmdDevices = new List<DeviceInstance>();

		private class DeviceInstance
		{
			public int Id;
			public IDmdDevice DmdDevice { get; } = new LibDmd.DmdDevice.DmdDevice();
			public DmdFrame DmdFrame { get; } = new DmdFrame();
			public RawFrame RawDmdFrame { get; } = new RawFrame();
			public LinkedList<char> CData { get; } = new LinkedList<char>();
		}

		static DmdDevice()
		{
			DefaultDevice.Id = 0;
			DmdDevices.Add(DefaultDevice);
		}

		private static DeviceInstance GetDevice(int id)
		{
			if (id < 0 || id >= DmdDevices.Count || DmdDevices[id] == null) {
				throw new Exception("[vpm] Invalid device id requested: " + id);
			}
			return DmdDevices.ElementAt(id);
		}

		#region Device-Aware API

		// int Create_Device()
		[DllExport("Create_Device", CallingConvention = CallingConvention.Cdecl)]
		public static int CreateDevice()
		{
			for (int i = 0; i < DmdDevices.Count; i++)
			{
				if (DmdDevices[i] == null)
				{
					DmdDevices[i] = new DeviceInstance {
						Id = i
					};
					Logger.Info("[dll] Create(): New output id is {0}", i);
					return i;
				}
			}
			var device = new DeviceInstance {
				Id = DmdDevices.Count
			};
			DmdDevices.Add(device);
			Logger.Info("[dll] Create(): New output id is {0}", device.Id);
			return device.Id;
		}

		// int Open_Device(int id)
		[DllExport("Open_Device", CallingConvention = CallingConvention.Cdecl)]
		public static int OpenDevice(int id) => InternalOpenDevice(GetDevice(id));

		// bool Close_Device(int id)
		[DllExport("Close_Device", CallingConvention = CallingConvention.Cdecl)]
		public static bool CloseDevice(int id) => InternalCloseDevice(GetDevice(id));

		// void PM_GameSettings_Device(int id, const char* GameName, UINT64 HardwareGeneration, const PMoptions &Options)
		[DllExport("PM_GameSettings_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void GameSettingsDevice(int id, string gameName, ulong hardwareGeneration, IntPtr options) => InternalGameSettingsDevice(GetDevice(id), gameName, hardwareGeneration, options);

		// void Console_Data(UINT8 data)
		[DllExport("Console_Data_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void ConsoleDataDevice(int id, byte data) => InternalConsoleDataDevice(GetDevice(id), data);

		// void Render_RGB24_Device(int id, UINT16 width, UINT16 height, Rgb24 *currbuffer)
		[DllExport("Render_RGB24_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderRgb24Device(int id, ushort width, ushort height, IntPtr currbuffer) => InternalRenderRgb24Device(GetDevice(id), width, height, currbuffer);

		// void Render_16_Shades_Device(int id, UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_16_Shades_with_Raw_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderRaw4Device(int id, ushort width, ushort height, IntPtr currbuffer, ushort noOfRawFrames, IntPtr currrawbuffer) => InternalRenderRaw4Device(GetDevice(id), width, height, currbuffer, noOfRawFrames, currrawbuffer);
				
		// void Render_4_Shades_Device(int id, UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_4_Shades_with_Raw_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderRaw2Device(int id, ushort width, ushort height, IntPtr currbuffer, ushort noOfRawFrames, IntPtr currrawbuffer) => InternalRenderRaw2Device(GetDevice(id), width, height, currbuffer, noOfRawFrames, currrawbuffer);

		// void Render_16_Shades_Device(int id, UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_16_Shades_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderGray4Device(int id, ushort width, ushort height, IntPtr currbuffer) => InternalRenderGray4Device(GetDevice(id), width, height, currbuffer);

		// void Render_4_Shades_Device(int id, UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_4_Shades_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderGray2Device(int id, ushort width, ushort height, IntPtr currbuffer) => InternalRenderGray2Device(GetDevice(id), width, height, currbuffer);

		//  void Render_PM_Alphanumeric_Frame_Device(int id, NumericalLayout numericalLayout, const UINT16 *const seg_data, const UINT16 *const seg_data2)
		[DllExport("Render_PM_Alphanumeric_Frame_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderAlphaNumDevice(int id, NumericalLayout numericalLayout, IntPtr seg_data, IntPtr seg_data2) => InternalRenderAlphaNumDevice(GetDevice(id), numericalLayout, seg_data, seg_data2);

		// void Set_4_Colors_Palette_Device(int id, Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100)
		[DllExport("Set_4_Colors_Palette_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void SetGray2PaletteDevice(int id, Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100) => InternalSetGray2PaletteDevice(GetDevice(id), color0, color33, color66, color100);

		// void Set_16_Colors_Palette_Device(int id, Rgb24 *color)
		[DllExport("Set_16_Colors_Palette_Device", CallingConvention = CallingConvention.Cdecl)]
		public static void SetGray4PaletteDevice(int id, IntPtr palette) => InternalSetGray4PaletteDevice(GetDevice(id), palette);

		#endregion

		#region Global Device API

		// int Open()
		[DllExport("Open", CallingConvention = CallingConvention.Cdecl)]
		public static int Open() => InternalOpenDevice(DefaultDevice);

		// bool Close()
		[DllExport("Close", CallingConvention = CallingConvention.Cdecl)]
		public static bool Close() => InternalCloseDevice(DefaultDevice);

		// void PM_GameSettings(const char* GameName, UINT64 HardwareGeneration, const PMoptions &Options)
		[DllExport("PM_GameSettings", CallingConvention = CallingConvention.Cdecl)]
		public static void GameSettings(string gameName, ulong hardwareGeneration, IntPtr options) => InternalGameSettingsDevice(DefaultDevice, gameName, hardwareGeneration, options);

		// void Console_Data(UINT8 data)
		[DllExport("Console_Data", CallingConvention = CallingConvention.Cdecl)]
		public static void ConsoleData(byte data) => InternalConsoleDataDevice(DefaultDevice, data);

		// void Render_RGB24(UINT16 width, UINT16 height, Rgb24 *currbuffer)
		[DllExport("Render_RGB24", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderRgb24(ushort width, ushort height, IntPtr currbuffer) => InternalRenderRgb24Device(DefaultDevice, width, height, currbuffer);

		// void Render_16_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_16_Shades_with_Raw", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderRaw4(ushort width, ushort height, IntPtr currbuffer, ushort noOfRawFrames, IntPtr currrawbuffer) => InternalRenderRaw4Device(DefaultDevice, width, height, currbuffer, noOfRawFrames, currrawbuffer);

		// void Render_4_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_4_Shades_with_Raw", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderRaw2(ushort width, ushort height, IntPtr currbuffer, ushort noOfRawFrames, IntPtr currrawbuffer) => InternalRenderRaw2Device(DefaultDevice, width, height, currbuffer, noOfRawFrames, currrawbuffer);

		// void Render_16_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_16_Shades", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderGray4(ushort width, ushort height, IntPtr currbuffer) => InternalRenderGray4Device(DefaultDevice, width, height, currbuffer);

		// void Render_4_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_4_Shades", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderGray2(ushort width, ushort height, IntPtr currbuffer) => InternalRenderGray2Device(DefaultDevice, width, height, currbuffer);

		//  void Render_PM_Alphanumeric_Frame(NumericalLayout numericalLayout, const UINT16 *const seg_data, const UINT16 *const seg_data2)
		[DllExport("Render_PM_Alphanumeric_Frame", CallingConvention = CallingConvention.Cdecl)]
		public static void RenderAlphaNum(NumericalLayout numericalLayout, IntPtr seg_data, IntPtr seg_data2) => InternalRenderAlphaNumDevice(DefaultDevice, numericalLayout, seg_data, seg_data2);

		// void Set_4_Colors_Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100)
		[DllExport("Set_4_Colors_Palette", CallingConvention = CallingConvention.Cdecl)]
		public static void SetGray2Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100) => InternalSetGray2PaletteDevice(DefaultDevice, color0, color33, color66, color100);

		// void Set_16_Colors_Palette(Rgb24 *color)
		[DllExport("Set_16_Colors_Palette", CallingConvention = CallingConvention.Cdecl)]
		public static void SetGray4Palette(IntPtr palette) => InternalSetGray4PaletteDevice(DefaultDevice, palette);

		#endregion

		#region API Implementation

		private static int InternalOpenDevice(DeviceInstance device)
		{
			Logger.Info("[dll] Open({0})", device.Id);
			return 1;
		}

		private static bool InternalCloseDevice(DeviceInstance device)
		{
			Logger.Info("[dll] Close({0})", device.Id);
			device.DmdDevice.Close();
			if (device != DefaultDevice) {
				DmdDevices[device.Id] = null;
			}
			return true;
		}

		private static void InternalGameSettingsDevice(DeviceInstance device, string gameName, ulong hardwareGeneration, IntPtr options)
		{
			var opt = (PMoptions)Marshal.PtrToStructure(options, typeof(PMoptions));
			Logger.Info("[dll] PM_GameSettings({0}, {1}, {2})", device.Id, gameName, opt.Colorize);
			device.DmdDevice.SetColorize(opt.Colorize != 0);
			device.DmdDevice.SetGameName(gameName);
			device.DmdDevice.SetColor(Color.FromRgb((byte)(opt.Red), (byte)(opt.Green), (byte)(opt.Blue)));
			device.DmdDevice.Init();
		}

		private static void InternalConsoleDataDevice(DeviceInstance device, byte data)
		{
			device.DmdDevice.ConsoleData(data);

			// Dä schickt immr eis Byte abr eigentlich wettr Bleck vo viär Bytes,
			// d.h miär mind ihs merkä was diä letschtä drii Bytes gsi sind um eppis
			// schlays chenna witr z schickä.
			// Wemmer diä viär Bytes de mau hett isch dr erschti Wärt immer äs P und
			// diä zwe druif sind Textzeichä womr i Hex muäss umwandlä. Am Schluss
			// chunnt de nu ä ni i Zihlä.
			device.CData.AddLast((char)data);
			if (device.CData.Count <= 4)
			{
				// het nunig aui wärt
				return;
			}
			device.CData.RemoveFirst();
			if (device.CData.First.Value != 'P') {
				return;
			}

			var num = new string(new[] { device.CData.First.Next.Value, device.CData.First.Next.Next.Value });
			try {
				device.DmdDevice.LoadPalette(Convert.ToUInt32(num, 16));
				Logger.Info($"[dll] Switch to palette {num}.");

			} catch (FormatException e) {
				Logger.Warn(e, "[dll] Could not parse \"{0}\" as hex number.", num);
			}
		}

		private static void InternalRenderRgb24Device(DeviceInstance device, ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height * 3;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			device.DmdDevice.RenderRgb24(device.DmdFrame.Update(new Dimensions(width, height), frame, 24));
		}

		private static void InternalRenderRaw4Device(DeviceInstance device, ushort width, ushort height, IntPtr currbuffer, ushort noOfRawFrames, IntPtr currrawbuffer)
		{
			noOfRawFrames = Math.Min(noOfRawFrames, (ushort)4);
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);

			var rawplanes = new byte[noOfRawFrames][];
			var planeSize = frameSize / 8;
			for (int i = 0; i < noOfRawFrames; i++)
			{
				rawplanes[i] = new byte[planeSize];
				Marshal.Copy(new IntPtr(currrawbuffer.ToInt64() + (i * planeSize)), rawplanes[i], 0, planeSize);
			}
			device.DmdDevice.RenderGray4(device.RawDmdFrame.Update(new Dimensions(width, height), frame, rawplanes, Array.Empty<byte[]>()));
		}

		private static void InternalRenderRaw2Device(DeviceInstance device, ushort width, ushort height, IntPtr currBuffer, ushort noOfRawPlanes, IntPtr currrawbuffer)
		{
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currBuffer, frame, 0, frameSize);
			var rawPlanes = new byte[2][];
			var rawExtraPlanes = new byte[noOfRawPlanes - 2][];
			var planeSize = frameSize / 8;
			for (int i = 0; i < noOfRawPlanes; i++)
			{
				var dest = i > 1 ? rawExtraPlanes : rawPlanes;
				var pos = i > 1 ? i - 2 : i;
				dest[pos] = new byte[planeSize];
				Marshal.Copy(new IntPtr(currrawbuffer.ToInt64() + (i * planeSize)), dest[pos], 0, planeSize);
			}
			device.DmdDevice.RenderGray2(device.RawDmdFrame.Update(new Dimensions(width, height), frame, rawPlanes, rawExtraPlanes));
		}

		private static void InternalRenderGray4Device(DeviceInstance device, ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			device.DmdDevice.RenderGray4(device.DmdFrame.Update(new Dimensions(width, height), frame, 4));
		}

		private static void InternalRenderGray2Device(DeviceInstance device, ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			device.DmdDevice.RenderGray2(device.DmdFrame.Update(new Dimensions(width, height), frame, 2));
		}

		private static void InternalRenderAlphaNumDevice(DeviceInstance device, NumericalLayout numericalLayout, IntPtr seg_data, IntPtr seg_data2)
		{
			device.DmdDevice.RenderAlphaNumeric(numericalLayout, InteropUtil.ReadUInt16Array(seg_data, 64), InteropUtil.ReadUInt16Array(seg_data2, 64));
		}

		private static void InternalSetGray2PaletteDevice(DeviceInstance device, Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100)
		{
			Logger.Info($"[dll] Set_4_Colors_Palette({device.Id}, {color0}, {color33}, {color66}, {color100})");
			device.DmdDevice.SetPalette(new[] {
				ConvertColor(color0),
				ConvertColor(color33),
				ConvertColor(color66),
				ConvertColor(color100)
			});
		}

		private static void InternalSetGray4PaletteDevice(DeviceInstance device, IntPtr palette)
		{
			Logger.Info("[dll] Set_16_Colors_Palette({0},...)", device.Id);

			byte[] p = new byte[48];
			Color[] colors = new Color[16];
			Marshal.Copy(palette, p, 0, 48);
			for (var i = 0; i < 16; i++) {
				colors[i] = Color.FromRgb(p[i*3], p[i*3+1], p[i*3+2]);
			}
			device.DmdDevice.SetPalette(colors);
		}

		#endregion

		private static Color ConvertColor(Rgb24 color)
		{
			return Color.FromRgb((byte) color.Red, (byte) color.Green, (byte) color.Blue);
		}

		[StructLayout(LayoutKind.Sequential), Serializable]
		public struct Rgb24
		{
			public char Red;
			public char Green;
			public char Blue;

			public override string ToString() => $"#{((byte)Red):X2}{((byte)Green):X2}{((byte)Blue):X2}";
		}
	}
}
