using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual.SkiaDmd.GLContext;
using NLog;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	/// <summary>
	/// Interaction logic for SkiaDmdWindow.xaml
	/// </summary>
	public partial class SkiaDmdControl : VirtualDisplay, IRgb24Destination, IBitmapDestination, IResizableDestination, IVirtualControl
	{
		public override IVirtualControl VirtualControl => this;
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

		private readonly Configuration _config;
		private DmdStyleDefinition _styleDef;

		private SKSurface _surface;
		private GLUtil _glUtil;
		private SKSize _canvasSize;
		private SKSurface _dotSurface;

		private bool _settingsOpen;
		private VirtualDmdSettings _settingWindow;
		private IDisposable _settingSubscription;

		private byte[] _frame;

		public SkiaDmdControl(DmdStyleDefinition styleDef, Configuration config)
		{
			_styleDef = styleDef;
			_config = config;

			InitializeComponent();
			Initialize();
			CompositionTarget.Rendering += (o, e) => 

			_glUtil = GLUtil.GetInstance();

			SettingsPath.Fill = new SolidColorBrush(Colors.Transparent);
			SettingsButton.MouseEnter += (sender, e) => {
				SettingsPath.Fill = new SolidColorBrush(Color.FromArgb(0x60, 0xff, 0xff, 0xff));
				SettingsPath.Stroke = new SolidColorBrush(Color.FromArgb(0x60, 0x0, 0x0, 0x0));
			};
			SettingsButton.MouseLeave += (sender, e) => {
				SettingsPath.Fill = new SolidColorBrush(Colors.Transparent);
				SettingsPath.Stroke = new SolidColorBrush(Colors.Transparent);
			};
			SettingsButton.MouseLeftButtonDown += ToggleDisplaySettings;
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
			//Host.SetDimensions(width, height);
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			try {
				Dispatcher.Invoke(() => {
					_frame = ImageUtil.ConvertToRgb24(bmp);
					Redraw();
				});
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

		private void OnPaintCanvas(object sender, SKPaintSurfaceEventArgs e)
		{
			OnPaintSurface(e.Surface.Canvas, e.Info.Width, e.Info.Height);
		}

		private void OnPaintSurface(SKCanvas canvas, int width, int height)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			var canvasSize = new SKSize(width, height);
			if (_canvasSize != canvasSize) {
				Logger.Info("Setting up OpenGL context at {0}x{1}...", width, height);
				_surface?.Dispose();
				_surface = _glUtil.CreateSurface(width, height);
				_canvasSize = canvasSize;
			}
			DrawDmd(_surface.Canvas, width, height);
			canvas.DrawSurface(_surface, new SKPoint(0f, 0f));
			stopwatch.Stop();
			var paintTime = stopwatch.ElapsedMilliseconds;
			
			// render fps
			if (paintTime > 0) {
				using (var fpsPaint = new SKPaint())
				{
					fpsPaint.Color = new SKColor(0, 0xff, 0);
					fpsPaint.TextSize = 20;
					var fps = 1000d / paintTime;
					canvas.DrawText($"FPS: {fps:0}", 30, 50, fpsPaint);
				}
			}
		}

		public void DrawDmd(SKCanvas canvas, int width, int height)
		{
			if (_frame == null) {
				return;
			}

			// render dmd
			var data = new DmdData(_frame, DmdWidth, DmdHeight);
			DmdPainter.Paint(data, canvas, width, height, _styleDef);
		}

		private void ToggleDisplaySettings(object sender, MouseButtonEventArgs mouseButtonEventArgs)
		{
			if (_settingWindow == null) {
				_settingWindow = new VirtualDmdSettings(_styleDef, Top, Left + Width, _config);
				_settingWindow.IsVisibleChanged += (visibleSender, visibleEvent) => _settingsOpen = (bool)visibleEvent.NewValue;
				_settingSubscription = _settingWindow.OnStyleApplied.Subscribe(style => {
					Logger.Info("Applying new style to DMD.");
					_styleDef = style;
					Redraw();
				});
			}

			if (!_settingsOpen) {
				_settingWindow.Show();
			} else {
				_settingWindow.Hide();
			}
		}

		private void Redraw()
		{
			BitmapHost.InvalidateVisual();
		}

		private void OnWindowClosing(object sender, CancelEventArgs e)
		{
			_dotSurface?.Dispose();
			_surface?.Dispose();
			_glUtil.Destroy();
		}
	}
}
