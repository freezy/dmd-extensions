using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibDmd.Common;
using NLog;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	/// <summary>
	/// Interaction logic for SkiaDmdControl.xaml
	/// </summary>
	public partial class SkiaDmdControl : IRgb24Destination, IBitmapDestination, IResizableDestination, IVirtualControl
	{
		public bool IgnoreAspectRatio { get; set; }
		public VirtualDisplay Host { get; set; }
		public bool IsAvailable => true;

		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get; private set; } = 32;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private double _hue;
		private double _sat;
		private double _lum;

		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		private SKSize _canvasSize;
		private SKSurface _dot;
		private SKCanvas _canvas;

		private byte[] _frame;

		private int _call;
		private Stopwatch _stopwatch = new Stopwatch();
		private WriteableBitmap _writeableBitmap;

		public SkiaDmdControl()
		{
			InitializeComponent();
			CompositionTarget.Rendering += (o, e) => DmdSurface.InvalidateVisual();
		}

		public void Init()
		{
			// ReSharper disable once SuspiciousTypeConversion.Global
			if (this is IFixedSizeDestination) {
				SetDimensions(DmdWidth, DmdHeight);
			}
			//ObservableExtensions.Subscribe(Host.WindowResized, pos => CreateImage((int)pos.Width, (int)pos.Height));
		}

		public void SetDimensions(int width, int height)
		{
			Logger.Info("Resizing Skia DMD to {0}x{1}", width, height);
			DmdWidth = width;
			DmdHeight = height;
			Host.SetDimensions(width, height);
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			try {
				Dispatcher.Invoke(() => _frame = ImageUtil.ConvertToRgb24(bmp));
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Virtual DMD renderer task seems to be lost.");
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			throw new NotImplementedException();
		}

		public void SetColor(Color color)
		{
			ColorUtil.RgbToHsl(color.R, color.G, color.B, out _hue, out _sat, out _lum);
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);
		}

		public void ClearDisplay()
		{
		}

		public void ClearPalette()
		{
			_gray2Palette = null;
			_gray4Palette = null;
		}

		public void ClearColor()
		{
			SetColor(RenderGraph.DefaultColor);
		}

		public void Dispose()
		{
		}

		private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
		{
			// the the canvas and properties
			var canvas = e.Surface.Canvas;

			// get the screen density for scaling
			var scale = (float)PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
			var canvasSize = new SKSize(e.Info.Width / scale, e.Info.Height / scale);

			if (_canvasSize != canvasSize) {
				PreRender(canvasSize);
				_canvasSize = canvasSize;
			}
			// handle the device screen density
			canvas.Scale(scale);
			_canvas = canvas;

			DrawImage(canvas);
		}

		private void PreRender(SKSize canvasSize)
		{
			var dotSize = new SKSize(canvasSize.Width / DmdWidth, canvasSize.Height / DmdHeight);
			Logger.Info("Pre-rendering dot at {0}x{1} for {2}x{3}", dotSize.Width, dotSize.Height, canvasSize.Width, canvasSize.Height);
			var dotPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
			var dotRadius = Math.Min(dotSize.Width, dotSize.Height) / 2;
			_dot = SKSurface.Create(new SKImageInfo((int)dotSize.Width, (int)dotSize.Height));
			_dot.Canvas.DrawCircle(dotSize.Width / 2, dotSize.Height / 2, dotRadius, dotPaint);
		}

		public void DrawImage(SKCanvas canvas)
		{
			if (_frame == null || _canvas == null) {
				Logger.Info("Frame is null, aborting.");
				return;
			}
			canvas.Clear(new SKColor(0, 0, 0));
			var dotSize = new SKPoint(_canvasSize.Width / DmdWidth, _canvasSize.Height / DmdHeight);
			for (var y = 0; y < DmdHeight; y++) {
				for (var x = 0; x < DmdWidth * 3; x += 3) {
					
					var framePos = y * DmdWidth * 3 + x;
					var color = new SKColor(_frame[framePos], _frame[framePos + 1], _frame[framePos + 2]);
					var dotPaint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn) };
					var dotPos = new SKPoint(x / 3f * dotSize.X, y * dotSize.Y);
					canvas.DrawSurface(_dot, dotPos, dotPaint);
					//canvas.DrawCircle(dmdPos.X + dotSize.X / 2, dmdPos.Y + dotSize.Y / 2, radius, dotPaint);
				}
			}

			// render fps
			var paint = new SKPaint() { Color = new SKColor(0xff, 0, 0), TextSize = 20 };
			if (_call == 0) {
				_stopwatch.Start();
			}
			var fps = _call / ((_stopwatch.Elapsed.TotalSeconds > 0) ? _stopwatch.Elapsed.TotalSeconds : 1);
			canvas.DrawText($"FPS: {fps:0}, frames: {_call++}", 30, 50, paint);
		}
	}
}
