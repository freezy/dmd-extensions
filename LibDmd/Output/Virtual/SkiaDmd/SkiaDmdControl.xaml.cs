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


		private int _call = 0;
		private Stopwatch _stopwatch = new Stopwatch();
		private WriteableBitmap _writeableBitmap;

		public SkiaDmdControl()
		{
			InitializeComponent();

			SizeChanged += OnSizeChanged;
			CompositionTarget.Rendering += (o, e) => DrawImage(_writeableBitmap);
		}

		public void Init()
		{
			// ReSharper disable once SuspiciousTypeConversion.Global
			if (this is IFixedSizeDestination) {
				SetDimensions(DmdWidth, DmdHeight);
			}
			ObservableExtensions.Subscribe(Host.WindowResized, pos => CreateImage((int)pos.Width, (int)pos.Height));
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
			/*try {
				Dispatcher.Invoke(() => DmdImage.Source = bmp);
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Virtual DMD renderer task seems to be lost.");
			}*/
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


		private void SetBitmap(WriteableBitmap bitmap)
		{
			DmdImage.Source = _writeableBitmap = bitmap;
			Logger.Info("Bitmap set!");
		}

		public void CreateImage(int width, int height)
		{
			SetBitmap(new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent));
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (writeableBitmap == null) {
				return;
			}

			int width = (int)writeableBitmap.Width,
				height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			using (var surface = SKSurface.Create(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, writeableBitmap.BackBuffer, width * 4)) {
				var canvas = surface.Canvas;
				var x = 30;
				var paint = new SKPaint() { Color = new SKColor(0, 0, 0), TextSize = 16 };
				canvas.Clear(new SKColor(130, 130, 130));
				if (_call == 0) {
					_stopwatch.Start();
				}
				var fps = _call / ((_stopwatch.Elapsed.TotalSeconds > 0) ? _stopwatch.Elapsed.TotalSeconds : 1);
				canvas.DrawText($"FPS: {fps:0}, frames: {_call++}", x, 50, paint);
			}

			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
		}
	}
}
