﻿using System;
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

		private readonly SKColor _foregroundColor = SKColors.OrangeRed;
		private readonly SKColor _backgroundColor = SKColors.Black;

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private int _call = 0;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private readonly Dictionary<int, SkiaSharp.Extended.Svg.SKSvg> _segments = new Dictionary<int, SkiaSharp.Extended.Svg.SKSvg>();
		private readonly Dictionary<int, SKSurface> _segmentsBmp = new Dictionary<int, SKSurface>();

		private SkiaSharp.Extended.Svg.SKSvg _fullSvg = new SkiaSharp.Extended.Svg.SKSvg();
		private AlphaNumericFrame _frame;

		private float _svgWidth = 0;
		private float _svgHeight = 0;

		private const int NumSegments = 20;
		private const int NumLines = 2;
		private const int Padding = 20;

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
			_fullSvg.Load(_assembly.GetManifestResourceStream($"{prefix}full.svg"));
		}

		public WriteableBitmap CreateImage(int width, int height)
		{
			var svgSize = _segments[0].Picture.CullRect;
			_svgWidth = (width - (2f * Padding)) / NumSegments;
			var scale = _svgWidth / svgSize.Width;
			_svgHeight = svgSize.Height * scale;

			var matrix = SKMatrix.MakeScale(scale, scale);
			var info = new SKImageInfo((int)_svgWidth, (int)_svgHeight);

			using (var svgPaint = new SKPaint()) {
				svgPaint.ColorFilter = SKColorFilter.CreateBlendMode(_foregroundColor, SKBlendMode.SrcIn);
				foreach (var i in _segments.Keys) {
					var surface = SKSurface.Create(info);
					surface.Canvas.DrawPicture(_segments[i].Picture, ref matrix, svgPaint);
					_segmentsBmp[i] = surface;
				}
			}
			return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
		}

		public void UpdateFrame(AlphaNumericFrame frame)
		{
			_frame = frame;
		}

		private void DrawSegment(int num, SKCanvas canvas, SKPoint position)
		{
			var seg = _frame.SegmentData[num];
			for (var j = 0; j < 16; j++) {
				if (((seg >> j) & 0x1) != 0) {
					canvas.DrawSurface(_segmentsBmp[j], position);
				}
			}
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (_frame == null || _segmentsBmp.Count == 0) {
				return;
			}

			int width = (int)writeableBitmap.Width,
				height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			using (var surface = SKSurface.Create(width, height, SKColorType.Bgra8888, SKAlphaType.Premul,
				writeableBitmap.BackBuffer, width * 4)) {

				var canvas = surface.Canvas;
				var paint = new SKPaint { Color = SKColors.White, TextSize = 10 };

				float transX = Padding;
				float transY = Padding;

				canvas.Clear(_backgroundColor);

				for (var j = 0; j < NumLines; j++) {
					for (var i = 0; i < NumSegments; i++) {
						DrawSegment(i + 20 * j, canvas, new SKPoint(transX, transY));
						transX += _svgWidth;
					}
					transX = Padding;
					transY += _svgHeight + 10;
				}

				if (_call == 0) {
					_stopwatch.Start();
				}

				var fps = _call / (_stopwatch.Elapsed.TotalSeconds != 0 ? _stopwatch.Elapsed.TotalSeconds : 1);
				canvas.DrawText($"FPS: {fps:0}", 0, 10, paint);
				canvas.DrawText($"Frames: {this._call++}", 50, 10, paint);
			}

			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}
	}
}