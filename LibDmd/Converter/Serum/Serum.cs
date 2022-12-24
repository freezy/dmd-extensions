using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.UI.WebControls;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;
using NLog;
using SharpGL;

namespace LibDmd.Converter.Serum
{
	public class Serum : AbstractSource, IConverter, IColoredGray6Source
	{
		public override string Name { get; } = "converter to ColorizedRom";
		public FrameFormat From { get; } = FrameFormat.Gray2;
		public bool Serum_loaded = false;

		public readonly string Filename;
		// cROM components
		private int FWidth; // frame width
		private int FHeight; // frame height
		public uint NOColors; // Number of colors in palette of original ROM=nO
		public IObservable<System.Reactive.Unit> OnResume { get; }
		public IObservable<System.Reactive.Unit> OnPause { get; }
		protected readonly Subject<ColoredFrame> ColoredGray6AnimationFrames = new Subject<ColoredFrame>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private const int MAX_COLOR_ROTATIONS = 8; // maximum amount of color rotations per frame

		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		//bool Serum_Load(const char* const altcolorpath, const char* const romname, int* pwidth, int* pheight, unsigned int* pnocolors)
		public static extern bool Serum_Load(string altcolorpath, string romname,ref int width, ref int height, ref uint nocolors);
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		// void Serum_Colorize(UINT8* frame, int width, int height, UINT8* palette, UINT8* rotations)
		public static extern void Serum_Colorize(Byte[] frame, int width, int height, byte[] palette, byte[] rotations);
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		// void Serum_Dispose(void)
		public static extern void Serum_Dispose();

		public Serum(string altcolorpath,string romname)
		{
			byte[] tpstring1 = Encoding.ASCII.GetBytes(altcolorpath);
			int lstr1 = tpstring1.Length;
			byte[] tpath = new byte[lstr1 + 1];
			for (int ti = 0; ti < lstr1; ti++) tpath[ti] = tpstring1[ti];
			tpath[lstr1] = 0;
			tpstring1 = Encoding.ASCII.GetBytes(romname);
			lstr1 = tpstring1.Length;
			byte[] trom = new byte[lstr1 + 1];
			for (int ti = 0; ti < lstr1; ti++) trom[ti] = tpstring1[ti];
			trom[lstr1] = 0;
			if (!Serum_Load(altcolorpath, romname, ref FWidth, ref FHeight, ref NOColors))
			{
				Serum_loaded = false;
			}
			Serum_loaded = true;
		}

		public void Dispose()
		{
			Serum_Dispose();
			Serum_loaded = false;
		}

		public void Init()
		{

		}

		private void Copy_Frame_to_Planes(byte[] Frame, byte[][] planes, byte colorbitdepth)
		{
			byte bitmsk = 1;
			uint tj = 0;
			for (uint tk = 0; tk < colorbitdepth; tk++) planes[tk][tj] = 0;
			for (uint ti = 0; ti < FWidth * FHeight; ti++) 
			{
				byte tl = 1;
				for (uint tk = 0; tk < colorbitdepth; tk++)
				{
					if ((Frame[ti] & tl) > 0) planes[tk][tj] |= bitmsk;
					tl <<= 1;
				}
				if (bitmsk == 0x80)
				{
					bitmsk = 1;
					tj++;
					if (tj < FWidth * FHeight / 8)
					{
						for (uint tk = 0; tk < colorbitdepth; tk++) planes[tk][tj] = 0;
					}
				}
				else bitmsk <<= 1;
			}
		}

		void Copy_Colours_to_Palette(byte[] scols, Color[] dpal)
		{
			for (int ti = 0; ti < 64; ti++)
			{
				dpal[ti].A = 255;
				dpal[ti].R = scols[ti * 3];
				dpal[ti].G = scols[ti * 3 + 1];
				dpal[ti].B = scols[ti * 3 + 2];
			}
		}

		public void Colorize(DMDFrame frame)
		{
			Color[] palette = new Color[64];
			byte[] pal = new byte[64 * 3];
			byte[] Frame = new byte[FWidth * FHeight];
			byte[][] planes = new byte[6][];
			for (uint ti = 0; ti < 6; ti++) planes[ti] = new byte[FWidth * FHeight / 8];
			byte[] rotations = new byte[MAX_COLOR_ROTATIONS * 3];
			for (uint ti = 0;ti<FWidth*FHeight;ti++) Frame[ti] = frame.Data[ti];
			Serum_Colorize(Frame, FWidth, FHeight, pal, rotations);
			Copy_Colours_to_Palette(pal, palette);
			Copy_Frame_to_Planes(Frame, planes, 6);
			ColoredGray6AnimationFrames.OnNext(new ColoredFrame(planes, palette, rotations));
		}
		public void Convert(DMDFrame frame)
		{
			byte[][] planes;
			if (Dimensions.Value.Width * Dimensions.Value.Height != frame.Data.Length * 4)
				planes = FrameUtil.Split(Dimensions.Value.Width, Dimensions.Value.Height, 2, frame.Data);
			else
				planes = FrameUtil.Split(Dimensions.Value.Width / 2, Dimensions.Value.Height / 2, 2, frame.Data);

		}
		public IObservable<ColoredFrame> GetColoredGray6Frames()
		{
			return ColoredGray6AnimationFrames;
		}
	}
}
