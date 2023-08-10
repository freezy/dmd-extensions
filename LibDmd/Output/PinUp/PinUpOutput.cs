using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Output.PinUp
{
	public class PinUpOutput : IGray2Destination, IGray4Destination, IFixedSizeDestination, IFrameEventDestination
	{
		public string Name => "PinUP Writer";
		public bool IsAvailable { get; private set; }
		public bool NeedsDuplicateFrames => true;

		/// <summary>
		/// If an updated version of dmddevicepup.dll with PuP_Trigger() function found, set to true
		/// </summary>
		private readonly bool _pupTriggerSupported;

		/// <summary>
		/// If an frame event source such as Serum or VNI emits event frames.
		/// </summary>
		private bool _frameEventsAvailable;

		public Dimensions FixedSize => _size;
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
					_pupTriggerSupported = false;
					Logger.Warn("[pinup] Attempt to find PuP_Trigger function but dmddevicePUP.dll is outdated.");
				
				} else {
					_pupTriggerSupported = true;
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

			Marshal.Copy(Encoding.ASCII.GetBytes(romName), 0, _pnt, romName.Length); // convert to bytes to make DLL call work?
			SetGameName(_pnt, romName.Length); // external PUP dll call
		}

		public void RenderGray4(DmdFrame frame)
		{
			// if we got events, and we can send them, don't render frames.
			if (_frameEventsAvailable && _pupTriggerSupported) {
				return;
			}

			try {
				var rgbFrame = frame.CloneFrame().ConvertToRgb24(ColorUtil.GetPalette(new[] { Colors.Black, Colors.OrangeRed }, frame.NumColors));
				Marshal.Copy(rgbFrame.Data, 0, _pnt, rgbFrame.Data.Length);
				Render_RGB24((ushort) FixedSize.Width, (ushort) FixedSize.Height, _pnt);

			} catch (Exception e) {
				IsAvailable = false;
				Logger.Error(e, "[pinup] Error sending frame to PinUp, disabling.");
			}
		}

		public void RenderGray2(DmdFrame frame)
		{
			// if we got events, and we can send them, don't render frames.
			if (_frameEventsAvailable && _pupTriggerSupported) {
				return;
			}

			// 2-bit frames are rendered as 4-bit
			RenderGray4(frame.Update(FrameUtil.ConvertGrayToGray(frame.Data, new byte[] { 0x0, 0x1, 0x4, 0xf }), 4));
		}

		public void OnFrameEventInit(FrameEventInit frameEventInit)
		{
			_frameEventsAvailable = frameEventInit.EventsAvailable;
		}

		public void OnFrameEvent(FrameEvent frameEvent)
		{
			if (_pupTriggerSupported) {
				PuPTrigger(frameEvent.EventId);
			}
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

			// Marshal.FreeHGlobal(_pnt);
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
