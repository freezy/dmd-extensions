using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;
using SkiaSharp;

namespace LibDmd.Output.Virtual
{
	class SegGenerator
	{
		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private int _call = 0;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private readonly Dictionary<int, SkiaSharp.Extended.Svg.SKSvg> _segments =
			new Dictionary<int, SkiaSharp.Extended.Svg.SKSvg>();

		private AlphaNumericFrame _frame;

		public SegGenerator()
		{
			var prefix = "LibDmd.Output.Virtual.alphanum.";
			var segFilenames = new[]
			{
				$"{prefix}00-top.svg",
				$"{prefix}01-top-right.svg",
				$"{prefix}02-bottom-right.svg",
				$"{prefix}03-bottom.svg",
				$"{prefix}04-bottom-left.svg",
				$"{prefix}05-top-left.svg",
				$"{prefix}06-middle-left.svg",
				$"{prefix}07-comma.svg",
				$"{prefix}08-diag-top-left.svg",
				$"{prefix}09-center-top.svg",
				$"{prefix}10-diag-top-right.svg",
				$"{prefix}11-middle-right.svg",
				$"{prefix}12-diag-bottom-right.svg",
				$"{prefix}13-center-bottom.svg",
				$"{prefix}14-diag-bottom-left.svg",
				$"{prefix}15-dot.svg",
			};

			Logger.Info("Loading segment SVGs...");
			for (var i = 0; i < segFilenames.Length; i++) {
				var svg = new SkiaSharp.Extended.Svg.SKSvg();
				svg.Load(_assembly.GetManifestResourceStream(segFilenames[i]));
				_segments.Add(i, svg);
			}
		}

		public WriteableBitmap CreateImage(int width, int height)
		{
			return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32,
				BitmapPalettes.Halftone256Transparent);
		}

		public void UpdateFrame(AlphaNumericFrame frame)
		{
			_frame = frame;
		}

		private void DrawSegment(int position, SKPaint paint, SKCanvas canvas, SKMatrix matrix)
		{
			var seg = _frame.SegmentData[position];
			for (var j = 0; j < 16; j++) {
				if (((seg >> j) & 0x1) != 0) {
					var svg = _segments[j];
					canvas.DrawPicture(svg.Picture, ref matrix, paint);
				}
			}
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (_frame == null) {
				return;
			}

			int width = (int)writeableBitmap.Width,
				height = (int)writeableBitmap.Height;

			var numChars = 20;
			var lines = 2;
			var padding = 20;

			writeableBitmap.Lock();

			using (var surface = SKSurface.Create(width, height, SKColorType.Bgra8888, SKAlphaType.Premul,
				writeableBitmap.BackBuffer, width * 4)) {
				using (var svgPaint = new SKPaint()) {

					var canvas = surface.Canvas;

					var paint = new SKPaint { Color = new SKColor(255, 255, 255), TextSize = 10 };

					var svgSize = _segments[0].Picture.CullRect;
					float svgWidth = (width - (2f * padding)) / numChars;
					float scale = svgWidth / svgSize.Width;
					float svgHeight = svgSize.Height * scale;
					var matrix = SKMatrix.MakeScale(scale, scale);
					matrix.TransX = padding;
					matrix.TransY = padding;

					canvas.Clear(new SKColor(0, 0, 0));

					for (var j = 0; j < lines; j++) {
						for (var i = 0; i < numChars; i++) {
							svgPaint.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.OrangeRed, SKBlendMode.SrcIn);
							DrawSegment(i + 20 * j, svgPaint, canvas, matrix);
							matrix.TransX += svgWidth;
						}
						matrix.TransX = padding;
						matrix.TransY += svgHeight + 10;
					}

					if (_call == 0) {
						_stopwatch.Start();
					}

					double fps = _call / (_stopwatch.Elapsed.TotalSeconds != 0 ? _stopwatch.Elapsed.TotalSeconds : 1);
					canvas.DrawText($"FPS: {fps:0}", 0, 10, paint);
					canvas.DrawText($"Frames: {this._call++}", 50, 10, paint);
				}
			}

			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}
	}
}