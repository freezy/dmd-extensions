using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RGiesecke.DllExport;

namespace PinMameDevice
{
    internal static class DmdDevice
    {
		static readonly DmdExt _dmdExt = new DmdExt();

		// int Open()
		[STAThread]
		[DllExport("Open", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static int Open()
		{
			_dmdExt.Init();
			return 1;
		}

		// bool Close()
		[DllExport("Close", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static bool Close()
		{
			_dmdExt.Close();
			return true;
		}

		// void PM_GameSettings(const char* GameName, UINT64 HardwareGeneration, const PMoptions &Options)
		[DllExport("PM_GameSettings", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static void PM_GameSettings(string gameName, ulong hardwareGeneration, PMoptions options)
		{
			Console.WriteLine("[vpm] PM_GameSettings()");
		}

		// void Render_RGB24(UINT16 width, UINT16 height, Rgb24 *currbuffer)
		[DllExport("Render_RGB24", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static void Render_RGB24(ushort width, ushort height, IntPtr currbuffer)
		{
			Console.WriteLine("[vpm] Render_RGB24()");
		}

		// void Render_16_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer) 
		[DllExport("Render_16_Shades", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static void Render_16_Shades(ushort width, ushort height, IntPtr currbuffer)
		{
			Console.WriteLine("[vpm] Render_16_Shades()");
		}

		// void Render_4_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer)
		[STAThread]
		[DllExport("Render_4_Shades", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static void Render_4_Shades(ushort width, ushort height, IntPtr currbuffer)
		{
			var frame = new byte[width * height];
			Marshal.Copy(currbuffer, frame, 0, width * height);
			_dmdExt.RenderGray2(width, height, frame);
		}

		//  void Render_PM_Alphanumeric_Frame(NumericalLayout numericalLayout, const UINT16 *const seg_data, const UINT16 *const seg_data2) 
		[DllExport("Render_PM_Alphanumeric_Frame", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static void Render_PM_Alphanumeric_Frame(NumericalLayout numericalLayout, IntPtr seg_data, IntPtr seg_data2)
		{
			Console.WriteLine("[vpm] Render_PM_Alphanumeric_Frame()");
		}

		// void Set_4_Colors_Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100) 
		[DllExport("Set_4_Colors_Palette", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static void Set_4_Colors_Palette(Rgb24 color0, Rgb24 color33, Rgb24 color66, Rgb24 color100)
		{
			Console.WriteLine("[vpm] Set_4_Colors_Palette()");
		}

		// void Set_16_Colors_Palette(Rgb24 *color)
		[DllExport("Set_16_Colors_Palette", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		static void Set_16_Colors_Palette(IntPtr color)
		{
			Console.WriteLine("[vpm] Set_16_Colors_Palette()");
		}

		struct PMoptions
		{
			int dmd_red, dmd_green, dmd_blue;
			int dmd_perc66, dmd_perc33, dmd_perc0;
			int dmd_only, dmd_compact, dmd_antialias;
			int dmd_colorize;
			int dmd_red66, dmd_green66, dmd_blue66;
			int dmd_red33, dmd_green33, dmd_blue33;
			int dmd_red0, dmd_green0, dmd_blue0;
		}

		struct Rgb24
		{
			char red;
			char green;
			char blue;
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
