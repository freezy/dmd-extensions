using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.PinUp
{
	public class PinUpOutput : IBitmapDestination, IGray2Destination, IGray4Destination, IFixedSizeDestination
	{
		public string Name => "PinUP Writer";
		public bool IsAvailable { get; private set; }

		/// <summary>
		/// If Serum colorization, set to true if no triggers found in it => legacy mode detection
		/// </summary>
		public bool PuPFrameMatching { get; set; } = true;

		/// <summary>
		/// If an updated version of dmddevicepup.dll with PuP_Trigger() function found, set to true
		/// </summary>
		public bool IsPuPTrigger { get; } = true;

		public Dimensions FixedSize { get; } = new Dimensions(128, 32);
		public bool DmdAllowHdScaling => false;

		private readonly Dimensions _size = new Dimensions(128, 32);
		private readonly IntPtr _pnt;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PinUpOutput(string romName)
		{
			if (romName == null) {
				throw new InvalidEnumArgumentException("ROM name must not be null.");
			}

			IsAvailable = true;

			var localPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
			var assemblyFolder = Path.GetDirectoryName(localPath);
			var dllFileName = Path.Combine(assemblyFolder, Environment.Is64BitProcess ? "dmddevicePUP64.DLL" : "dmddevicePUP.DLL");
			var pDll = NativeMethods.LoadLibrary(dllFileName);

			if (pDll == IntPtr.Zero) {
				IsAvailable = false;
				Logger.Error("[pinup] Cannot load " + dllFileName);
				return;
			}

			try {
				var pAddress = NativeMethods.GetProcAddress(pDll, "Render_RGB24");
				if (pAddress == IntPtr.Zero) { 
					throw new Exception("Cannot find Render_RGB24 in dmddevicePUP.dll");
				}
				Render_RGB24 = (_dRender_RGB24)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dRender_RGB24));

				pAddress = NativeMethods.GetProcAddress(pDll, "Open");
				if (pAddress == IntPtr.Zero) { 
					throw new Exception("Cannot map function in dmddevicePUP.dll");
				}
				Open = (_dOpen)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dOpen));

				pAddress = NativeMethods.GetProcAddress(pDll, "Close");
				if (pAddress == IntPtr.Zero) { 
					throw new Exception("Cannot map function in dmddevicePUP.dll");
				}
				Close = (_dClose)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dClose));

				pAddress = NativeMethods.GetProcAddress(pDll, "SetGameName");
				if (pAddress == IntPtr.Zero) { 
					throw new Exception("Cannot map function in dmddevicePUP.dll");
				}
				SetGameName = (_dSetGameName)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dSetGameName));

				pAddress = NativeMethods.GetProcAddress(pDll, "PuP_Trigger");
				if (pAddress == IntPtr.Zero) {
					IsPuPTrigger = false;
					Logger.Error("[pinup] Attempt to find PuP_Trigger function but dmddevicePUP.dll is outdated.");
				
				} else { 
					PuPTrigger = (_SendTrigger)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_SendTrigger));
				}
			}
			catch (Exception e) {
				IsAvailable = false;
				Logger.Error(e, "[pinup] Error sending frame to PinUp, disabling.");
				return;
			}

			Logger.Info($"[pinup] Starting {romName}...");
			Open();

			_pnt = Marshal.AllocHGlobal(_size.Surface * 3); // make a global memory pnt for DLL call
			var gameName = romName;

			Marshal.Copy(Encoding.ASCII.GetBytes(gameName), 0, _pnt, gameName.Length); // convert to bytes to make DLL call work?
			SetGameName(_pnt, gameName.Length); // external PUP dll call
		}

		public void SendTriggerId(ushort id)
		{
			if (IsPuPTrigger) {
				PuPTrigger(id);
			}
		}

		//Render Bitmap gets called by dmdext console.  (pinball fx2/3 type support)
		public void RenderBitmap(BmpFrame frame)
		{
			if (PuPFrameMatching == false) {
				return;
			}
			RenderRgb24(new DmdFrame(frame.Dimensions, ImageUtil.ConvertToRgb24(frame.Bitmap), 24));
		}

		public void RenderGray4(DmdFrame frame)
		{
			if (PuPFrameMatching == false) {
				return;
			}

			try {
				// Render as orange palette (same as default with no PAL loaded)
				var planes = FrameUtil.Split(FixedSize, 4, frame.Data);

				var orangeFrame = FrameUtil.ConvertToRgb24(FixedSize, planes,
					ColorUtil.GetPalette(new[] {Colors.Black, Colors.OrangeRed}, 16));

				Marshal.Copy(orangeFrame.Data, 0, _pnt, FixedSize.Surface * 3);
				Render_RGB24((ushort) FixedSize.Width, (ushort) FixedSize.Height, _pnt);

			} catch (Exception e) {
				IsAvailable = false;
				Logger.Error(e, "[pinup] Error sending frame to PinUp, disabling.");
			}
		}

		public void RenderGray2(DmdFrame frame)
		{
			// 2-bit frames are rendered as 4-bit
			if (PuPFrameMatching == false) return;
			RenderGray4(frame.Update(FrameUtil.ConvertGrayToGray(frame.Data, new byte[] { 0x0, 0x1, 0x4, 0xf }), 2));
		}

		public void ClearDisplay()
		{
			// no, we don't write a blank image.
		}

		public void Dispose()
		{
			try {
				Close();

			} catch (Exception e) {
				Logger.Warn(e, "[pinup] Error closing PinUP output: {0}", e.Message);
			}

			// Marshal.FreeHGlobal(pnt);
		}

		private void RenderRgb24(DmdFrame frame)
		{
			if (PuPFrameMatching == false) {
				return;
			}

			try {
				// Copy the frame array to unmanaged memory.

				// Marshal.Copy(frame, 0, pnt, Width * Height * 3);    //crash with 128x16 so try something else
				// Render_RGB24((ushort) Width, (ushort) Height, pnt);
				Marshal.Copy(frame.Data, 0, _pnt, FixedSize.Surface * 3);
				Render_RGB24((ushort) FixedSize.Width, (ushort) FixedSize.Height, _pnt);

			} catch (Exception e) {
				IsAvailable = false;
				Logger.Error(e, "[pinup] Error sending frame to PinUp, disabling.");
			}
		}

		#region DLL API

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

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _SendTrigger(ushort trigID);
		private _SendTrigger PuPTrigger;

		#endregion
	}

	static class NativeMethods
	{
		[DllImport("kernel32.dll")]
		public static extern IntPtr LoadLibrary(string dllToLoad);

		[DllImport("kernel32.dll")]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

		[DllImport("kernel32.dll")]
		public static extern bool FreeLibrary(IntPtr hModule);
	}
}
