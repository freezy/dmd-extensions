#if PINUP_SUPPORT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows.Media;
using NLog;
using System.Runtime.InteropServices;
using System.Text;

namespace LibDmd.Output.FileOutput
{

	public class PinUPOutput : IBitmapDestination, IRgb24Destination, IFixedSizeDestination
	{
		public string OutputFolder { get; set; }

		public string Name { get; } = "PinUP Writer";
		public bool IsAvailable { get; } = true;
		public int Width = 128;
		public int Height = 32;
		public uint Fps;

		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get; private set; } = 32;

		[DllImport(@"dmddevicePUP.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern void Render_RGB24(ushort width, ushort height, IntPtr currbuffer);
		[DllImport(@"dmddevicePUP.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern int Open();
		[DllImport(@"dmddevicePUP.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern bool Close();
		[DllImport(@"dmddevicePUP.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern void GameSettings([MarshalAs(UnmanagedType.LPStr)]string gameName, ulong hardwareGeneration, IntPtr options);
		[DllImport(@"dmddevicePUP.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SetGameName(IntPtr cName, int len);

		private IntPtr pnt;
		private String _gameName;

		public PinUPOutput(string RomName)
		{
			// throw new InvalidFolderException("PINUP ERROR?......");
			Logger.Info("PinUP DLL Starting...." + RomName);
			Open();

			pnt = System.Runtime.InteropServices.Marshal.AllocHGlobal(Width * Height * 3);    //make a global memory pnt for DLL call

			_gameName = RomName;

			Marshal.Copy(Encoding.ASCII.GetBytes(_gameName), 0, pnt, _gameName.Length);   //convert to bytes to make DLL call work?

			SetGameName(pnt, _gameName.Length);  //external PUP dll call
		}

		public void Init()
		{
			// throw new InvalidFolderException("iniiintiting");
			if (this is IFixedSizeDestination) {
				//SetDimensions(DmdWidth, DmdHeight);
			}
		}

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		//Render Bitmap gets called by dmdext console with the -o PINUP option.  (pinball fx2/3 type support)
		public void RenderBitmap(BitmapSource bmp)
		{
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel * bmp.PixelWidth * bmp.PixelHeight];
			var rect = new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight);

			bmp.CopyPixels(rect, bytes, bmp.PixelWidth * bytesPerPixel, 0);
			RenderRgb24(bytes);
		}

		public void Dispose()
		{
			Close();
			// Marshal.FreeHGlobal(pnt);
		}

		public void ClearDisplay()
		{
			// no, we don't write a blank image.
		}

		public void RenderRgb24(byte[] frame)
		{
			// Copy the fram array to unmanaged memory.

			// Marshal.Copy(frame, 0, pnt, Width * Height * 3);    //crash with 128x16 so try something else
			// Render_RGB24((ushort) Width, (ushort) Height, pnt);
			Marshal.Copy(frame, 0, pnt, DmdWidth * DmdHeight * 3);
			Render_RGB24((ushort)DmdWidth, (ushort)DmdHeight, pnt);
		}

		public void RenderRaw(byte[] data)
		{
			Marshal.Copy(data, 0, pnt, Width * Height * 3);
			Render_RGB24((ushort)Width, (ushort)Height, pnt);
		}

		public void SetColor(Color color)
		{
			// ignore
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			// ignore
		}

		public void ClearPalette()
		{
			// ignore
		}

		public void ClearColor()
		{
			// ignore
		}
	}
}

#endif