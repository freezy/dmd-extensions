using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter.Pin2Color
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i RGB24-Frames umwandlä.
	/// </summary>
	/// 
	/// <remarks>
	/// Fir Viärbit-Biuder git's kä Ergänzig unds einzigä wo cha 
	/// passiärä isch das ä kompletti Animazion abgschpiut wird.
	/// </remarks>
	public class Pin2ColorGray4Colorizer : AbstractSource, IConverter, IColoredGray2Source, IColoredGray4Source, IColoredGray6Source
	{
		public override string Name { get; } = "4-Bit Colorizer";
		public IEnumerable<FrameFormat> From => new [] {FrameFormat.Gray4};
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		public ScalerMode ScalerMode { get; set; }

		protected readonly Subject<ColoredFrame> ColoredGray2AnimationFrames = new Subject<ColoredFrame>();
		protected readonly Subject<ColoredFrame> ColoredGray4AnimationFrames = new Subject<ColoredFrame>();
		protected readonly Subject<ColoredFrame> ColoredGray6AnimationFrames = new Subject<ColoredFrame>();

		/// <summary>
		/// Datä vomer uism .pal-Feil uisägläsä hend
		/// </summary>
		private readonly VniColoring _vniColoring;

		/// <summary>
		/// Datä vomer uism Animazionsfeil uisägläsä hend
		/// </summary>
		private readonly AnimationSet _animations;
		private Animation _activeAnimation;

		/// <summary>
		/// Die etzigi Palettä
		/// </summary>
		private Palette _palette;
		private int _paletteIndex;

		/// <summary>
		/// D Standardpalettä wo bruicht wird wenn grad nid erkennt wordä isch
		/// </summary>
		private Palette _defaultPalette;
		private int _defaultPaletteIndex;

		/// <summary>
		/// Dr Timer wo bimänä ziitbeschränktä Palettäwächsu uifd Standardpalettä zruggsetzt
		/// </summary>
		private IDisposable _paletteReset;

		private bool _resetEmbedded = false;
		private int _lastEmbedded = -1;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Pin2ColorGray4Colorizer(VniColoring vniColoring, AnimationSet animations)
		{
			_vniColoring = vniColoring;
			_animations = animations;

			SetPalette(vniColoring.DefaultPalette, vniColoring.DefaultPaletteIndex, true);
		}

		public void Init()
		{
		}

		public void Convert(DmdFrame frame)
		{
			if (_vniColoring.Palettes.Length > 1 && _animations == null)
			{
				if (frame.Data[0] == 0x08 && frame.Data[1] == 0x09 &&
					frame.Data[2] == 0x0a && frame.Data[3] == 0x0b)
				{
					uint newpal = (uint)frame.Data[5] * 8 + (uint)frame.Data[4];
					for (int i = 0; i < 6; i++)
						frame.Data[i] = 0;

					if (newpal != _lastEmbedded)
					{
						LoadPalette(newpal);
						if (!_vniColoring.DefaultPalette.IsPersistent)
						{
							_resetEmbedded = true;
						}
						_lastEmbedded = (int)newpal;
					}
				}
				else if (_resetEmbedded)
				{
					_lastEmbedded = _vniColoring.DefaultPaletteIndex;
					SetPalette(_vniColoring.DefaultPalette, _vniColoring.DefaultPaletteIndex);
					_resetEmbedded = false;
				}
			}

			byte[][] planes;
			if (frame.Dimensions.Surface != frame.Data.Length * 4)
				planes = FrameUtil.Split(frame.Dimensions, 4, frame.Data);
			else
				planes = FrameUtil.Split(frame.Dimensions / 2, 4, frame.Data);

			if (_vniColoring.Mappings != null)
			{
				if (frame is RawFrame vd && vd.RawPlanes.Length > 0)
				{
					TriggerAnimation(frame.Dimensions, vd.RawPlanes, false);
				}
				else
				{
					TriggerAnimation(frame.Dimensions, planes, false);
				}
			}

			// Wenn än Animazion am laifä isch de wirds Frame dr Animazion zuägschpiut wos Resultat de säubr uisäschickt
			if (_activeAnimation != null)
			{
				_activeAnimation.ScalerMode = ScalerMode;
				_activeAnimation.NextFrame(frame.Dimensions, planes, AnimationFinished);
				return;
			}

			// Sisch diräkt uisgäh
			Render(frame.Dimensions, planes);
		}

		public void LoadPalette(uint newpal)
		{
			if (_vniColoring.Palettes.Length > newpal)
			{
				SetPalette(_vniColoring.GetPalette(newpal), (int)newpal);

			}
			else
			{
				Logger.Warn("[pin2color] No palette for change to " + newpal);
			}
		}
		/// <summary>
		/// Tuät s Biud durähäschä, luägt obs än Animazion uisleest odr Palettä setzt und macht das grad.
		/// </summary>
		/// <param name="planes">S Buid zum iberpriäfä
		/// </param>
		/// 
		public void ActivateMapping(Mapping mapping)
		{
			if (mapping.Mode == SwitchMode.Event)
			{
				return;
			}

			// If same LCM scene, no need to stop/start 
			if (_activeAnimation != null && _activeAnimation.SwitchMode == SwitchMode.LayeredColorMask && mapping.Mode == SwitchMode.LayeredColorMask && mapping.Offset == _activeAnimation.Offset)
			{
				return;
			}

			// If same LRM scene, no need to stop/start 
			if (_activeAnimation != null && _activeAnimation.SwitchMode == SwitchMode.MaskedReplace && mapping.Mode == SwitchMode.MaskedReplace && mapping.Offset == _activeAnimation.Offset)
			{
				return;
			}


			// Faus scho eppis am laifä isch, ahautä
			_activeAnimation?.Stop();
			_activeAnimation = null;

			// Palettä ladä
			var palette = _vniColoring.GetPalette(mapping.PaletteIndex);
			if (palette == null)
			{
				Logger.Warn("[pin2color] No palette found at index {0}.", mapping.PaletteIndex);
				return;
			}
			Logger.Debug("[pin2color] Setting palette {0} of {1} colors.", mapping.PaletteIndex, palette.Colors.Length);
			_paletteReset?.Dispose();
			_paletteReset = null;
			SetPalette(palette, mapping.PaletteIndex);

			// Palettä risettä wenn ä Lengi gäh isch
			if (!mapping.IsAnimation && mapping.Duration > 0)
			{
				_paletteReset = Observable
					.Never<Unit>()
					.StartWith(Unit.Default)
					.Delay(TimeSpan.FromMilliseconds(mapping.Duration)).Subscribe(_ =>
					{
						if (_defaultPalette != null)
						{
							Logger.Debug("[pin2color] Resetting to default palette after {0} ms.", mapping.Duration);
							SetPalette(_defaultPalette, _defaultPaletteIndex);
						}
						_paletteReset = null;
					});
			}

			// Animazionä
			if (mapping.IsAnimation)
			{

				// Luägä ob ibrhaipt äs VNI/FSQ Feil umä gsi isch
				if (_animations == null)
				{
					Logger.Warn("[pin2color] Tried to load animation but no animation file loaded.");
					return;
				}
				_activeAnimation = _animations.Find(mapping.Offset);

				if (_activeAnimation == null)
				{
					Logger.Warn("[pin2color] Cannot find animation at position {0}.", mapping.Offset);
					return;
				}

				_activeAnimation.Start(mapping.Mode, Render, AnimationFinished);
			}
		}

		private void TriggerAnimation(Dimensions dim, byte[][] planes, bool reverse)
		{
			uint nomaskcrc = 0;
			bool clear = true;

			for (var i = 0; i < planes.Length; i++)
			{
				var mapping = FindMapping(dim, planes[i], reverse, out nomaskcrc);

				// Faus niid gfundä hemmr fertig
				if (mapping != null)
				{
					Logger.Debug("[pin2color] Detect hash {0:X} for mode {1}", mapping.Checksum, mapping.Mode);

					ActivateMapping(mapping);
					// Can exit if not LCM sceene.
					if (_activeAnimation != null && _activeAnimation.SwitchMode != SwitchMode.LayeredColorMask && _activeAnimation.SwitchMode != SwitchMode.MaskedReplace)
						return;

				}
				if (_activeAnimation != null)
				{
					if (_activeAnimation.SwitchMode == SwitchMode.LayeredColorMask || _activeAnimation.SwitchMode == SwitchMode.MaskedReplace)
						clear = _activeAnimation.DetectLCM(planes[i], nomaskcrc, reverse, clear);
					else if (_activeAnimation.SwitchMode == SwitchMode.Follow || _activeAnimation.SwitchMode == SwitchMode.FollowReplace)
						_activeAnimation.DetectFollow(planes[i], nomaskcrc, _vniColoring.Masks, reverse);
				}
			}
		}

		/// <summary>
		/// Tuät Bitplane fir Bitplane häschä unds erschtä Mäpping wo gfundä
		/// wordä isch zrugg gäh.
		/// </summary>
		/// <param name="planes">Bitplanes vom Biud</param>
		/// <returns>Mäpping odr null wenn nid gfundä</returns>
		private Mapping FindMapping(Dimensions dim, byte[] plane, bool reverse, out uint NoMaskCRC)
		{
			NoMaskCRC = 0;
			var maskSize = dim.Width * dim.Height / 8;

			var checksum = FrameUtil.Checksum(plane, reverse);

			NoMaskCRC = checksum;

			var mapping = _vniColoring.FindMapping(checksum);
			if (mapping != null)
			{
				return mapping;
			}

			// Wenn kä Maskä definiert, de nächschti Bitplane
			if (_vniColoring.Masks == null || _vniColoring.Masks.Length <= 0)
				return null;

			// Sisch gemmr Maskä fir Maskä durä und luägid ob da eppis passt
			var maskedPlane = new byte[maskSize];
			foreach (var mask in _vniColoring.Masks)
			{
				checksum = FrameUtil.ChecksumWithMask(plane, mask, reverse);
				mapping = _vniColoring.FindMapping(checksum);
				if (mapping != null)
				{
					return mapping;
				}
			}

			return null;
		}

		/// <summary>
		/// Tuäts Biud uif diä entschprächändä Sourcä uisgäh.
		/// </summary>
		/// <param name="dim">Dimensionä vom Biud</param>
		/// <param name="planes">S Biud zum uisgäh</param>
		private void Render(Dimensions dim, byte[][] planes)
		{

			// todo can probably be dropped entirely, since we now do upscaling at graph level.
			if ((dim.Surface / 8) != planes[0].Length)
			{
				// We want to do the scaling after the animations get triggered.
				if (ScalerMode == ScalerMode.Doubler)
				{
					// Don't scale placeholder.
					planes = FrameUtil.Scale2(dim, planes);
				}
				else
				{
					// Scale2 Algorithm (http://www.scale2x.it/algorithm)
					var colorData = FrameUtil.Join(dim / 2, planes);
					var scaledData = FrameUtil.Scale2xUgh(dim, colorData);
					planes = FrameUtil.Split(dim, planes.Length, scaledData);
				}
			}

			// Wenns kä Erwiiterig gä hett, de gäbemer eifach d Planes mit dr Palettä zrugg
			if (planes.Length == 2)
			{
				ColoredGray2AnimationFrames.OnNext(new ColoredFrame(dim, planes, ColorUtil.GetPalette(_palette.GetColors((int)(Math.Log(_palette.Colors.Length) / Math.Log(2))), (int)Math.Pow(2, planes.Length)), _paletteIndex));
			}

			// Faus scho, de schickermr s Frame uifd entsprächendi Uisgab faus diä gsetzt isch
			if (planes.Length == 4)
			{
				ColoredGray4AnimationFrames.OnNext(new ColoredFrame(dim, planes, ColorUtil.GetPalette(_palette.GetColors((int)(Math.Log(_palette.Colors.Length)/Math.Log(2))), (int)Math.Pow(2, planes.Length)), _paletteIndex));
			}

			if (planes.Length == 6)
			{
				ColoredGray6AnimationFrames.OnNext(new ColoredFrame(dim, planes, ColorUtil.GetPalette(_palette.GetColors((int)(Math.Log(_palette.Colors.Length) / Math.Log(2))), (int)Math.Pow(2, planes.Length)), _paletteIndex));
			}
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="palette">Diä nii Palettä</param>
		/// <param name="isDefault"></param>
		public void SetPalette(Palette palette, int index, bool isDefault = false)
		{
			if (palette == null)
			{
				Logger.Warn("[pin2color] Ignoring null palette.");
				return;
			}
			if (isDefault)
			{
				_defaultPalette = palette;
				_defaultPaletteIndex = index;
			}
			Logger.Debug("[pin2color] Setting new palette ({0}): [ {1} ]", index, string.Join(" ", palette.Colors.Select(c => c.ToString())));
			_palette = palette;
			_paletteIndex = index;
		}

		/// <summary>
		/// Wird uisgfiährt wenn än Animazion fertig isch, cha irgend ä Modus si.
		/// </summary>
		protected void AnimationFinished()
		{
			//Logger.Trace("[pin2color] [timing] Animation finished.");
			//LastChecksum = 0x0;
			SetPalette(_defaultPalette, _defaultPaletteIndex);
			_activeAnimation = null;
		}

		public IObservable<ColoredFrame> GetColoredGray2Frames()
		{
			return ColoredGray2AnimationFrames;
		}

		public IObservable<ColoredFrame> GetColoredGray4Frames()
		{
			return ColoredGray4AnimationFrames;
		}
		
		public IObservable<ColoredFrame> GetColoredGray6Frames()
		{
			return ColoredGray6AnimationFrames;
		}
	}
}
