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

		public WriteableBitmap CreateImage(int width, int height)
		{
			return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
		}

		public void UpdateImage(WriteableBitmap writeableBitmap)
		{
			int width = (int)writeableBitmap.Width,
				height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			using (var surface = SKSurface.Create(
				width: width,
				height: height,
				colorType: SKColorType.Bgra8888,
				alphaType: SKAlphaType.Premul,
				pixels: writeableBitmap.BackBuffer,
				rowBytes: width * 4)) {
				var canvas = surface.Canvas;

				const int x = 30;
				var paint = new SKPaint() { Color = new SKColor(255, 255, 255), TextSize = 100 };
				var svg = new SKSvg();

				svg.Load(_assembly.GetManifestResourceStream("LibDmd.Output.Virtual.alphanum.svg"));

				canvas.Clear(new SKColor(130, 130, 130));
				canvas.DrawPicture(svg.Picture);
				canvas.DrawText("SkiaSharp on Wpf!", x, 200, paint);
				if (this._call == 0)
					this._stopwatch.Start();
				double fps = _call / (_stopwatch.Elapsed.TotalSeconds != 0 ? _stopwatch.Elapsed.TotalSeconds : 1);
				canvas.DrawText($"FPS: {fps:0}", x, 300, paint);
				canvas.DrawText($"Frames: {this._call++}", x, 400, paint);
			}

			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}
	}
}
