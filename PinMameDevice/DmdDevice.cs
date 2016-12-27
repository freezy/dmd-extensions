using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Media;
using NLog;
using RGiesecke.DllExport;

namespace PinMameDevice
{
	/// <summary>
	/// Äs DLL womr cha ubr C/C++ inäladä und wo vo VPinMAME bruicht wird um DMD
	/// datä z schickä. Drbi wird äs API implementiärt.
	/// </summary>
	/// <remarks>
	/// Diä Klass beinhautet fasch kä Logik sondrn tuät fascht auäs diräkt a 
	/// <see cref="DmdExt"/> weytrleitä.
	/// </remarks>
	/// <see cref="https://sourceforge.net/p/pinmame/code/HEAD/tree/trunk/ext/dmddevice/dmddevice.h"/>
	public static class DmdDevice
    {
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		static readonly DmdExt _dmdExt = new DmdExt();
		static readonly LinkedList<char> CData = new LinkedList<char>();

			// int Open()
		[DllExport("Open", CallingConvention = CallingConvention.Cdecl)]
		static int Open()
		{
			Logger.Info("[vpm] Open()");
			// wird ignoriärt wiu mr wartit bis diä ganzi Konfig ibärä isch.
			return 1;
		}

		// bool Close()
		[DllExport("Close", CallingConvention = CallingConvention.Cdecl)]
		static bool Close()
		{
			Logger.Info("[vpm] Close()");
			_dmdExt.Close();
			return true;
		}

		// void PM_GameSettings(const char* GameName, UINT64 HardwareGeneration, const PMoptions &Options)
		[DllExport("PM_GameSettings", CallingConvention = CallingConvention.Cdecl)]
		static void GameSettings(string gameName, ulong hardwareGeneration, IntPtr options)
		{
			var opt = (PMoptions) Marshal.PtrToStructure(options, typeof(PMoptions));
			Logger.Info("[vpm] PM_GameSettings({0})", opt.Colorize);
			_dmdExt.SetColorize(opt.Colorize != 0);
			_dmdExt.SetGameName(gameName);
			_dmdExt.SetColor(Color.FromRgb((byte)(opt.Red), (byte)(opt.Green), (byte)(opt.Blue)));
			_dmdExt.Init();
		}

		// void Console_Data(UINT8 data)
		[DllExport("Console_Data", CallingConvention = CallingConvention.Cdecl)]
		static void ConsoleData(byte data)
		{
			// Dä schickt immr eis Byte abr eigentlich wettr Bleck vo viär Bytes,
			// d.h miär mind ihs merkä was diä letschtä drii Bytes gsi sind um eppis
			// schlays chenna witr z schickä.
			// Wemmer diä viär Bytes de mau hett isch dr erschti Wärt immer äs P und
			// diä zwe druif sind Textzeichä womr i Hex muäss umwandlä. Am Schluss
			// chunnt de nu ä ni i Zihlä.
			CData.AddLast((char)data);
			if (CData.Count <= 4) {
				// het nunig aui wärt
				return;
			}
			CData.RemoveFirst();
			if (CData.First.Value == 'P') {
				_dmdExt.LoadPalette(Convert.ToUInt32(new string(new[] { CData.First.Next.Value, CData.First.Next.Next.Value }), 16));
			}
		}

		// void Render_RGB24(UINT16 width, UINT16 height, Rgb24 *currbuffer)
		[DllExport("Render_RGB24", CallingConvention = CallingConvention.Cdecl)]
		static void RenderRgb24(ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height * 3;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			_dmdExt.RenderRgb24(width, height, frame);
		}

		// void Render_16_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer) 
		[DllExport("Render_16_Shades", CallingConvention = CallingConvention.Cdecl)]
		static void RenderGray4(ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			_dmdExt.RenderGray4(width, height, frame);
		}

		// void Render_4_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer)
		[DllExport("Render_4_Shades", CallingConvention = CallingConvention.Cdecl)]
		static void RenderGray2(ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			_dmdExt.RenderGray2(width, height, frame);
		}

		//  void Render_PM_Alphanumeric_Frame(NumericalLayout numericalLayout, const UINT16 *const seg_data, const UINT16 *const seg_data2) 
		[DllExport("Render_PM_Alphanumeric_Frame", CallingConvention = CallingConvention.Cdecl)]
		static void RenderAlphaNum(NumericalLayout numericalLayout, IntPtr seg_data, IntPtr seg_data2)
		{
			_dmdExt.RenderAlphaNumeric(numericalLayout, Copy(seg_data, 64), Copy(seg_data2, 64));
		}

		// void Set_4_Colors_Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100) 
		[DllExport("Set_4_Colors_Palette", CallingConvention = CallingConvention.Cdecl)]
		static void SetGray2Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100)
		{
			Logger.Info("[vpm] Set_4_Colors_Palette()");
			_dmdExt.SetPalette(new[] {ConvertColor(color0), ConvertColor(color33), ConvertColor(color66), ConvertColor(color100)});
		}

		// void Set_16_Colors_Palette(Rgb24 *color)
		[DllExport("Set_16_Colors_Palette", CallingConvention = CallingConvention.Cdecl)]
		static void SetGray4Palette(IntPtr palette)
		{
			Logger.Info("[vpm] Set_16_Colors_Palette()");
			var size = Marshal.SizeOf(typeof (Rgb24));

			// for some shit reason, using a loop fails compilation.
			_dmdExt.SetPalette(new[] {
				ConvertColor(GetColorAtPosition(palette, 0, size)),
				ConvertColor(GetColorAtPosition(palette, 1, size)),
				ConvertColor(GetColorAtPosition(palette, 2, size)),
				ConvertColor(GetColorAtPosition(palette, 3, size)),
				ConvertColor(GetColorAtPosition(palette, 4, size)),
				ConvertColor(GetColorAtPosition(palette, 5, size)),
				ConvertColor(GetColorAtPosition(palette, 6, size)),
				ConvertColor(GetColorAtPosition(palette, 7, size)),
				ConvertColor(GetColorAtPosition(palette, 8, size)),
				ConvertColor(GetColorAtPosition(palette, 9, size)),
				ConvertColor(GetColorAtPosition(palette, 10, size)),
				ConvertColor(GetColorAtPosition(palette, 11, size)),
				ConvertColor(GetColorAtPosition(palette, 12, size)),
				ConvertColor(GetColorAtPosition(palette, 13, size)),
				ConvertColor(GetColorAtPosition(palette, 14, size)),
				ConvertColor(GetColorAtPosition(palette, 15, size)),
			});
		}

		private static bool IsOldWindows()
		{
			return Environment.OSVersion.Version.Major < 5;
		}

		private static Rgb24 GetColorAtPosition(IntPtr data, int pos, int size)
		{
			var p = new IntPtr(data.ToInt64() + pos*size);
			return (Rgb24) Marshal.PtrToStructure(p, typeof (Rgb24));
		}

		private static Color ConvertColor(Rgb24 color)
		{
			return Color.FromRgb((byte) color.Red, (byte) color.Green, (byte) color.Blue);
		}
		
		private static ushort[] Copy(IntPtr data, int length)
		{
			var buffer = new ushort[length];
			/*var byteBuffer = new byte[length * 2];
			Marshal.Copy(data, byteBuffer, 0, length * 2);
			var pos = 0;
			var i = 0;
			for (var i = 0; i < length; i += 2) {
				buffer[pos++] = BitConverter.ToUInt16(byteBuffer, i);
			}*/
			return buffer;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct PMoptions
		{
			public int Red, Green, Blue;
			public int Perc66, Perc33, Perc0;
			public int DmdOnly, Compact, Antialias;
			public int Colorize;
			public int Red66, Green66, Blue66;
			public int Red33, Green33, Blue33;
			public int Red0, Green0, Blue0;
		}

		[StructLayout(LayoutKind.Sequential), Serializable]
		public struct Rgb24
		{
			public char Red;
			public char Green;
			public char Blue;
		}

		public enum NumericalLayout
		{
			None,
			__2x16Alpha,
			__2x20Alpha,
			__2x7Alpha_2x7Num,
			__2x7Alpha_2x7Num_4x1Num,
			__2x7Num_2x7Num_4x1Num,
			__2x7Num_2x7Num_10x1Num,
			__2x7Num_2x7Num_4x1Num_gen7,
			__2x7Num10_2x7Num10_4x1Num,
			__2x6Num_2x6Num_4x1Num,
			__2x6Num10_2x6Num10_4x1Num,
			__4x7Num10,
			__6x4Num_4x1Num,
			__2x7Num_4x1Num_1x16Alpha,
			__1x16Alpha_1x16Num_1x7Num
		}
    }
}
