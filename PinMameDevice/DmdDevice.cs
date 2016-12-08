using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using NLog;
using RGiesecke.DllExport;

namespace PinMameDevice
{
	public static class DmdDevice
    {
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		static readonly DmdExt _dmdExt = new DmdExt();

		// int Open()
		[STAThread]
		[DllExport("Open", CallingConvention = CallingConvention.Cdecl)]
		static int Open()
		{
			Logger.Info("[vpm] Open()");
			// ignoring, use PM_GameSettings for opening because then we have all the config ready.
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
		static void PM_GameSettings(string gameName, ulong hardwareGeneration, PMoptions options)
		{
			Logger.Info("[vpm] PM_GameSettings()");
			_dmdExt.SetGameName(gameName);
			_dmdExt.SetColor(Color.FromRgb((byte)options.dmd_red, (byte)options.dmd_green, (byte)options.dmd_blue));
			_dmdExt.Open();
		}

		// void Render_RGB24(UINT16 width, UINT16 height, Rgb24 *currbuffer)
		[DllExport("Render_RGB24", CallingConvention = CallingConvention.Cdecl)]
		static void Render_RGB24(ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height * 3;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			_dmdExt.RenderRgb24(width, height, frame);
		}

		// void Render_16_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer) 
		[DllExport("Render_16_Shades", CallingConvention = CallingConvention.Cdecl)]
		static void Render_16_Shades(ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			_dmdExt.RenderGray4(width, height, frame);
		}

		// void Render_4_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer)
		[STAThread]
		[DllExport("Render_4_Shades", CallingConvention = CallingConvention.Cdecl)]
		static void Render_4_Shades(ushort width, ushort height, IntPtr currbuffer)
		{
			var frameSize = width * height;
			var frame = new byte[frameSize];
			Marshal.Copy(currbuffer, frame, 0, frameSize);
			_dmdExt.RenderGray2(width, height, frame);
		}

		//  void Render_PM_Alphanumeric_Frame(NumericalLayout numericalLayout, const UINT16 *const seg_data, const UINT16 *const seg_data2) 
		[DllExport("Render_PM_Alphanumeric_Frame", CallingConvention = CallingConvention.Cdecl)]
		static void Render_PM_Alphanumeric_Frame(NumericalLayout numericalLayout, IntPtr seg_data, IntPtr seg_data2)
		{
			Logger.Info("[vpm] Render_PM_Alphanumeric_Frame()");
		}

		// void Set_4_Colors_Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100) 
		[DllExport("Set_4_Colors_Palette", CallingConvention = CallingConvention.Cdecl)]
		static void Set_4_Colors_Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100)
		{
			Logger.Info("[vpm] Set_4_Colors_Palette()");
			_dmdExt.SetPalette(new []{ ConvertColor(color0), ConvertColor(color33), ConvertColor(color66), ConvertColor(color100) });
		}

		// void Set_16_Colors_Palette(Rgb24 *color)
		[DllExport("Set_16_Colors_Palette", CallingConvention = CallingConvention.Cdecl)]
		static void Set_16_Colors_Palette(IntPtr palette)
		{
			Logger.Info("[vpm] Set_16_Colors_Palette()");
			var size = Marshal.SizeOf(typeof(Rgb24));

			// for some shit reason, using a loop fails compilation.
			_dmdExt.SetPalette(new [] {
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

		private static Rgb24 GetColorAtPosition(IntPtr data, int pos, int size)
		{
			var p = new IntPtr(data.ToInt64() + pos * size);
			return (Rgb24) Marshal.PtrToStructure(p, typeof(Rgb24));
		}

		private static Color ConvertColor(Rgb24 color)
		{
			return Color.FromRgb((byte)color.red, (byte)color.green, (byte)color.blue);
		}

		struct PMoptions
		{
			public int dmd_red, dmd_green, dmd_blue;
			public int dmd_perc66, dmd_perc33, dmd_perc0;
			public int dmd_only, dmd_compact, dmd_antialias;
			public int dmd_colorize;
			public int dmd_red66, dmd_green66, dmd_blue66;
			public int dmd_red33, dmd_green33, dmd_blue33;
			public int dmd_red0, dmd_green0, dmd_blue0;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Rgb24
		{
			public char red;
			public char green;
			public char blue;
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
