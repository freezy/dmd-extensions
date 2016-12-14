using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter
{
	public abstract class AbstractColorizer 
	{
		public readonly int Width;
		public readonly int Height;

		protected readonly Coloring Coloring;
		protected readonly Animation[] Animations;
		protected readonly byte[] ColoredFrame;
		protected readonly BehaviorSubject<Color[]> Palette = new BehaviorSubject<Color[]>(new[]{Colors.Black, Colors.DarkOrchid});

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		protected Animation CurrentAnimation;
		protected readonly Subject<byte[]> AnimationFrames = new Subject<byte[]>();
		protected bool IsAnimationRunning => CurrentAnimation != null && CurrentAnimation.IsRunning;

		protected AbstractColorizer(int width, int height, Coloring coloring, Animation[] animations)
		{
			Width = width;
			Height = height;
			Coloring = coloring;
			Animations = animations;
			ColoredFrame = new byte[width * height * 3];
			SetPalette(Coloring.DefaultPalette.Colors);
			Logger.Debug("[colorize] Initialized.");
		}

		protected bool ApplyMapping(uint checksum, bool masked)
		{
			var mapping = Coloring.FindMapping(checksum);
			if (mapping == null) {
				return false;
			}
			if (mapping.Mode == 0) {
				var palette = Coloring.GetPalette(mapping.PaletteIndex);
				if (palette == null) {
					Logger.Warn("[colorize] No palette found at index {0} for {1} frame.", mapping.PaletteIndex, masked ? "masked" : "unmasked");
					return false;
				}
				Logger.Info("[colorize] Setting palette of {0} colors via {1} frame.", palette.Colors.Length, masked ? "masked" : "unmasked");
				SetPalette(palette.Colors);
				return true;
			}
			if (mapping.Mode == 1) {
				if (mapping.PaletteIndex >= Animations.Length) {
					Logger.Warn("[colorize] No animation found at index {0} for {1} frame.", mapping.PaletteIndex, masked ? "masked" : "unmasked");
					return false;
				}
				Logger.Info("[colorize] Playing animation of {0} frames via {1} frame.", Animations[mapping.PaletteIndex].Frames.Length, masked ? "masked" : "unmasked");
				CurrentAnimation = Animations[mapping.PaletteIndex];
				CurrentAnimation.Start(AnimationFrames, Palette);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="colors">Diä niiä Farbä vord Palettä</param>
		public void SetPalette(Color[] colors)
		{
			if (colors == null) {
				Logger.Warn("[colorize] Ignoring null palette.");
			}
			Logger.Debug("[colorize] Setting new palette:");
			Array.ForEach(colors, c => Logger.Trace("   " + c));

			Palette.OnNext(ColorUtil.GetPalette(colors, 16));
		}

		/// <summary>
		/// Tuät d Palettä wo grad bruichd wird mitärän andärä uiswächslä.
		/// </summary>
		/// <param name="index">Dr Index fo dr niiä Palettä wo vom Palettä-Feil gläsä wordä isch</param>
		public void LoadPalette(uint index)
		{
			var palette = Coloring.GetPalette(index);
			if (palette != null) {
				Logger.Info("[colorize] Setting palette of {0} colors via side channel...", palette.Colors.Length);
				SetPalette(palette.Colors);

			} else {
				Logger.Warn("[colorize] No palette with index {0} found to load through side channel.", index);
			}
		}

		public IObservable<byte[]> GetRgb24Frames()
		{
			return AnimationFrames;
		}

	}
}
