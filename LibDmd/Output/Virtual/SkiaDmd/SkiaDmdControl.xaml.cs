using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.DmdDevice;
using NLog;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	/// <summary>
	/// Interaction logic for SkiaDmdWindow.xaml
	/// </summary>
	public partial class SkiaDmdControl : IGray2Destination, IGray4Destination, IRgb24Destination, IBitmapDestination, IResizableDestination, IVirtualControl
	{
		public bool IgnoreAspectRatio { get; set; }
		public VirtualDisplay Host { get; set; }
		public bool IsAvailable => true;

		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get; private set; } = 32;

		public Configuration Configuration { get; set; }
		public DmdStyleDefinition StyleDefinition { get; set; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private SKColor _dotColor = new SKColor(RenderGraph.DefaultColor.R, RenderGraph.DefaultColor.G, RenderGraph.DefaultColor.B, RenderGraph.DefaultColor.A);
		private SKColor[] _gray2Palette;
		private SKColor[] _gray4Palette;

		private SKSurface _surface;
		private readonly GLUtil _glUtil;
		private SKSize _canvasSize;

		private bool _settingsOpen;
		private VirtualDmdSettings _settingWindow;
		private IDisposable _settingSubscription;
		private int _numFrame = 0;
		private double _minFps = 0;
		private double _maxFps = 0;
		private double _avgFps;

		private FrameFormat _frameFormat;
		private byte[] _rgb24Frame;
		private byte[] _gray2Frame;
		private byte[] _gray4Frame;

		public SkiaDmdControl()
		{
			InitializeComponent();

			_glUtil = GLUtil.GetInstance();

			SizeChanged += SizeChanged_Event;

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
			ObservableExtensions.Subscribe(Host.WindowResized, pos => {
				Width = pos.Width;
				Height = pos.Height;
				Redraw();
			});
		}

		public void SetDimensions(int width, int height)
		{
			Logger.Info("Resizing Skia DMD to {0}x{1}", width, height);
			DmdWidth = width;
			DmdHeight = height;
			Host.SetDimensions(width, height);
		}

		public void RenderGray2(byte[] frame)
		{
			// skip if identical
			if (FrameUtil.CompareBuffers(_gray2Frame, 0, frame, 0, frame.Length)) {
				return;
			}
			Dispatcher.Invoke(() => {
				_gray2Frame = frame;
				_frameFormat = FrameFormat.Gray2;
				Redraw();
			});
		}

		public void RenderGray4(byte[] frame)
		{
			// skip if identical
			if (FrameUtil.CompareBuffers(_gray4Frame, 0, frame, 0, frame.Length)) {
				return;
			}
			Dispatcher.Invoke(() => {
				_gray4Frame = frame;
				_frameFormat = FrameFormat.Gray4;
				Redraw();
			});
		}

		public void RenderRgb24(byte[] frame)
		{
			// skip if identical
			if (FrameUtil.CompareBuffers(_rgb24Frame, 0, frame, 0, frame.Length)) {
				return;
			}
			Dispatcher.Invoke(() => {
				_rgb24Frame = frame;
				_frameFormat = FrameFormat.Rgb24;
				Redraw();
			});
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			try {
				Dispatcher.Invoke(() => {
					var frame = ImageUtil.ConvertToRgb24(bmp);
					if (FrameUtil.CompareBuffers(_rgb24Frame, 0, frame, 0, frame.Length)) {
						return;
					}
					_rgb24Frame = frame;
					_frameFormat = FrameFormat.Rgb24;
					Redraw();
				});
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Virtual DMD renderer task seems to be lost.");
			}
		}

		public void SetColor(Color color)
		{
			_dotColor = new SKColor(color.R, color.G, color.B, color.A);
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4).Select(color => new SKColor(color.R, color.G, color.B, color.A)).ToArray();
			_gray4Palette = ColorUtil.GetPalette(colors, 16).Select(color => new SKColor(color.R, color.G, color.B, color.A)).ToArray();
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
			_surface?.Dispose();
			_glUtil.Destroy();
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
			canvas.Clear(SKColors.Transparent);
			canvas.DrawSurface(_surface, new SKPoint(0f, 0f));
			stopwatch.Stop();
			var paintTime = stopwatch.ElapsedMilliseconds;
			
			// render fps
			if (Configuration?.VirtualDmd != null && Configuration.VirtualDmd.ShowFps && paintTime > 0) {
				using (var fpsPaint = new SKPaint()) {
					fpsPaint.Color = new SKColor(0, 0xff, 0);
					fpsPaint.TextSize = 20;
					var fps = 1000d / paintTime;
					_minFps = _minFps > 0.0 ? Math.Min(fps, _minFps) : fps;
					_maxFps = Math.Max(_maxFps, fps);
					_avgFps = (_avgFps * _numFrame + fps) / ++_numFrame;
					canvas.DrawText($"FPS: {fps:000} ({_minFps:000}/{_avgFps:000}/{_maxFps:000}), Frame: {_numFrame}", 5, 25, fpsPaint);
				}
			}
		}

		public void DrawDmd(SKCanvas canvas, int width, int height)
		{
			byte[] frame;
			SKColor[] palette = null;
			switch (_frameFormat) {
				case FrameFormat.Gray2:
					frame = _gray2Frame;
					palette = _gray2Palette;
					break;
				case FrameFormat.Gray4:
					frame = _gray4Frame;
					palette = _gray4Palette;
					break;
				case FrameFormat.Rgb24:
					frame = _rgb24Frame;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			if (frame == null) {
				return;
			}

			// render dmd
			var data = new DmdFrame(_frameFormat, frame, DmdWidth, DmdHeight, _dotColor, palette);
			DmdPainter.Paint(data, canvas, width, height, StyleDefinition, true);
		}

		private void ToggleDisplaySettings(object sender, MouseButtonEventArgs mouseButtonEventArgs)
		{
			if (_settingWindow == null) {
				_settingWindow = new VirtualDmdSettings(StyleDefinition, Host.Top, Host.Left + Host.Width, Configuration);
				_settingWindow.IsVisibleChanged += (visibleSender, visibleEvent) => _settingsOpen = (bool)visibleEvent.NewValue;
				_settingWindow.Closed += (closedSender, closedEvent) => _settingWindow = null;
				_settingSubscription = _settingWindow.OnStyleApplied.Subscribe(style => {
					Logger.Info("Applying new style to DMD.");
					StyleDefinition = style;
					Redraw();
				});
			}

			if (!_settingsOpen) {
				_settingWindow.Show();
			} else {
				_settingWindow.Hide();
			}
		}

		private void SizeChanged_Event(object sender, SizeChangedEventArgs e)
		{
			Redraw();
		}

		private void Redraw()
		{
			BitmapHost.InvalidateVisual();
		}
	}
}
