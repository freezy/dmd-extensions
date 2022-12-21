using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Security.Cryptography;
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
		

		public readonly string Filename;
		// cROM components
		private char[] rName; // ROM name
		private uint FWidth; // frame width
		private uint FHeight; // frame height
		private uint NFrames; // number of frames
		public uint NOColors; // Number of colors in palette of original ROM=nO
		private uint NCColors; // Number of colors in palette of colorized ROM=nC
		private uint NCompMasks; // Number of dynamic masks=nM
		private uint NMovMasks; // Number of moving rects=nMR
		private uint NSprites; // Number of sprites=nS
		private uint[] HashCodes; // uint[nF] hashcode/checksum
		private byte[] ShapeCompMode;   // UINT8[nF] FALSE - full comparison (all 4 colors) TRUE - shape mode (we just compare black 0 against all the 3 other colors as if it was 1 color)
										// HashCode take into account the ShapeCompMode parameter converting any '2' or '3' into a '1'
		private byte[] CompMaskID;  // UINT8[nF] Comparison mask ID per frame (255 if no rectangle for this frame)
		private byte[] MovRctID;    // UINT8[nF] Horizontal moving comparison rectangle ID per frame (255 if no rectangle for this frame)
		private byte[] CompMasks;   // UINT8[nM*fW*fH] Mask for comparison
		private byte[] MovRcts; // UINT8[nMR*4] Rect for Moving Comparision rectangle [x,y,w,h]. The value (<MAX_DYNA_4COLS_PER_FRAME) points to a sequence of 4 colors in Dyna4Cols. 255 means not a dynamic content.
		private byte[] CPal;        // UINT8[3*nC*nF] Palette for each colorized frames
		private byte[] CFrames; // UINT8[nF*fW*fH] Colorized frames color indices
		private byte[] DynaMasks;   // UINT8[nF*fW*fH] Mask for dynamic content for each frame.  The value (<MAX_DYNA_4COLS_PER_FRAME) points to a sequence of 4 colors in Dyna4Cols. 255 means not a dynamic content.
		private byte[] Dyna4Cols;  // UINT8[nF*MAX_DYNA_4COLS_PER_FRAME*nO] Color sets used to fill the dynamic content
		private byte[] FrameSprites; // UINT8[nF*MAX_SPRITES_PER_FRAME] Sprite numbers to look for in this frame max=MAX_SPRITES_PER_FRAME
		private byte[] SpriteDescriptionsO; // UINT8[nS*MAX_SPRITE_SIZE*MAX_SPRITE_SIZE] 4-or-16-color sprite original drawing (255 means this is a transparent=ignored pixel) for Comparison step
		private byte[] SpriteDescriptionsC; // UINT8[nS*MAX_SPRITE_SIZE*MAX_SPRITE_SIZE] 64-color sprite for Colorization step
		//private uint[] SpriteDetectDwords; // uint[nS] dword to quickly detect 4 consecutive distinctive pixels inside the original drawing of a sprite for optimized detection
		//private UInt16[] SpriteDetectDwordPos; // UINT16[nS] offset of the above dword in the sprite description
		private byte[] ActiveFrames; // UINT8[nF] is the frame active (colorized or duration>16ms) or not
		private byte[] ColorRotations; // UINT8[nF*3*MAX_COLOR_ROTATIONS] list of color rotation for each frame:
									   // 1st byte is color # of the first color to rotate / 2nd byte id the number of colors to rotate / 3rd byte is the length in 10ms between each color switch
		private UInt16[] SpriteDetAreas; // UINT16[nS*4*MAX_SPRITE_DETECT_AREAS] rectangles (left, top, width, height) as areas to detect sprites (left=0xffff -> no zone)
		private uint[] SpriteDetDwords; // uint[nS*MAX_SPRITE_DETECT_AREAS] dword to quickly detect 4 consecutive distinctive pixels inside the original drawing of a sprite for optimized detection
		private UInt16[] SpriteDetDwordPos; // UINT16[nS*MAX_SPRITE_DETECT_AREAS] offset of the above dword in the sprite description

		private const int MAX_DYNA_4COLS_PER_FRAME = 16; // max number of color sets for dynamic content for each frame
		private const int MAX_SPRITE_SIZE = 128; // maximum size of the sprites
		private const int MAX_SPRITES_PER_FRAME = 32; // maximum amount of sprites to look for per frame
		private const int MAX_COLOR_ROTATIONS = 8; // maximum amount of color rotations per frame
		private const int MAX_SPRITE_DETECT_AREAS = 4; // maximum number of areas to detect the sprite
		
		private bool serumloaded = false; // is there any crom loaded?
		private uint LastFound = 0; // Which frame was found last time we recognized one?
		private CRC32encode crce;

		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		protected readonly Subject<ColoredFrame> ColoredGray6AnimationFrames = new Subject<ColoredFrame>();
		private byte[] PreviousFrame;
		private Color[] PreviousPalette;
		private byte[] PreviousRotations;
		private byte PreviousSprite=255;
		//private uint PreviousSpritePos=0;
		private ushort PreviousFrx = 0;
		private ushort PreviousFry = 0;
		private ushort PreviousSpx = 0;
		private ushort PreviousSpy = 0;
		private ushort PreviousWid = 0;
		private ushort PreviousHei = 0;

		public bool isRotation = true;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Serum(string altcolorpath,string romname)
		{
			try
			{
				ZipFile.ExtractToDirectory(Path.Combine(altcolorpath, romname, romname + ".cRZ"), Path.Combine(altcolorpath, romname));
			}
			catch (Exception e)
			{
				Logger.Warn(e, $"Could not uncompress the cRZ File");
				serumloaded = false;
				return;
			}
			uint ti;
			crce = new CRC32encode();
			// load a crom file
			FileStream fs;
			try
			{
				fs = new FileStream(Path.Combine(altcolorpath, romname, romname + ".cRom"), FileMode.Open);
			}
			catch (Exception e)
			{
				Logger.Warn(e, $"Could not open the cRom File");
				serumloaded = false;
				return;
			}
			var reader = new BinaryReader(fs);
			rName = new char[64];
			rName = reader.ReadChars(64);
			uint sizeheader = reader.ReadUInt32(); // for possible modifications in the format
			FWidth = reader.ReadUInt32();
			FHeight = reader.ReadUInt32();
			NFrames = reader.ReadUInt32();
			NOColors = reader.ReadUInt32();
			if (NOColors == 16) From = FrameFormat.Gray4; else From = FrameFormat.Gray2;
			NCColors = reader.ReadUInt32();
			NCompMasks = reader.ReadUInt32();
			NMovMasks = reader.ReadUInt32();
			if (sizeheader >= 8 * sizeof(uint))
			{
				NSprites = reader.ReadUInt32();
			}
			else NSprites = 0;
			HashCodes = new uint[NFrames];
			for (ti = 0; ti < NFrames; ti++)
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
			Dyna4Cols = new byte[NFrames * MAX_DYNA_4COLS_PER_FRAME * NOColors];
			Dyna4Cols = reader.ReadBytes((int)(NFrames * MAX_DYNA_4COLS_PER_FRAME * NOColors));
			FrameSprites = new byte[NFrames * MAX_SPRITES_PER_FRAME];
			FrameSprites = reader.ReadBytes((int)(NFrames * MAX_SPRITES_PER_FRAME));
			SpriteDescriptionsO = new byte[NSprites * MAX_SPRITE_SIZE * MAX_SPRITE_SIZE];
			SpriteDescriptionsC = new byte[NSprites * MAX_SPRITE_SIZE * MAX_SPRITE_SIZE];
			for (ti = 0; ti < NSprites * MAX_SPRITE_SIZE * MAX_SPRITE_SIZE; ti++)
			{
				SpriteDescriptionsC[ti] = reader.ReadByte();
				SpriteDescriptionsO[ti] = reader.ReadByte();
			}
			/*SpriteDetectDwords = new uint[NSprites];
			for (ti = 0; ti < NSprites; ti++)
				SpriteDetectDwords[ti] = reader.ReadUInt32();
			SpriteDetectDwordPos = new UInt16[NSprites];
			for (ti = 0; ti < NSprites; ti++)
				SpriteDetectDwordPos[ti] = reader.ReadUInt16();*/
			ActiveFrames=new byte[NFrames];
			ActiveFrames = reader.ReadBytes((int)NFrames);
			ColorRotations=new byte[NFrames*3*MAX_COLOR_ROTATIONS];
			ColorRotations = reader.ReadBytes((int)NFrames * 3 * MAX_COLOR_ROTATIONS);
			SpriteDetDwords = new uint[NSprites * MAX_SPRITE_DETECT_AREAS];
			for (ti = 0; ti < NSprites * MAX_SPRITE_DETECT_AREAS; ti++)
				SpriteDetDwords[ti] = reader.ReadUInt32();
			SpriteDetDwordPos = new UInt16[NSprites * MAX_SPRITE_DETECT_AREAS];
			for (ti = 0; ti < NSprites * MAX_SPRITE_DETECT_AREAS; ti++)
				SpriteDetDwordPos[ti] = reader.ReadUInt16();
			SpriteDetAreas = new UInt16[NSprites * 4 * MAX_SPRITE_DETECT_AREAS];
			for (ti = 0; ti < NSprites * 4 * MAX_SPRITE_DETECT_AREAS; ti++)
				SpriteDetAreas[ti] = reader.ReadUInt16();
			reader.Close();
			PreviousFrame = new byte[FWidth * FHeight];
			PreviousPalette = new Color[64];
			PreviousRotations = new byte[MAX_COLOR_ROTATIONS * 3];
			File.Delete(Path.Combine(altcolorpath, romname, romname + ".cRom"));
			serumloaded = true;
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
			FrameSprites= null;
			SpriteDescriptionsC = null;
			SpriteDescriptionsO= null;
			SpriteDetDwords = null;
			SpriteDetDwordPos = null;
			SpriteDetAreas = null;
			ActiveFrames = null;
			ColorRotations= null;
			crce = null;
			PreviousFrame = null;
			PreviousPalette = null;
			PreviousRotations= null;
			serumloaded = false;
		}

		public void Init()
		{

		}

		private Int32 Identify_Frame(byte[] frame)
		{
			// check if the generated frame is the same as one we have in the crom (
			if (!serumloaded) return -1;
			bool[] framechecked = new bool[NFrames];
			byte[] pmask = new byte[FWidth * FHeight];
			for (uint tz = 0; tz < NFrames; tz++) framechecked[tz] = false;
			uint tj = LastFound; // we start from the frame we last found
			byte mask = 255;
			byte Shape = 0;
			do
			{
				// calculate the hashcode for the generated frame with the mask and shapemode of the current crom frame
				mask = CompMaskID[tj];
				Shape = ShapeCompMode[tj];
				uint Hashc;
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
				if (tj == NFrames) tj = 0;
				while ((tj != LastFound) && (framechecked[tj] == true))
				{
					tj++;
					if (tj == NFrames) tj = 0;
				}
			} while (tj != LastFound);
			return -1;
		}
		private void Check_Sprites(byte[] Frame, Int32 quelleframe, ref byte quelsprite, ref ushort frx,ref ushort fry, ref ushort spx, ref ushort spy, ref ushort wid,ref ushort hei)
		{
			byte ti=0;
			uint mdword;
			while ((ti < MAX_SPRITES_PER_FRAME) && (FrameSprites[quelleframe * MAX_SPRITES_PER_FRAME + ti] < 255)) 
			{
				byte qspr = FrameSprites[quelleframe * MAX_SPRITES_PER_FRAME + ti];
				for (uint tm=0;tm<MAX_SPRITE_DETECT_AREAS;tm++)
				{
					if (SpriteDetAreas[qspr * MAX_SPRITE_DETECT_AREAS * 4 + tm * 4] == 0xffff) continue;
					// we look for the sprite in the frame sent
					mdword = (uint)(Frame[0] << 8) | (uint)(Frame[1] << 16) | (uint)(Frame[2] << 24);
					for (UInt16 tj = 0; tj < FWidth * FHeight - 3; tj++)
					{
						mdword = (mdword >> 8) | (uint)(Frame[tj + 3] << 24);
						// we look for the magic dword first
						UInt16 sddp = SpriteDetDwordPos[qspr * MAX_SPRITE_DETECT_AREAS + tm];
						if (mdword == SpriteDetDwords[qspr * MAX_SPRITE_DETECT_AREAS + tm]) 
						{
							short frax = (short)(tj % FWidth);
							short fray = (short)(tj / FWidth);
							short sprx = (short)(sddp % MAX_SPRITE_SIZE);
							short spry = (short)(sddp / MAX_SPRITE_SIZE);
							short detx = (short)SpriteDetAreas[qspr * MAX_SPRITE_DETECT_AREAS * 4 + tm * 4];
							short dety = (short)SpriteDetAreas[qspr * MAX_SPRITE_DETECT_AREAS * 4 + tm * 4 + 1];
							short detw = (short)SpriteDetAreas[qspr * MAX_SPRITE_DETECT_AREAS * 4 + tm * 4 + 2];
							short deth = (short)SpriteDetAreas[qspr * MAX_SPRITE_DETECT_AREAS * 4 + tm * 4 + 3];
							if ((frax < sprx - detx) || (fray < spry - dety)) continue; // if the detection area is outside the frame, continue
							int offsx = frax - sprx + detx;
							int offsy = fray - spry + dety;
							if ((offsx + detw >= FWidth) || (offsy + deth >= FHeight)) continue;
							// we can now check if the sprite is there
							bool notthere = false;
							for (UInt16 tk = 0; tk < deth; tk++) 
							{
								for (UInt16 tl = 0; tl < detw; tl++)
								{
									byte val = SpriteDescriptionsO[qspr * MAX_SPRITE_SIZE * MAX_SPRITE_SIZE + (tk + dety) * MAX_SPRITE_SIZE + tl + detx];
									if (val == 255) continue;
									if (val != Frame[(tk + offsy) * FWidth + tl + offsx])
									{
										notthere = true;
										break;
									}
								}
								if (notthere == true) break;
							}
							if (!notthere)
							{
								quelsprite = qspr;
								/*								frx = (ushort)Math.Max(0, frax - sprx);
																fry = (ushort)Math.Max(0, fray - spry);
																spx = (ushort)Math.Max(0, sprx - frax);
																spy = (ushort)Math.Max(0, spry - fray);
																wid = (ushort)Math.Min(Math.Min((int)FWidth - (frax - sprx), (int)FWidth), MAX_SPRITE_SIZE);
																hei = (ushort)Math.Min(Math.Min((int)FHeight - (fray - spry), (int)FHeight), MAX_SPRITE_SIZE);
								*/
								if (frax < sprx)
								{
									spx = (ushort)(sprx - frax);
									frx = 0;
									wid = Math.Min((ushort)FWidth, (ushort)(MAX_SPRITE_SIZE - spx));
								}
								else
								{
									spx = 0;
									frx = (ushort)(frax - sprx);
									wid = Math.Min((ushort)(FWidth - frx), (ushort)(MAX_SPRITE_SIZE - frx));
								}
								if (fray < spry)
								{
									spy = (ushort)(spry - fray);
									fry = 0;
									hei = Math.Min((ushort)FHeight, (ushort)(MAX_SPRITE_SIZE - spy));
								}
								else
								{
									spy = 0;
									fry = (ushort)(fray - spry);
									hei = Math.Min((ushort)(FHeight - fry), (ushort)(MAX_SPRITE_SIZE - fry));
								}
								return;
							}
						}
					}
				}
				ti++;
			}
			quelsprite = 255;
			return;
		}
		private void Colorize_Frame(byte[] frame,Int32 IDfound)
		{
			uint ti;
			// Generate the colorized version of a frame once identified in the crom frames
			for (ti = 0; ti < FWidth * FHeight; ti++) 
			{
				byte dynacouche = DynaMasks[IDfound * FWidth * FHeight + ti];
				if (dynacouche == 255)
					frame[ti] = CFrames[IDfound * FWidth * FHeight + ti];
				else
					frame[ti] = Dyna4Cols[IDfound * MAX_DYNA_4COLS_PER_FRAME * NOColors + dynacouche * NOColors + frame[ti]];
			}
		}

		private void Colorize_Sprite(byte[] frame,byte nosprite,ushort frx,ushort fry,ushort spx,ushort spy,ushort wid,ushort hei)
		{
			for (uint tj=0;tj<hei;tj++)
			{
				for (uint ti = 0; ti < wid; ti++)
				{
					if (SpriteDescriptionsO[(nosprite * MAX_SPRITE_SIZE + tj + spy) * MAX_SPRITE_SIZE + ti + spx] < 255)
					{
						frame[(fry + tj) * FWidth + frx + ti] = SpriteDescriptionsC[(nosprite * MAX_SPRITE_SIZE + tj + spy) * MAX_SPRITE_SIZE + ti + spx];
					}
				}
			}
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
			byte[][] planes = new byte[6][];
			for (int i = 0; i < 6; i++) planes[i] = new byte[FWidth * FHeight / 8];
			Color[] palette = new Color[64];
			byte[] rotations = new byte[MAX_COLOR_ROTATIONS * 3];
			byte[] Frame=new byte[FWidth*FHeight];
			for (uint ti = 0; ti < FWidth * FHeight; ti++) Frame[ti] = frame.Data[ti];
			// We check if we find a sprite in the frame
			// Let's first identify the incoming frame among the ones we have in the crom
			Int32 IDfound = Identify_Frame(Frame);
			byte nosprite = 255;
			uint possprite = 0;
			ushort frx = 0, fry = 0, spx = 0, spy = 0, wid = 0, hei = 0;
			if ((IDfound == -1) || (ActiveFrames[IDfound] == 0))
			{
				/*// code for the colorization team
				for (uint ti=0;ti<NOColors;ti++)
				{
					palette[ti].A = 255;
					palette[ti].R = (byte)(255.0f * ((float)ti / (float)NOColors));
					palette[ti].G = (byte)(127.0f * ((float)ti / (float)NOColors));
					palette[ti].B = 0;
				}
				palette[16].A = 255;
				palette[16].R = 255;
				palette[16].G = 0;
				palette[16].B = 0;
				for (uint ti = 0; ti < 15; ti++)
				{
					for (uint tj = 0; tj < 5; tj++)
					{
						Frame[ti + tj * FWidth] = 16;
					}
				}*/
				// code for the players
				for (uint ti = 0; ti < FWidth * FHeight; ti++) Frame[ti] = PreviousFrame[ti];
				for (uint ti = 0; ti < 64; ti++) palette[ti] = PreviousPalette[ti];
				for (uint ti = 0; ti < 3 * MAX_COLOR_ROTATIONS; ti++) rotations[ti] = PreviousRotations[ti];
				nosprite = PreviousSprite;
				frx = PreviousFrx;
				fry = PreviousFry;
				spx = PreviousSpx;
				spy = PreviousSpy;
				wid = PreviousWid;
				hei = PreviousHei;
			}
			else
			{
				Check_Sprites(Frame, IDfound, ref nosprite,ref frx,ref fry,ref spx,ref spy,ref wid,ref hei);
				Colorize_Frame(Frame, IDfound);
				Copy_Frame_Palette(IDfound, palette);
				if (nosprite < 255)
				{
					Colorize_Sprite(Frame, nosprite, frx, fry, spx, spy, wid, hei);
				}
				for (uint ti = 0; ti < FWidth * FHeight; ti++) PreviousFrame[ti] = Frame[ti];
				for (uint ti = 0; ti < 64; ti++) PreviousPalette[ti] = palette[ti];
				for (uint ti = 0; ti < MAX_COLOR_ROTATIONS * 3; ti++)
				{
					PreviousRotations[ti] = rotations[ti] = ColorRotations[IDfound * 3 * MAX_COLOR_ROTATIONS + ti];
				}
				PreviousSprite = nosprite;
				PreviousFrx= frx;
				PreviousFry= fry;
				PreviousSpx= spx;
				PreviousSpy= spy;
				PreviousWid= wid;
				PreviousHei= hei;
			}
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
