using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;
using Color = System.Windows.Media.Color;

namespace LibDmd.Converter.Vni
{
	public abstract class FrameSeq
	{
		public string Name { get; protected set; }

		/// <summary>
		/// Number of frames contained in this animation
		/// </summary>
		public int NumFrames => Frames.Length;

		/// <summary>
		/// Offset of this animation in the VNI file
		/// </summary>
		/// 
		/// <remarks>
		/// Needed as index for loading.
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
		protected int HoldCycles;
		protected int ClockFrom;
		protected bool ClockIsSmall;
		protected bool ClockInFront;
		protected int ClockOffsetX;
		protected int ClockOffsetY;
		protected int RefreshDelay;
		protected int Type;
		protected int Fsk;

		protected int PaletteIndex;
		protected Color[] AnimationColors;
		protected AnimationEditMode EditMode;
		protected int TransitionFrom;

		#endregion

		public Dimensions Size { get; protected set; }

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
		private Action<Dimensions, byte[][]> _currentRender;
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

		private readonly List<byte[]> _lcmBufferPlanes = new List<byte[]>();

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected FrameSeq(long offset)
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
		public void Start(SwitchMode mode, Action<Dimensions, byte[][]> render, Action completed = null)
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

		private void StartEnhance(Action<Dimensions, byte[][]> render)
		{
			_lastTick = Environment.TickCount;
			_timer = 0;
			_currentRender = render;
		}

		private void ClearLCMBuffer()
		{
			_lcmBufferPlanes.ForEach(FrameUtil.ClearPlane);
		}

		private byte[][] RenderLCM(Dimensions dim, byte[][] planes)
		{
			var numLcmPlanes = _lcmBufferPlanes.Count;
			var outPlanes = new byte[numLcmPlanes][];

			if (SwitchMode == SwitchMode.LayeredColorMask)
			{
				for (int i = 0; i < planes.Length; i++)
				{
					outPlanes[i] = planes[i];
				}
				for (int i = planes.Length; i < numLcmPlanes; i++)
				{
					outPlanes[i] = _lcmBufferPlanes[i];
				}
			}
			if (SwitchMode == SwitchMode.MaskedReplace)
			{

				if (_lcmBufferPlanes[0].Length == planes[0].Length * 4)
				{
					planes = ScalerMode == ScalerMode.Scale2x
						? FrameUtil.Scale2X(dim, planes)
						: FrameUtil.ScaleDouble(dim, planes);
				}

				for (int i = 0; i < numLcmPlanes; i++)
				{
					if (i < planes.Length)
						outPlanes[i] = FrameUtil.CombinePlaneWithMask(_lcmBufferPlanes[i], planes[i], ReplaceMask);
					else
						outPlanes[i] = _lcmBufferPlanes[i];
				}
			}

			return outPlanes;
		}

		private static int index = 0;

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

		public bool DetectLCM(byte[] plane, uint NoMaskCRC, bool Reverse, bool clear)
		{
			uint checksum = NoMaskCRC;

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
								FrameUtil.ClearPlane(ReplaceMask);
						}

						for (int i = 0; i < af.Planes.Count; i++)
						{
							FrameUtil.OrPlane(af.PlaneData[i], _lcmBufferPlanes[i]);
							if (SwitchMode == SwitchMode.MaskedReplace)
								FrameUtil.OrPlane(af.Mask, ReplaceMask);
						}
					}
				}
			}
			return clear;
		}

		private void StartLCM(Action<Dimensions, byte[][]> render)
		{
			_currentRender = render;
			_lcmBufferPlanes.Clear();
			for (int i = 0; i < Frames[0].Planes.Count; i++)
				_lcmBufferPlanes.Add(FrameUtil.NewPlane(Size));

			ClearLCMBuffer();
			if (SwitchMode == SwitchMode.MaskedReplace)
				ReplaceMask = new byte[Size.Surface / 8];

			Logger.Debug("[vni][{0}] Started LCM/LRM mode, ({1})...", SwitchMode, Name);
		}

		private void StartReplace(Action<Dimensions, byte[][]> render, Action completed = null)
		{
			Logger.Debug("[vni][{0}] Starting colored animation of {1} frames ({2})...", SwitchMode, Frames.Length, Name);
			_lastTick = Environment.TickCount;
			_timer = 0;
			_currentRender = render;
		}

		private void RenderAnimation(Dimensions dim, byte[][] vpmFrame, Action completed = null)
		{
			if (SwitchMode == SwitchMode.ColorMask || SwitchMode == SwitchMode.Replace)
			{
				var delay = Environment.TickCount - _lastTick;
				_lastTick = Environment.TickCount;

				_timer -= delay;

				if (_timer > 0)
				{
					_frameIndex--;
					OutputFrame(dim, vpmFrame);
					_frameIndex++;
					return;
				}
			}

			if (_frameIndex < NumFrames)
			{
				if (SwitchMode == SwitchMode.LayeredColorMask || SwitchMode == SwitchMode.MaskedReplace || SwitchMode == SwitchMode.Follow || SwitchMode == SwitchMode.FollowReplace)
				{
					OutputFrame(dim, vpmFrame);
					return;
				}
				
				InitializeFrame();
				OutputFrame(dim, vpmFrame);
				_frameIndex++;
				return;
			}
			
			completed?.Invoke();
			SwitchMode = SwitchMode.Palette;
			OutputFrame(dim, vpmFrame);
			Stop("finished");
		}

		private void OutputFrame(Dimensions dim, byte[][] planes)
		{
			byte[][] outplanes;

			switch (SwitchMode)
			{
				case SwitchMode.ColorMask:
				case SwitchMode.Follow:
					outplanes = RenderColorMask(planes);
					break;
				case SwitchMode.FollowReplace:
				case SwitchMode.Replace:
					outplanes = Frames[_frameIndex].PlaneData;
					break;
				case SwitchMode.LayeredColorMask:
				case SwitchMode.MaskedReplace:
					outplanes = RenderLCM(dim, planes);
					break;
				default:
					{
						outplanes = planes;
					}
					break;
			}

			if (outplanes[0].Length == dim.Surface / 2) {
				dim *= 2;
			}

			_currentRender?.Invoke(dim, outplanes);
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
				if (vpmFrame.Length == frame_count)
				{
					for (int i = 0; i < vpmFrame.Length-2; i++)
					{
						outplanes[i] = vpmFrame[i];
					}
					for (int i = vpmFrame.Length-2; i < frame_count; i++)
					{
						outplanes[i] = Frames[_frameIndex].Planes[i].Plane;
					}

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
			}
			return outplanes;
		}

		private void InitializeFrame()
		{
			_timer += (int)Frames[_frameIndex].Delay;
		}

		public void NextFrame(Dimensions dim, byte[][] planes, Action completed = null)
		{
			RenderAnimation(dim, planes, completed);
		}

		public void Stop(string what = "stopped")
		{
			_frameIndex = 0;
			_currentRender = null;
			IsRunning = false;
		}

		public bool Equals(FrameSeq frameSeq)
		{
			return Offset == frameSeq.Offset;
		}

		public override string ToString()
		{
			return $"{Name}, {Frames.Length} frames";
		}

		public void Dump(string path, Mapping mapping, Palette[] palettes)
		{
			var i = 0;
			foreach (var animFrame in Frames) {
				var palette = palettes[mapping.PaletteIndex];
				var frame = new ColoredFrame(Size, FrameUtil.Join(Size, animFrame.PlaneData), palette.Colors);
				frame.ConvertToBitmap()
					.GetBitmap()
					.Save(Path.Combine(path, $"{Name}_{i:000}.png"), ImageFormat.Png);
				i++;
			}
		}
	}

	public enum AnimationEditMode
	{
		Replace, Mask, Fixed
	}
}
