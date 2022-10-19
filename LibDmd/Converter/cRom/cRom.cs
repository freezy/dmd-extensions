using System;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Common;
//using LibDmd.Converter.Colorize;
using LibDmd.Input;
using Xceed.Wpf.Toolkit;

namespace LibDmd.Converter.cRom
{
	public class cRom : AbstractSource, IConverter, IColoredGray6Source
	{
		public override string Name { get; } = "2-bit source to ColorizedRom";
		public FrameFormat From { get; } = FrameFormat.Gray2;

		public readonly string Filename;
		// cROM components
		private char[] rName; // ROM name
		private UInt32 FWidth; // frame width
		private UInt32 FHeight; // frame height
		private UInt32 NFrames; // number of frames
		private UInt32 NCColors; // Number of colors in palette of colorized ROM=nC
		private UInt32 NCompMasks; // Number of dynamic masks=nM
		private UInt32 NMovMasks; // Number of moving rects=nMR
		private UInt32[] HashCodes; // UINT32[nF] hashcode/checksum
		private byte[] ShapeCompMode;   // UINT8[nF] FALSE - full comparison (all 4 colors) TRUE - shape mode (we just compare black 0 against all the 3 other colors as if it was 1 color)
										// HashCode take into account the ShapeCompMode parameter converting any '2' or '3' into a '1'
		private byte[] CompMaskID;  // UINT8[nF] Comparison mask ID per frame (255 if no rectangle for this frame)
		private byte[] MovRctID;    // UINT8[nF] Horizontal moving comparison rectangle ID per frame (255 if no rectangle for this frame)
		private byte[] CompMasks;   // UINT8[nM*fW*fH] Mask for comparison
		private byte[] MovRcts; // UINT8[nMR*4] Rect for Moving Comparision rectangle [x,y,w,h]. The value (<MAX_DYNA_4COLS_PER_FRAME) points to a sequence of 4 colors in Dyna4Cols. 255 means not a dynamic content.
		private byte[] CPal;        // UINT8[3*nC*nF] Palette for each colorized frames
		private byte[] CFrames; // UINT8[nF*fW*fH] Colorized frames color indices
		private byte[] DynaMasks;   // UINT8[nF*fW*fH] Mask for dynamic content for each frame.  The value (<MAX_DYNA_4COLS_PER_FRAME) points to a sequence of 4 colors in Dyna4Cols. 255 means not a dynamic content.
		private byte[] Dyna4Cols;  // UINT8[nF*MAX_DYNA_4COLS_PER_FRAME*4] Color sets used to fill the dynamic content

		private const int MAX_DYNA_4COLS_PER_FRAME = 8;

		private bool cromloaded = false; // is there any crom loaded?
		private UInt32 LastFound = 0; // Which frame was found last time we recognized one?
		private CRC32encode crce;

		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		protected readonly Subject<ColoredFrame> ColoredGray6AnimationFrames = new Subject<ColoredFrame>();

		public cRom(string filename)
		{
			crce = new CRC32encode();
			// load a crom file
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);
			rName = new char[64];
			rName = reader.ReadChars(64);
			FWidth = reader.ReadUInt32();
			FHeight = reader.ReadUInt32();
			NFrames = reader.ReadUInt32();
			reader.ReadUInt32();
			NCColors = reader.ReadUInt32();
			NCompMasks = reader.ReadUInt32();
			NMovMasks = reader.ReadUInt32();
			HashCodes = new UInt32[NFrames];
			for (uint ti = 0; ti < NFrames; ti++)
				HashCodes[ti] = reader.ReadUInt32();
			ShapeCompMode = new byte[NFrames];
			ShapeCompMode = reader.ReadBytes((int)NFrames);
			CompMaskID = new byte[NFrames];
			CompMaskID = reader.ReadBytes((int)NFrames);
			MovRctID = new byte[NFrames];
			MovRctID = reader.ReadBytes((int)NFrames);
			if (NCompMasks > 0)
			{
				CompMasks = new byte[NCompMasks * FHeight * FWidth];
				CompMasks = reader.ReadBytes((int)(NCompMasks * FHeight * FWidth));
			}
			if (NMovMasks > 0)
			{
				MovRcts = new byte[NMovMasks * FHeight * FWidth];
				MovRcts = reader.ReadBytes((int)(NMovMasks * FHeight * FWidth));
			}
			CPal = new byte[NFrames * 3 * NCColors];
			CPal = reader.ReadBytes((int)(NFrames * 3 * NCColors));
			CFrames = new byte[NFrames * FHeight * FWidth];
			CFrames = reader.ReadBytes((int)(NFrames * FHeight * FWidth));
			DynaMasks = new byte[NFrames * FHeight * FWidth];
			DynaMasks = reader.ReadBytes((int)(NFrames * FHeight * FWidth));
			Dyna4Cols = new byte[NFrames * MAX_DYNA_4COLS_PER_FRAME * 4];
			Dyna4Cols = reader.ReadBytes((int)(NFrames * MAX_DYNA_4COLS_PER_FRAME * 4));
			reader.Close();
			cromloaded = true;
		}

		public void Dispose()
		{
			HashCodes = null;
			ShapeCompMode = null;
			CompMaskID = null;
			MovRctID = null;
			CompMasks = null;
			MovRcts = null;
			CPal = null;
			CFrames = null;
			DynaMasks = null;
			Dyna4Cols = null;
			crce = null;
		}

		public void Init()
		{

		}

		private Int32 Identify_Frame(byte[] frame)
		{
			// check if the generated frame is the same as one we have in the crom (
			if (!cromloaded) return -1;
			bool[] framechecked = new bool[NFrames];
			byte[] pmask = new byte[FWidth * FHeight];
			for (UInt32 tz = 0; tz < NFrames; tz++) framechecked[tz] = false;
			UInt32 tj = LastFound; // we start from the frame we last found
			byte mask = 255;
			byte Shape = 0;
			do
			{
				// calculate the hashcode for the generated frame with the mask and shapemode of the current crom frame
				mask = CompMaskID[tj];
				Shape = ShapeCompMode[tj];
				UInt32 Hashc;
				if (mask < 255)
				{
					for (uint ti = 0; ti < FWidth * FHeight; ti++) pmask[ti] = CompMasks[mask * FWidth * FHeight + ti];
					Hashc = crce.crc32_fast_mask(frame, pmask, FWidth * FHeight, Shape);
				}
				else Hashc = crce.crc32_fast(frame, FWidth * FHeight, Shape);
				// now we can compare with all the crom frames that share these same mask and shapemode
				for (int ti = (int)tj; ti < (int)NFrames; ti++)
				{
					if (framechecked[ti]) continue;
					if ((CompMaskID[ti] == mask) && (ShapeCompMode[ti] == Shape))
					{
						if (Hashc == HashCodes[ti])
						{
							LastFound = (uint)ti;
							return ti; // we found the frame, we return it
						}
						framechecked[ti] = true;
					}
				}
				for (int ti = 0; ti < (int)tj; ti++)
				{
					if (framechecked[ti]) continue;
					if ((CompMaskID[ti] == mask) && (ShapeCompMode[ti] == Shape))
					{
						if (Hashc == HashCodes[ti])
						{
							LastFound = (uint)ti;
							return ti; // we found the frame, we return it
						}
						framechecked[ti] = true;
					}
				}
				tj++;
				while ((tj != LastFound) && (framechecked[tj] == true))
				{
					tj++;
					if (tj == NFrames) tj = 0;
				}
			} while (tj != LastFound);
			return -1;
		}

		private void Colorize_Frame(byte[] frame,Int32 IDfound)
		{
			// Generate the colorized version of a frame once identified in the crom frames
			for (UInt32 ti = 0; ti < FWidth * FHeight; ti++) 
			{
				byte dynacouche = DynaMasks[IDfound * FWidth * FHeight + ti];
				if (dynacouche == 255)
					frame[ti] = CFrames[IDfound * FWidth * FHeight + ti];
				else
					frame[ti] = Dyna4Cols[IDfound * MAX_DYNA_4COLS_PER_FRAME * 4 + dynacouche * 4 + frame[ti]];
			}
		}

		/*private void Copy_Planes_to_Frame(DMDFrame frame, byte[] Frame, byte colorbitdepth)
		{
			/*uint offsetplane = (FWidth * FHeight)>>3;
			uint tk = 0;
			for (uint ti = 0; ti < (FWidth * FHeight)>>3; ti++)
			{
				byte bitmsk = 1;// 0x80;
				byte[] plane = new byte[8];
				for (uint tl = 0; tl < colorbitdepth; tl++)
				{
					plane[tl] = frame.Data[ti + tl * offsetplane];
				}
				for (uint tj = 0; tj < 8; tj++)
				{
					Frame[tk] = 0;
					byte btmsk = 1;
					for (byte tl = 0; tl < colorbitdepth; tl++)
					{
						if ((plane[tl] & bitmsk) > 0) Frame[tk] += btmsk;
						btmsk <<= 1;
					}
					bitmsk <<= 1;//>> 1);
					tk++;
				}
			}
			byte bitmsk = 0x80;
			uint tj = 0;
			for (uint ti=0;ti<FWidth*FHeight;ti++)
			{
				byte btmsk = (byte)(1 << (colorbitdepth - 1));
				Frame[ti] = 0;
				for (uint tk = 0; tk < colorbitdepth; tk++)
				{
					if ((frame.Data[tj] & bitmsk) > 0)
						Frame[ti] |= btmsk;
					btmsk >>= 1;
					if (bitmsk == 1)
					{
						bitmsk = 0x80;
						tj++;
					}
					else bitmsk >>= 1;
				}
			}
		}*/

		private void Copy_Frame_to_Planes(byte[] Frame, byte[][] planes, byte colorbitdepth)
		{
			byte bitmsk = 1;
			uint tj = 0;
			for (uint tk=0;tk<colorbitdepth;tk++) planes[tk][tj] = 0;
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

		void Copy_Frame_Palette(Int32 nofr, Color[] dpal)
		{
			for (int ti = 0; ti < 64; ti++)
			{
				dpal[ti].A = 255;
				dpal[ti].R = CPal[nofr * 64 * 3 + ti * 3];
				dpal[ti].G = CPal[nofr * 64 * 3 + ti * 3 + 1];
				dpal[ti].B = CPal[nofr * 64 * 3 + ti * 3 + 2];
			}
		}

		public void Colorize(DMDFrame frame)
		{
			byte[] Frame=new byte[FWidth*FHeight];
			for (uint ti = 0; ti < FWidth * FHeight; ti++) Frame[ti] = frame.Data[ti];
			/*if (Dimensions.Value.Width * Dimensions.Value.Height != frame.Data.Length * 4)
				planes = FrameUtil.Split(Dimensions.Value.Width, Dimensions.Value.Height, 2, frame.Data);
			else
				planes = FrameUtil.Split(Dimensions.Value.Width / 2, Dimensions.Value.Height / 2, 2, frame.Data);*/
			// Let's first identify the incoming frame among the ones we have in the crom
			Int32 IDfound = Identify_Frame(Frame);
			if (IDfound == -1) return; //no frame found, return without changing
									   // Let's now generate the corresponding colorized frame
			Colorize_Frame(Frame, IDfound);
			byte[][] planes = new byte[6][];
			Color[] palette = new Color[64];
			for (int i = 0; i < 6; i++) planes[i] = new byte[FWidth * FHeight / 8];
			Copy_Frame_to_Planes(Frame, planes, 6);
			Copy_Frame_Palette(IDfound, palette);
			ColoredGray6AnimationFrames.OnNext(new ColoredFrame(planes, palette));
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
