using System;
using System.Windows.Media;
using System.Collections.Generic;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	public abstract class Animation
	{
		public string Name { get; protected set; }

		/// <summary>
		/// Wefu Biudr diä Animazion het
		/// </summary>
		public int NumFrames => Frames.Length;

		/// <summary>
		/// Bitlängi odr Ahzahl Planes vo dr Buidr vo dr Animazion
		/// </summary>
		public int BitLength => Frames.Length > 0 ? Frames[0].BitLength : 0;

		/// <summary>
		/// Uif welärä Posizion (i Bytes) d Animazion im Feil gsi isch
		/// </summary>
		/// 
		/// <remarks>
		/// Wird aus Index zum Ladä bruicht.
		/// </remarks>
		public readonly long Offset;

		/// <summary>
		/// D Biudr vo dr Animazion
		/// </summary>
		protected AnimationFrame[] Frames;

		/// <summary>
		/// D Lengi vo dr ganzä Animazio i Millisekundä
		/// </summary>
		protected uint AnimationDuration;

		#region Unused Props
		protected int Cycles;
		protected int Hold;
		protected int ClockFrom;
		protected bool ClockSmall;
		protected bool ClockInFront;
		protected int ClockOffsetX;
		protected int ClockOffsetY;
		protected int RefreshDelay;
		[Obsolete] protected int Type;
		protected int Fsk;

		protected int PaletteIndex;
		protected Color[] AnimationColors;
		protected AnimationEditMode EditMode;
		protected int TransitionFrom;

		#endregion
		protected int Width;
		protected int Height;

		public ScalerMode ScalerMode { get; set; }

		#region Animation-related

		/// <summary>
		/// Wiä langs nu gaht bisd Animazion fertig isch
		/// </summary>
		public int RemainingFrames => NumFrames - _frameIndex;

		/// <summary>
		/// Faus ja de wärdid Biudr ergänzt, sisch wärdits uistuischt
		/// </summary>
		public bool AddPlanes => SwitchMode == SwitchMode.Follow || SwitchMode == SwitchMode.ColorMask;

		/// <summary>
		/// Dr Modus vo dr Animazion wo bestimmt wiäd Biudr aagwänded wärdid
		/// </summary>
		public SwitchMode SwitchMode { get; private set; }

		/// <summary>
		/// Zeigt ah obd Animazion nu am laifä isch
		/// </summary>
		public bool IsRunning { get; private set; }

		/// <summary>
		/// Current frame's following mask
		/// </summary>
		public byte[] ReplaceMask { get; private set; }

		private IObservable<AnimationFrame> _frames;
		private Action<byte[][]> _currentRender;
		private int _lastTick;
		private int _timer;

		/// <summary>
		/// Index of the frame currently displaying (or enhancing).
		/// </summary>
		private int _frameIndex;

		#endregion

		/// <summary>
		/// Next hash to look for (in col seq mode)
		/// </summary>
		uint Crc32 { get; }



		/// <summary>
		/// Mask for "Follow" switch mode.
		/// </summary>
		byte[] Mask { get; }

		protected byte[][] Masks;

		private List<byte[]> LCMBufferPlanes = new List<byte[]>();

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected Animation(long offset)
		{
			Offset = offset;
		}

		/// <summary>
		/// Tuät d Animazion startä.
		/// </summary>
		/// 
		/// <param name="mode">Dr Modus i welem d Animazion laift (chunnt uifs Mappind druif ah)</param>
		/// <param name="render">Ä Funktion wo tuät s Buid uisgäh</param>
		/// <param name="completed">Wird uisgfiärt wenn fertig</param>
		public void Start(SwitchMode mode, Action<byte[][]> render, Action completed = null)
		{
			IsRunning = true;
			SwitchMode = mode;
			_frameIndex = 0;

			switch (SwitchMode)
			{
				case SwitchMode.ColorMask:
				case SwitchMode.Follow:
					StartEnhance(render);
					break;
				case SwitchMode.Replace:
				case SwitchMode.FollowReplace:
					StartReplace(render, completed);
					break;
				case SwitchMode.LayeredColorMask:
				case SwitchMode.MaskedReplace:
					StartLCM(render);
					break;
			}
		}

		private void StartEnhance(Action<byte[][]> render)
		{
			_lastTick = Environment.TickCount;
			_timer = 0;
			_currentRender = render;
		}

		private void ClearLCMBuffer()
		{
			LCMBufferPlanes.ForEach(p => Common.FrameUtil.ClearPlane(p));
		}

		private byte[][] RenderLCM(byte[][] vpmFrame)
		{
			var frame_count = LCMBufferPlanes.Count;
			byte[][] outplanes;
			outplanes = new byte[frame_count][];

			if (SwitchMode == SwitchMode.LayeredColorMask)
			{
				for (int i = 0; i < vpmFrame.Length; i++)
				{
					outplanes[i] = vpmFrame[i];
				}
				for (int i = vpmFrame.Length; i < frame_count; i++)
				{
					outplanes[i] = LCMBufferPlanes[i];
				}
			}
			if (SwitchMode == SwitchMode.MaskedReplace)
			{
				if (vpmFrame[0].Length != LCMBufferPlanes[0].Length * 4)
				{
					if (ScalerMode == ScalerMode.Doubler)
					{
						vpmFrame = FrameUtil.Scale2(Width, Height, vpmFrame);
					}
					else
					{
						vpmFrame = FrameUtil.Scale2x(Width, Height, vpmFrame);
					}
				}

				for (int i = 0; i < frame_count; i++)
				{
					if (i < vpmFrame.Length)
						outplanes[i] = FrameUtil.CombinePlaneWithMask(LCMBufferPlanes[i], vpmFrame[i], ReplaceMask);
					else
						outplanes[i] = LCMBufferPlanes[i];
				}
			}

			return outplanes;
		}

		public void DetectFollow(byte[] plane, uint NoMaskCRC, byte[][] masks, bool Reverse)
		{
			var frameIndex = 0;
			foreach (var af in Frames)
			{
				if (NoMaskCRC == af.Hash)
				{
					_frameIndex = frameIndex;
					return;
				}
				if (masks != null && masks.Length > 0)
				{
					foreach (var mask in masks)
					{
						var maskcrc = FrameUtil.ChecksumWithMask(plane, mask, Reverse);
						if (maskcrc == af.Hash)
						{
							_frameIndex = frameIndex;
							return;
						}
					}
				}
				frameIndex++;
			}
		}

		public void DetectLCM(byte[] plane, uint NoMaskCRC, bool Reverse)
		{
			bool clear = true;
			uint checksum = NoMaskCRC;
			var maskSize = Width * Height / 8;
			var maskedPlane = new byte[maskSize];

			for (int k = -1; k < Masks.Length; k++)
			{
				if (k >= 0)
				{
					checksum = FrameUtil.ChecksumWithMask(plane, Masks[k], Reverse);
				}
				foreach (var af in Frames)
				{
					if (af.Hash == checksum)
					{
						if (clear)
						{
							ClearLCMBuffer();
							clear = false;
							if (SwitchMode == SwitchMode.MaskedReplace)
								Common.FrameUtil.ClearPlane(ReplaceMask);
						}

						for (int i = 0; i < af.Planes.Count; i++)
						{
							FrameUtil.OrPlane(af.PlaneData[i], LCMBufferPlanes[i]);
							if (SwitchMode == SwitchMode.MaskedReplace)
								FrameUtil.OrPlane(af.Mask, ReplaceMask);
						}
					}

				}
			}
		}

		private void StartLCM(Action<byte[][]> render)
		{
			_currentRender = render;
			LCMBufferPlanes.Clear();
			for (int i = 0; i < Frames[0].Planes.Count; i++)
				LCMBufferPlanes.Add(LibDmd.Common.FrameUtil.NewPlane(Width, Height));

			ClearLCMBuffer();
			if (SwitchMode == SwitchMode.MaskedReplace)
				ReplaceMask = new byte[(Width * Height) / 8];

			Logger.Debug("[vni][{0}] Started LCM/LRM mode, ({1})...", SwitchMode, Name);
		}

		private void StartReplace(Action<byte[][]> render, Action completed = null)
		{
			Logger.Debug("[vni][{0}] Starting colored animation of {1} frames ({2})...", SwitchMode, Frames.Length, Name);
			_lastTick = Environment.TickCount;
			_timer = 0;
			_currentRender = render;
		}

		private void RenderAnimation(byte[][] vpmFrame, Action completed = null)
		{
			if (SwitchMode == SwitchMode.ColorMask || SwitchMode == SwitchMode.Replace)
			{
				var delay = Environment.TickCount - _lastTick;
				_lastTick = Environment.TickCount;

				_timer -= delay;
			}

			if (_frameIndex < NumFrames)
			{
				if (SwitchMode == SwitchMode.LayeredColorMask || SwitchMode == SwitchMode.MaskedReplace || SwitchMode == SwitchMode.Follow || SwitchMode == SwitchMode.FollowReplace)
				{
					OutputFrame(vpmFrame);
					return;
				}
				if (_frameIndex < NumFrames)
				{
					InitializeFrame();
					OutputFrame(vpmFrame);
					_frameIndex++;
					return;
				}
			}
			else
			{
				if (NumFrames == 1 && _timer > 0)
				{
					if (_frameIndex == 1)
						_frameIndex--;
					OutputFrame(vpmFrame);
					_frameIndex++;
					return;
				}
			}
			completed?.Invoke();
			SwitchMode = SwitchMode.Palette;
			OutputFrame(vpmFrame);
			Stop("finished");
		}

		private void OutputFrame(byte[][] vpmFrame)
		{
			byte[][] outplanes;

			switch (SwitchMode)
			{
				case SwitchMode.ColorMask:
				case SwitchMode.Follow:
					outplanes = RenderColorMask(vpmFrame);
					break;
				case SwitchMode.FollowReplace:
				case SwitchMode.Replace:
					outplanes = Frames[_frameIndex].PlaneData;
					break;
				case SwitchMode.LayeredColorMask:
				case SwitchMode.MaskedReplace:
					outplanes = RenderLCM(vpmFrame);
					break;
				default:
					{
						outplanes = vpmFrame;
					}
					break;
			}
			_currentRender(outplanes);
		}

		private byte[][] RenderColorMask(byte[][] vpmFrame)
		{
			var frame_count = Frames[_frameIndex].Planes.Count;
			byte[][] outplanes;
			outplanes = new byte[frame_count][];

			if (Frames[_frameIndex].Planes.Count < 4)
			{
				Logger.Warn("[vni][{0}] Cannot enhance frame with {1} additional bitplanes.", SwitchMode, Frames[_frameIndex].Planes.Count);
			}
			else
			{
				for (int i = 0; i < vpmFrame.Length; i++)
				{
					outplanes[i] = vpmFrame[i];
				}
				for (int i = vpmFrame.Length; i < frame_count; i++)
				{
					outplanes[i] = Frames[_frameIndex].Planes[i].Plane;
				}
			}
			return outplanes;
		}

		private void InitializeFrame()
		{
			_timer += (int)Frames[_frameIndex].Delay;
		}

		public void NextFrame(byte[][] planes, Action completed = null)
		{
			RenderAnimation(planes, completed);
		}

		public void Stop(string what = "stopped")
		{
			_frameIndex = 0;
			_currentRender = null;
			IsRunning = false;
		}

		public bool Equals(Animation animation)
		{
			return Offset == animation.Offset;
		}

		public int getWidth()
		{
			return Width;
		}

		public int getHeight()
		{
			return Height;
		}

		public override string ToString()
		{
			return $"{Name}, {Frames.Length} frames";
		}
	}

	public enum AnimationEditMode
	{
		Replace, Mask, Fixed
	}
}
