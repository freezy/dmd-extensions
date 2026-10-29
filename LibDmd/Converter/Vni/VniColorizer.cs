using System;
using System.Collections.Generic;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter.Vni
{
	public class VniColorizer : AbstractConverter, IColoredGray2Source, IColoredGray4Source, IColoredGray6Source, IRgb24Source
	{
		public override string Name => "VNI Colorizer";
		public override IEnumerable<FrameFormat> From => new []{ FrameFormat.Gray2, FrameFormat.Gray4 };

		public IObservable<ColoredFrame> GetColoredGray2Frames() => DedupedColoredGray2Source.GetColoredGray2Frames();
		public IObservable<ColoredFrame> GetColoredGray4Frames() => DedupedColoredGray4Source.GetColoredGray4Frames();
		public IObservable<ColoredFrame> GetColoredGray6Frames() => DedupedColoredGray6Source.GetColoredGray6Frames();
		public IObservable<DmdFrame> GetRgb24Frames() => DedupedRgb24Source.GetRgb24Frames();

		public bool Has128x32Animation { get; set; }
		public ScalerMode ScalerMode { get; set; }

		protected override bool PadSmallFrames => true;

		/// <summary>
		/// Data from the .pal file
		/// </summary>
		private readonly PalFile _palFile;

		/// <summary>
		/// Data from the .vni file
		/// </summary>
		private readonly AnimationSet _animations;

		/// <summary>
		/// IF not null, the currently playing animation.
		/// </summary>
		private FrameSeq _activeFrameSeq;

		/// <summary>
		/// The current palette
		/// </summary>
		private Palette _palette;

		/// <summary>
		/// The standard palette to use when nothing has matched.
		/// </summary>
		private Palette _defaultPalette;

		/// <summary>
		/// The timer that resets to the standard palette in case a time-limited palette change is active.
		/// </summary>
		private long _paletteResetAt;

		private bool _resetEmbedded;
		private int _lastEmbedded = -1;

		/// <summary>
		/// The last frame that was checked for triggers, so a frame repeated by the
		/// clock isn't checked again.
		/// </summary>
		private readonly FrameSnapshot<DmdFrame> _lastTriggerFrame = new FrameSnapshot<DmdFrame>();
		private RetainedBuffer<byte> _embeddedData;
		private RetainedBuffer<byte[]> _triggerPlanes;
		private readonly FrameBufferPool _frameBuffers = new FrameBufferPool();
		private readonly Action _animationFinished;
		private readonly Action<Dimensions, byte[][]> _render;

		/// <summary>
		/// Reuse output storage after each observer callback returns. Observers that
		/// retain a frame must copy its pixels and palette. Enabled by default in LibDmd.Core.
		/// </summary>
		public bool ReuseFrameBuffers { get; set; }
#if LIBDMD_CORE
			= true;
#endif

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VniColorizer(PalFile palFile, AnimationSet animations) : base(true)
		{
			_palFile = palFile;
			_animationFinished = AnimationFinished;
			_render = Render;
			_animations = animations;
			Has128x32Animation = (_palFile.Masks != null && _palFile.Masks.Length >= 1 && _palFile.Masks[0].Length == 512);
			SetPalette(palFile.DefaultPalette, true);
		}

		public override bool Supports(FrameFormat format)
		{
			switch (format) {
				case FrameFormat.Gray2:
				case FrameFormat.Gray4:
					return true;
				default:
					return false;
			}
		}

		protected override void ConvertClocked(DmdFrame frame)
		{
			if (ReuseFrameBuffers) {
				using (_frameBuffers.Enter()) ConvertFrame(frame);
			} else {
				using (FrameBufferPool.Suspend()) ConvertFrame(frame);
			}
		}

		private void ConvertFrame(DmdFrame frame)
		{
			// Repeated input must not retrigger mappings, but a timed palette reset needs a redraw.
			var refreshPalette = _paletteResetAt != 0 && System.Diagnostics.Stopwatch.GetTimestamp() >= _paletteResetAt;
			if (refreshPalette) {
				_paletteResetAt = 0;
				if (_defaultPalette != null) SetPalette(_defaultPalette);
			}
			var isNewFrame = IsNewFrame(frame);
			if (isNewFrame) {
				RememberTriggerFrame(frame);
			} else if (!refreshPalette && (_activeFrameSeq == null || !_activeFrameSeq.AdvancesOnRepeatedFrame)) {
				// Retain the last output without advancing or expiring a static scene.
				return;
			}

			var data = frame.Data;
			if (frame.BitLength == 4 && _palFile.Palettes.Length > 1 && _animations == null) {

				if (data[0] == 0x08 && data[1] == 0x09 && data[2] == 0x0a && data[3] == 0x0b) {
					uint newPal = (uint)data[5] * 8 + data[4];

					// remove the marker from a copy, so the received frame can still be compared with the next one.
					var embedded = _embeddedData.Get(data.Length);
					Buffer.BlockCopy(data, 0, embedded, 0, data.Length);
					data = embedded;
					for (int i = 0; i < 6; i++) {
						data[i] = 0;
					}

					if (newPal != _lastEmbedded) {
						LoadPalette(newPal);
						if (!_palFile.DefaultPalette.IsPersistent) {
							_resetEmbedded = true;
						}
						_lastEmbedded = (int)newPal;
					}
				} else if (isNewFrame && _resetEmbedded) {
					_lastEmbedded = _palFile.DefaultPaletteIndex;
					SetPalette(_palFile.DefaultPalette);
					_resetEmbedded = false;
				}
			}

			var planes = FrameUtil.Split(frame.Dimensions, frame.BitLength, data);
			if (isNewFrame) {

				// Multi-frame color masks move on with new input, regardless of the current frame's delay.
				if (_activeFrameSeq != null && _activeFrameSeq.NumFrames > 1
					&& (_activeFrameSeq.SwitchMode == SwitchMode.ColorMask || _activeFrameSeq.SwitchMode == SwitchMode.ColorMaskLoop)) {
					_activeFrameSeq.ExpireFrameDelay();
				}

				if (_palFile.Mappings != null) {
					if (frame is RawFrame vd && vd.RawPlanes.Length > 0) {
						var triggerPlanes = _triggerPlanes.Get(vd.TotalPlanes);
						for (var i = 0; i < triggerPlanes.Length; i++) {
							triggerPlanes[i] = RawPlane(vd, i);
						}
						TriggerAnimation(triggerPlanes, false);

					} else {
						TriggerAnimation(planes, false);
					}
				}
			}

			// if an animation is playing, render it instead of the normal frame
			if (_activeFrameSeq != null) {
				_activeFrameSeq.ScalerMode = ScalerMode;
				_activeFrameSeq.NextFrame(frame.Dimensions, planes, _animationFinished, renderInputOnCompletion: isNewFrame);
				return;
			}

			// Sisch diräkt uisgäh
			Render(frame.Dimensions, planes);
		}

		/// <summary>
		/// Checks whether a frame differs from the last one that was checked for triggers.
		/// </summary>
		private bool IsNewFrame(DmdFrame frame)
		{
			var previous = _lastTriggerFrame.Value;
			if (previous == null || frame.Dimensions != previous.Dimensions || frame.BitLength != previous.BitLength
				|| !FrameUtil.CompareBuffersFast(frame.Data, previous.Data)) return true;
			var raw = frame as RawFrame;
			var previousRaw = previous as RawFrame;
			var count = raw?.TotalPlanes ?? 0;
			if (count != (previousRaw?.TotalPlanes ?? 0)) return true;
			for (var i = 0; i < count; i++) {
				if (!FrameUtil.CompareBuffersFast(RawPlane(raw, i), RawPlane(previousRaw, i))) return true;
			}
			return false;
		}

		private void RememberTriggerFrame(DmdFrame frame) => _lastTriggerFrame.Copy(frame);

		private static byte[] RawPlane(RawFrame frame, int index) => index < frame.RawPlanes.Length
			? frame.RawPlanes[index] : frame.ExtraRawPlanes[index - frame.RawPlanes.Length];

		public void LoadPalette(uint newpal)
		{
			if (_palFile.Palettes.Length > newpal) {
				SetPalette(_palFile.GetPalette(newpal));
			} else {
				Logger.Warn("[vni] No palette for change to " + newpal);
			}
		}

		/// <summary>
		/// Tuät s Biud durähäschä, luägt obs än Animazion uisleest odr Palettä setzt und macht das grad.
		/// </summary>
		public void ActivateMapping(Mapping mapping)
		{
			if (mapping.Mode == SwitchMode.Event)
			{
				return;
			}

			// If same LCM scene, no need to stop/start 
			if (_activeFrameSeq != null && _activeFrameSeq.SwitchMode == SwitchMode.LayeredColorMask && mapping.Mode == SwitchMode.LayeredColorMask && mapping.Offset == _activeFrameSeq.Offset)
			{
				return;
			}

			// If same LRM scene, no need to stop/start
			if (_activeFrameSeq != null && _activeFrameSeq.SwitchMode == SwitchMode.MaskedReplace && mapping.Mode == SwitchMode.MaskedReplace && mapping.Offset == _activeFrameSeq.Offset)
			{
				return;
			}

			// Modes 8 and 9 don't restart when their trigger shows up again while playing
			if (_activeFrameSeq != null && (_activeFrameSeq.SwitchMode == SwitchMode.ReplaceNoRetrigger || _activeFrameSeq.SwitchMode == SwitchMode.ColorMaskLoop)
			    && mapping.IsAnimation && mapping.Offset == _activeFrameSeq.Offset)
			{
				return;
			}

			// Faus scho eppis am laifä isch, ahautä
			_activeFrameSeq?.Stop();
			_activeFrameSeq = null;

			// Palettä ladä
			var palette = _palFile.GetPalette(mapping.PaletteIndex);
			if (palette == null)
			{
				Logger.Warn("[vni] No palette found at index {0}.", mapping.PaletteIndex);
				return;
			}
			//Logger.Debug("[vni] Setting palette {0} of {1} colors.", mapping.PaletteIndex, palette.Colors.Length);
			SetPalette(palette);
			// Expire on the next colorizer tick; no per-trigger scheduler or closure is needed.
			_paletteResetAt = !mapping.IsAnimation && mapping.Duration > 0
				? System.Diagnostics.Stopwatch.GetTimestamp() + (long)mapping.Duration * System.Diagnostics.Stopwatch.Frequency / 1000
				: 0;

			// Animazionä
			if (mapping.IsAnimation)
			{
				
				// Luägä ob ibrhaipt äs VNI/FSQ Feil umä gsi isch
				if (_animations == null)
				{
					Logger.Warn("[vni] Tried to load animation but no animation file loaded.");
					return;
				}
				_activeFrameSeq = _animations.Find(mapping);

				if (_activeFrameSeq == null)
				{
					Logger.Warn("[vni] Cannot find animation at position {0}.", mapping.Offset);
					return;
				}

				_activeFrameSeq.Start(mapping.Mode, _render, _animationFinished);
			}
		}

		private void TriggerAnimation(byte[][] planes, bool reverse)
		{
			uint nomaskcrc = 0;
			bool clear = true;

			for (var i = 0; i < planes.Length; i++)
			{
				var mapping = FindMapping(planes[i], reverse, out nomaskcrc);

				// Faus niid gfundä hemmr fertig
				if (mapping != null)
				{
					if (Logger.IsDebugEnabled) Logger.Debug("[vni] Detect hash {0:X} for mode {1}", mapping.Checksum, mapping.Mode);

					ActivateMapping(mapping);
					// Can exit if not LCM sceene.
					if (_activeFrameSeq != null && _activeFrameSeq.SwitchMode != SwitchMode.LayeredColorMask && _activeFrameSeq.SwitchMode != SwitchMode.MaskedReplace)
						return;

				}
				if (_activeFrameSeq != null)
				{
					if (_activeFrameSeq.SwitchMode == SwitchMode.LayeredColorMask || _activeFrameSeq.SwitchMode == SwitchMode.MaskedReplace)
						clear = _activeFrameSeq.DetectLCM(planes[i], nomaskcrc, reverse, clear);
					else if (_activeFrameSeq.SwitchMode == SwitchMode.Follow || _activeFrameSeq.SwitchMode == SwitchMode.FollowReplace)
						_activeFrameSeq.DetectFollow(planes[i], nomaskcrc, _palFile.Masks, reverse);
				}
			}
		}

		/// <summary>
		/// Tuät Bitplane fir Bitplane häschä unds erschtä Mäpping wo gfundä
		/// wordä isch zrugg gäh.
		/// </summary>
		/// <param name="plane"></param>
		/// <param name="reverse"></param>
		/// <param name="noMaskCrc"></param>
		/// <returns>Mäpping odr null wenn nid gfundä</returns>
		private Mapping FindMapping(byte[] plane, bool reverse, out uint noMaskCrc)
		{
			noMaskCrc = 0;
			var checksum = FrameUtil.Checksum(plane, reverse);
			noMaskCrc = checksum;

			// events aren't supported, so they shouldn't hide a palette or animation mapping found through a mask.
			var mapping = _palFile.FindMapping(checksum);
			if (mapping != null && mapping.Mode != SwitchMode.Event)
			{
				return mapping;
			}

			// Wenn kä Maskä definiert, de nächschti Bitplane
			if (_palFile.Masks == null || _palFile.Masks.Length <= 0)
				return null;

			// Sisch gemmr Maskä fir Maskä durä und luägid ob da eppis passt
			foreach (var mask in _palFile.Masks)
			{
				if (mask.Length != plane.Length)
					continue;
				checksum = FrameUtil.ChecksumWithMask(plane, mask, reverse);
				mapping = _palFile.FindMapping(checksum);
				if (mapping != null && mapping.Mode != SwitchMode.Event)
				{
					return mapping;
				}
			}
			
			return null;
		}

		/// <summary>
		/// Tuäts Biud uif diä entschprächändä Sourcä uisgäh.
		/// </summary>
		/// <param name="dim">Frame dimensions</param>
		/// <param name="planes">S Biud zum uisgäh</param>
		private void Render(Dimensions dim, byte[][] planes)
		{
			// We want to do the scaling after the animations get triggered.
			if (_animations != null && dim * 2 == _animations.Dimensions) {
				planes = ScalerMode == ScalerMode.Scale2x
					? FrameUtil.Scale2X(dim, planes)
					: FrameUtil.ScaleDouble(dim, planes);
				dim *= 2;
			}

			// true color animations come as 24 (8 bits per channel) or 15 (5 bits per channel) bit planes.
			if (planes.Length == 24 || planes.Length == 15) {
				DedupedRgb24Source.NextFrame(FrameBufferPool.Create(dim, PlanesToRgb24(dim, planes), 24));
				return;
			}

			var palette = _palette.GetColors(planes.Length);
			var data = FrameUtil.Join(dim, planes);
			var coloredFrame = FrameBufferPool.Create(dim, data, palette);

			switch (planes.Length) {
				case 2:
					DedupedColoredGray2Source.NextFrame(coloredFrame);
					break;
				case 4:
					DedupedColoredGray4Source.NextFrame(coloredFrame);
					break;
				case 6:
					DedupedColoredGray6Source.NextFrame(coloredFrame);
					break;
			}
		}

		/// <summary>
		/// Converts the bit planes of a true color animation frame to RGB24. The planes
		/// are blue, green and red, each starting with the least significant bit.
		/// </summary>
		/// <param name="dim">Frame dimensions</param>
		/// <param name="planes">15 or 24 bit planes</param>
		/// <returns>RGB24 frame data</returns>
		public static byte[] PlanesToRgb24(Dimensions dim, byte[][] planes)
		{
			var bitsPerColor = planes.Length / 3;
			var shift = 8 - bitsPerColor;
			var rgb = FrameBufferPool.Allocate<byte>(dim.Surface * 3);
			for (var pixel = 0; pixel < dim.Surface; pixel++) {
				var pos = pixel >> 3;
				var bit = 1 << (pixel & 7);
				int red = 0, green = 0, blue = 0;
				for (var i = 0; i < bitsPerColor; i++) {
					if ((planes[i][pos] & bit) != 0) {
						blue |= 1 << i;
					}
					if ((planes[bitsPerColor + i][pos] & bit) != 0) {
						green |= 1 << i;
					}
					if ((planes[2 * bitsPerColor + i][pos] & bit) != 0) {
						red |= 1 << i;
					}
				}
				rgb[pixel * 3] = (byte)(red << shift);
				rgb[pixel * 3 + 1] = (byte)(green << shift);
				rgb[pixel * 3 + 2] = (byte)(blue << shift);
			}
			return rgb;
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="palette">Diä nii Palettä</param>
		/// <param name="isDefault"></param>
		public void SetPalette(Palette palette, bool isDefault = false)
		{
			if (palette == null) {
				Logger.Warn("[vni] Ignoring null palette.");
				return;
			}
			if (isDefault) {
				_defaultPalette = palette;
			}
			//Logger.Debug("[vni] Setting new palette: [ {0} ]", string.Join(" ", palette.Colors.Select(c => c.ToString())));
			_palette = palette;
		}

		/// <summary>
		/// Wird uisgfiährt wenn än Animazion fertig isch, cha irgend ä Modus si.
		/// </summary>
		protected void AnimationFinished()
		{
			//Logger.Trace("[vni] [timing] Animation finished.");
			//LastChecksum = 0x0;
			SetPalette(_defaultPalette);
			_activeFrameSeq = null;
		}

#if !LIBDMD_CORE
		public void DumpAnimations(string path)
		{
			foreach (var offset in _palFile.Mappings.Keys) {
				var mapping = _palFile.Mappings[offset];
				var anim = _animations.Find(mapping.Offset);
				if (anim == null) {
					Logger.Info($"Could not find animation for offset {offset}.");
					continue;
				}
				Logger.Info($"Dumping animation {anim.Name} at mapping offset {mapping.Offset} (at {offset}).");
				anim.Dump(path, mapping, _palFile.Palettes);
			}
		}
#endif
	}
}
