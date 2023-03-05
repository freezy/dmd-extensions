using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output.PinUp;
using NLog;

namespace LibDmd.Converter.Serum
{
	public class Serum : AbstractSource, IConverter, IColoredGray6Source
	{
		public override string Name => "Serum";
		public FrameFormat From { get; } = FrameFormat.Gray2;
		public bool IsLoaded;
		public uint NumTriggersAvailable { get; }

		// cROM components
		/// <summary>
		/// Frame width in LEDs
		/// </summary>
		public int FrameWidth;

		/// <summary>
		/// Frame height in LEDs
		/// </summary>
		public int FrameHeight;

		/// <summary>
		/// Number of colours in the manufacturer's ROM
		/// </summary>
		public uint NumColors;

		/// <summary>
		/// =active instance of Pinup Player if available, =null if not
		/// </summary>
		private PinUpOutput _activePupOutput;
		
		private readonly Subject<ColoredFrame> _coloredGray6AnimationFrames = new Subject<ColoredFrame>();

		public ScalerMode ScalerMode { get; set; }

		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Maximum amount of color rotations per frame
		/// </summary>
		private const int MAX_COLOR_ROTATIONS = 8;

		/// <summary>
		/// Serum library functions declarations
		/// </summary>

		/// <summary>
		/// Serum_Load: Function to call at table opening time
		/// </summary>
		/// <param name="altcolorpath">path of the altcolor directory, e.g. "c:/Visual Pinball/VPinMame/altcolor/"</param>
		/// <param name="romname">rom name</param>
		/// <param name="width">out: colorized rom width in LEDs</param>
		/// <param name="height">out: colorized rom height in LEDs</param>
		/// <param name="numColors">out: number of colours in the manufacturer rom</param>
		/// <returns></returns>
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		// C format: bool Serum_Load(const char* const altcolorpath, const char* const romname, int* pwidth, int* pheight, unsigned int* pnocolors, unsigned int* pntriggers)
		private static extern bool Serum_Load(string altcolorpath, string romname,ref int width, ref int height, ref uint numColors, ref uint triggernb);
		
		/// <summary>
		/// Serum_Colorize: Function to call with a VpinMame frame to colorize it
		/// </summary>
		/// <param name="frame">width*height bytes: in: buffer with the VPinMame frame out: buffer with the colorized frame</param>
		/// <param name="width">frame width in LEDs</param>
		/// <param name="height">frame height in LEDs</param>
		/// <param name="palette">64*3 bytes: out: RGB palette description 64 colours with their R, G and B component</param>
		/// <param name="rotations">8*3 bytes: out: colour rotations 8 maximum per frame with first colour, number of colour and time interval in 10ms</param>
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		// C format: void Serum_Colorize(UINT8* frame, int width, int height, UINT8* palette, UINT8* rotations, UINT32* triggerID)
		private static extern void Serum_Colorize(Byte[] frame, int width, int height, byte[] palette, byte[] rotations,ref uint triggerID);
		
		/// <summary>
		/// Serum_Dispose: Function to call at table unload time to free allocated memory
		/// </summary>
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		// C format: void Serum_Dispose(void)
		private static extern void Serum_Dispose();
		
		/// <summary>
		/// Serum_Dispose: Function to call at table unload time to free allocated memory
		/// </summary>
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		// C format: void Serum_Dispose(void)
		private static extern void Serum_GetVersion(IntPtr version);

		public Serum(string altcolorPath, string romName)
		{
			uint numTriggers = 0;
			if (!Serum_Load(altcolorPath, romName, ref FrameWidth, ref FrameHeight, ref NumColors, ref numTriggers)) {
				IsLoaded = false;
				return;
			}
			NumTriggersAvailable = numTriggers;
			From = NumColors == 16 ? FrameFormat.Gray4 : FrameFormat.Gray2;
			IsLoaded = true;
		}

		public void SetPinupInstance(PinUpOutput puo)
		{
			_activePupOutput = puo;
			if ((puo != null) && (NumTriggersAvailable > 0)) {
				puo.PuPFrameMatching = false;
			}
		}

		public void Dispose()
		{
			Serum_Dispose();
			_activePupOutput = null;
			IsLoaded = false;
		}

		public void Init()
		{

		}
		
		public static string GetVersion()
		{
			byte[] version = new byte[16];
			GCHandle pinnedArray = GCHandle.Alloc(version, GCHandleType.Pinned);
			IntPtr pointer = pinnedArray.AddrOfPinnedObject();
			Serum_GetVersion(pointer);
			pinnedArray.Free();
			int len = 0;
			while ((version[len] != 0) && (len < 16)) {
				len++;
			}
			return Encoding.UTF8.GetString(version, 0, len);
		}
		
		public void Convert(DMDFrame frame)
		{
			Color[] palette = new Color[64];
			byte[] pal = new byte[64 * 3];
			byte[] frameData = new byte[FrameWidth * FrameHeight];
			byte[][] planes = new byte[6][];
			for (uint ti = 0; ti < 6; ti++) {
				planes[ti] = new byte[FrameWidth * FrameHeight / 8];
			}
			byte[] rotations = new byte[MAX_COLOR_ROTATIONS * 3];
			Buffer.BlockCopy(frame.Data, 0, frameData, 0, frame.Data.Length);
			
			uint triggerId = 0xFFFFFFFF;

			Serum_Colorize(frameData, FrameWidth, FrameHeight, pal, rotations, ref triggerId);

			for (uint ti = 0; ti < MAX_COLOR_ROTATIONS; ti++) {
				if ((rotations[ti * 3] >= 64) || (rotations[ti * 3] + rotations[ti * 3 + 1] > 64)) {
					rotations[ti * 3] = 255;
				}
			}
			
			if (_activePupOutput != null && triggerId != 0xFFFFFFFF) {
				_activePupOutput.SendTriggerID((ushort)triggerId);
			}
			
			CopyColorsToPalette(pal, palette);

			// convert to planes
			if ((Dimensions.Value.Width * Dimensions.Value.Height / 8) == planes[0].Length) {
				planes = FrameUtil.Split(Dimensions.Value.Width, Dimensions.Value.Height, planes.Length, frameData);
				
			} else {
				// We want to do the scaling after the animations get triggered.
				if (ScalerMode == ScalerMode.Doubler) {
					// Don't scale placeholder.
					CopyFrameToPlanes(frameData, planes, 6);
					planes = FrameUtil.Scale2(Dimensions.Value.Width, Dimensions.Value.Height, planes);
				} else {
					// Scale2 Algorithm (http://www.scale2x.it/algorithm)
					//var colorData = FrameUtil.Join(Dimensions.Value.Width / 2, Dimensions.Value.Height / 2, planes);
					var scaledData = FrameUtil.Scale2x(Dimensions.Value.Width, Dimensions.Value.Height, frameData);
					CopyFrameToPlanes(scaledData, planes, 6);
					planes = FrameUtil.Split(Dimensions.Value.Width, Dimensions.Value.Height, planes.Length, scaledData);
				}
			}
			
			// send the colored frame
			_coloredGray6AnimationFrames.OnNext(new  ColoredFrame(planes, palette, rotations));
		}

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6AnimationFrames;

		private void CopyFrameToPlanes(IReadOnlyList<byte> frame, IReadOnlyList<byte[]> planes, byte colorBitDepth)
		{
			byte bitMask = 1;
			var tj = 0;
			for (var tk = 0; tk < colorBitDepth; tk++) {
				planes[tk][tj] = 0;
			}

			var len = FrameWidth * FrameHeight;
			for (var ti = 0; ti < len; ti++) {
				byte tl = 1;
				for (var tk = 0; tk < colorBitDepth; tk++) {
					if ((frame[ti] & tl) > 0) planes[tk][tj] |= bitMask;
					tl <<= 1;
				}
				if (bitMask == 0x80) {
					bitMask = 1;
					tj++;
					if (tj < len / 8) {
						for (var tk = 0; tk < colorBitDepth; tk++) {
							planes[tk][tj] = 0;
						}
					}
				} else {
					bitMask <<= 1;
				}
			}
		}

		private void CopyColorsToPalette(byte[] scols, Color[] dpal)
		{
			for (int ti = 0; ti < 64; ti++)
			{
				dpal[ti].A = 255;
				dpal[ti].R = scols[ti * 3];
				dpal[ti].G = scols[ti * 3 + 1];
				dpal[ti].B = scols[ti * 3 + 2];
			}
		}
	}
}
