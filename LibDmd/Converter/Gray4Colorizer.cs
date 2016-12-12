using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using NLog;

namespace LibDmd.Converter
{
	public class Gray4Colorizer : IConverter
	{

		public readonly int Width;
		public readonly int Height;
		public readonly Color DefaultColor = Colors.DarkOrchid;

		public RenderBitLength From { get; } = RenderBitLength.Gray4;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		private readonly Coloring _coloring;
		private readonly byte[] _coloredFrame;
		private Color[] _palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Gray4Colorizer(int width, int height, string filename)
		{
			Logger.Info("[colorize] Loading palette file at {0}...", filename);
			Width = width;
			Height = height;
			_coloring = new Coloring(filename);
			_coloredFrame = new byte[width * height * 3];
			SetPalette(new[] { Colors.Black, DefaultColor });
			Logger.Debug("[colorize] Initialized.");
		}

		public void SetPalette(Color[] colors)
		{
			Logger.Debug("[colorize] Setting new palette:");
			Array.ForEach(colors, c => Logger.Trace("   " + c));

			_palette = ColorUtil.GetPalette(colors, 16);
		}

		public byte[] Convert(byte[] frame)
		{
			ColorUtil.ColorizeFrame(Width, Height, frame, _palette, _coloredFrame);
			return _coloredFrame;
		}

		public void LoadPalette(uint index)
		{
			var palette = _coloring.GetPalette(index);
			if (palette != null) {
				Logger.Info("[colorize] Setting palette of {0} via side channel...", palette.Colors.Length);
				SetPalette(palette.Colors);

			} else {
				Logger.Warn("[colorize] No palette with index {0} found to load through side channel.", index);
			}
		}
	}
}
