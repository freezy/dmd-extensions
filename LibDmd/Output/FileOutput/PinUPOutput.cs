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
using System.Reflection;
using LibDmd.Common;

namespace LibDmd.Output.FileOutput
{
    static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
    }



    public class PinUPOutput : IBitmapDestination, IGray2Destination, IGray4Destination, IFixedSizeDestination
	{
        public string OutputFolder { get; set; }

		public string Name { get; } = "PinUP Writer";
        public bool IsAvailable { get; private set; }
        public int Width = 128;
		public int Height = 32;
		public uint Fps;

		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get; private set; } = 32;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _dRender_RGB24(ushort width, ushort height, IntPtr currbuffer);
        static _dRender_RGB24 Render_RGB24;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int _dOpen();
        static _dOpen Open;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool _dClose();
        static _dClose Close;
      /*  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _dGameSettings([MarshalAs(UnmanagedType.LPStr)]string gameName, ulong hardwareGeneration, IntPtr options);
        static _dGameSettings GameSettings;*/
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _dSetGameName(IntPtr cName, int len);
        static _dSetGameName SetGameName;

		private IntPtr pnt;
		private String _gameName;

		public PinUPOutput(string RomName)
		{
            IsAvailable = true;

            string localPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            string assemblyFolder = Path.GetDirectoryName(localPath);
            string dllFileName = Path.Combine(assemblyFolder, "dmddevicePUP.DLL");
            IntPtr pDll = NativeMethods.LoadLibrary(dllFileName);
            if (pDll == IntPtr.Zero)
            {
                IsAvailable = false;
                Logger.Error("Cannot load "+dllFileName);
                return;
            }
            try
            {
                IntPtr pAddress = NativeMethods.GetProcAddress(pDll, "Render_RGB24");
                if (pAddress == IntPtr.Zero)
                    throw new Exception("Cannot find Render_RGB24 in dmddevicePUP.dll");
                Render_RGB24 = (_dRender_RGB24)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dRender_RGB24));

                pAddress = NativeMethods.GetProcAddress(pDll, "Open");
                if (pAddress == IntPtr.Zero)
                    throw new Exception("Cannot map function in dmddevicePUP.dll");
                Open = (_dOpen)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dOpen));

                pAddress = NativeMethods.GetProcAddress(pDll, "Close");
                if (pAddress == IntPtr.Zero)
                    throw new Exception("Cannot map function in dmddevicePUP.dll");
                Close = (_dClose)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dClose));

/*                pAddress = NativeMethods.GetProcAddress(pDll, "GameSettings");
                if (pAddress == IntPtr.Zero)
                    throw new Exception("Cannot map function in dmddevicePUP.dll");
                GameSettings = (_dGameSettings)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dGameSettings)); */

                pAddress = NativeMethods.GetProcAddress(pDll, "SetGameName");
                if (pAddress == IntPtr.Zero)
                    throw new Exception("Cannot map function in dmddevicePUP.dll");
                SetGameName = (_dSetGameName)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dSetGameName));
            }
            catch (Exception e)
            {
                IsAvailable = false;
                Logger.Error("PinUpOutput: " + e.Message);
                return;
            }


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

        public void RenderGray4(byte[] frame)
        {
            // Render as orange palette (same as default with no PAL loaded)
            var planes = FrameUtil.Split(DmdWidth, DmdHeight, 4, frame);

            var orangeframe = LibDmd.Common.FrameUtil.ConvertToRgb24(DmdWidth, DmdHeight, planes, ColorUtil.GetPalette(new[] { Colors.Black, Colors.OrangeRed }, 16));

            Marshal.Copy(orangeframe, 0, pnt, DmdWidth * DmdHeight * 3);
            Render_RGB24((ushort)DmdWidth, (ushort)DmdHeight, pnt);
        }

        public void RenderGray2(byte[] frame)
        {
            // 2-bit frames are rendered as 4-bit
            RenderGray4(FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x1, 0x4, 0xf }));
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
