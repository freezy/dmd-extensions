using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

		private readonly Dictionary<int, SKSvg> _segments = new Dictionary<int, SKSvg>();

		public SegGenerator()
		{
			var prefix = "LibDmd.Output.Virtual.alphanum.";
			var segFilenames = new[] {
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
				var svg = new SKSvg();
				svg.Load(_assembly.GetManifestResourceStream(segFilenames[i]));
				_segments.Add(i, svg);
			}
		}

		public WriteableBitmap CreateImage(int width, int height)
		{
			return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
		}

		public void UpdateImage(WriteableBitmap writeableBitmap)
		{
			int width = (int)writeableBitmap.Width,
				height = (int)writeableBitmap.Height;

			var numChars = 20;
			float paddingX = 10;

			writeableBitmap.Lock();

			using (var surface = SKSurface.Create(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, writeableBitmap.BackBuffer, width * 4)) {

				var canvas = surface.Canvas;

				var paint = new SKPaint() { Color = new SKColor(255, 255, 255), TextSize = 10 };
				var svg = _segments[0];
				var svgSize = svg.Picture.CullRect;
				float svgWidth = (width - (paddingX * (numChars - 1))) / (float)numChars;
				float scale = svgWidth / svgSize.Width;
				var matrix = SKMatrix.MakeScale(scale, scale);

				canvas.Clear(new SKColor(130, 130, 130));

				for (var i = 0; i < numChars; i++) {
					canvas.DrawPicture(svg.Picture, ref matrix);
					matrix.TransX += width / (float)numChars;
				}

				if (this._call == 0) {
					this._stopwatch.Start();
				}
				double fps = _call / (_stopwatch.Elapsed.TotalSeconds != 0 ? _stopwatch.Elapsed.TotalSeconds : 1);
				canvas.DrawText($"FPS: {fps:0}", 0, 110, paint);
				canvas.DrawText($"Frames: {this._call++}", 50, 110, paint);
			}

			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}
	}
}
