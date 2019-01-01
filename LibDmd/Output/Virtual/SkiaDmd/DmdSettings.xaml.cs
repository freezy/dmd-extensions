using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual.AlphaNumeric;
using NLog;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	/// <summary>
	/// Interaction logic for AlphaNumericSettings.xaml
	/// </summary>
	public partial class VirtualDmdSettings
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static int Dpi = 96;

		public ISubject<DmdStyleDefinition> OnStyleApplied { get; } = new Subject<DmdStyleDefinition>();

		private DmdStyle _previewStyle;
		private DmdStyleDefinition _previewStyleDefinition;
		private SKSize _previewSize;
		private SKSurface _previewSurface;
		private readonly DmdStyleDefinition _originalStyle;
		private readonly Configuration _config;
		private readonly VirtualDmdConfig _dmdConfig;
		private readonly float _scale;
		private readonly byte[] _previewDmdData;
		private readonly int _previewDmdWidth;
		private readonly int _previewDmdHeight;

		public List<string> StyleNames => _config == null ? null : _dmdConfig.GetStyleNames();
		public string NewStyleName { get; set; }

		public VirtualDmdSettings(DmdStyleDefinition styleDefinition, double top, double left, Configuration config)
		{
			Top = top;
			Left = left;
			_config = config;
			_originalStyle = styleDefinition.Copy();
			_dmdConfig = _config?.VirtualAlphaNumericDisplay as VirtualDmdConfig;

			DataContext = this;
			InitializeComponent();

			_previewSize = new SKSize((float)Preview.Width, (float)Preview.Height);
			_scale = (float) Preview.Width / 1024;
			ApplyStyle(styleDefinition);

			if (config == null) {
				SaveGroup.Visibility = Visibility.Collapsed;
			}

			var previewBitmap = new BitmapImage();
			previewBitmap.BeginInit();
			previewBitmap.UriSource = new Uri("pack://application:,,,/LibDmd;component/Output/Virtual/SkiaDmd/TestImage.png");
			previewBitmap.EndInit();

			_previewDmdData = ImageUtil.ConvertToRgb24(previewBitmap);
			_previewDmdWidth = previewBitmap.PixelWidth;
			_previewDmdHeight = previewBitmap.PixelHeight;

			UpdateControls();
			SetupTriggers();

			// name each layer
			ForegroundStyle.Label = "Foreground Layer";
			InnerGlowStyle.Label = "Inner Glow Layer";
			OuterGlowStyle.Label = "Outer Glow Layer";
			BackgroundStyle.Label = "Unlit Layer";

			// subscribe to control changes that trigger rasterization
			ForegroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyleDef => {
				ApplyLayerStyle(DmdLayer.Foreground, layerStyleDef);
				ReDraw();
			});
			InnerGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				ApplyLayerStyle(DmdLayer.InnerGlow, layerStyle);
				ReDraw();
			});
			OuterGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				ApplyLayerStyle(DmdLayer.OuterGlow, layerStyle);
				ReDraw();
			});
			BackgroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				ApplyLayerStyle(DmdLayer.Background, layerStyle);
				ReDraw();
			});
		}

		private void ApplyStyle(DmdStyleDefinition styleDefinition)
		{
			_previewStyleDefinition = styleDefinition.Copy();
			_previewStyle = styleDefinition.Scale(_scale);
		}

		private void ApplyLayerStyle(DmdLayer layer, DmdLayerStyleDefinition layerStyleDef)
		{
			switch (layer) {
				case DmdLayer.OuterGlow:
					_previewStyleDefinition.OuterGlow = layerStyleDef;
					break;
				case DmdLayer.InnerGlow:
					_previewStyleDefinition.InnerGlow = layerStyleDef;
					break;
				case DmdLayer.Foreground:
					_previewStyleDefinition.Foreground = layerStyleDef;
					break;
				case DmdLayer.Background:
					_previewStyleDefinition.Background = layerStyleDef;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
			}
			_previewStyle = _previewStyleDefinition.Scale(_scale);
		}

		private void ReDraw()
		{
			Preview.InvalidateVisual();
		}

		private void SetupTriggers()
		{
			StyleNameComboBox.SelectionChanged += (sender, e) => {
				if (e.AddedItems.Count > 0) StyleSelectionChanged(e.AddedItems[0].ToString());
			};
			StyleNameComboBox.KeyUp += (sender, e) => StyleSelectionChanged(StyleNameComboBox.Text);

			BackgroundColor.SelectedColorChanged += (sender, e) => {
				_previewStyleDefinition.BackgroundColor = new SKColor(BackgroundColor.SelectedColor.Value.R,
					BackgroundColor.SelectedColor.Value.G, BackgroundColor.SelectedColor.Value.B,
					BackgroundColor.SelectedColor.Value.A);
				ReDraw();
			};
		}

		private void UpdateControls()
		{
			BackgroundColor.SelectedColor = _previewStyleDefinition.BackgroundColor.ToColor();

			ForegroundStyle.DmdStyleDefinition = _previewStyleDefinition.Foreground;
			InnerGlowStyle.DmdStyleDefinition = _previewStyleDefinition.InnerGlow;
			OuterGlowStyle.DmdStyleDefinition = _previewStyleDefinition.OuterGlow;
			BackgroundStyle.DmdStyleDefinition = _previewStyleDefinition.Background;
		}

		private void OnPaintCanvas(object sender, SKPaintSurfaceEventArgs e)
		{
			// get the screen density for scaling
			//var scale = (float)PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
			var canvasSize = new SKSize(e.Info.Width, e.Info.Height);

			if (_previewSize != canvasSize || _previewSurface == null) {
				//PreRender(canvasSize);

				Logger.Info("Setting up OpenGL context at {0}x{1}...", e.Info.Width, e.Info.Height);
				_previewSurface?.Dispose();
				_previewSurface = GLUtil.GetInstance().CreateSurface(e.Info.Width, e.Info.Height);

				_previewSize = canvasSize;
			}
			PaintPreview(_previewSurface.Canvas, e.Info.Width, e.Info.Height);

			e.Surface.Canvas.DrawSurface(_previewSurface, new SKPoint(0f, 0f));
		}

		private void PaintPreview(SKCanvas canvas, int width, int height)
		{
			canvas.Clear(SKColors.Black);
			var dotSize = new SKSize((float)width / _previewDmdWidth, (float)height / _previewDmdHeight);
			for (var y = 0; y < _previewDmdHeight; y++) {
				for (var x = 0; x < _previewDmdWidth * 3; x += 3) {
					var framePos = y * _previewDmdWidth * 3 + x;
					var color = new SKColor(_previewDmdData[framePos], _previewDmdData[framePos + 1], _previewDmdData[framePos + 2]);
					using (var dotPaint = new SKPaint()) {
						dotPaint.IsAntialias = true;
						dotPaint.Color = color;
						var dotPos = new SKPoint(x / 3f * dotSize.Width, y * dotSize.Height);
						var dotRadius = Math.Min(dotSize.Width, dotSize.Height) / 2;
						canvas.DrawCircle(dotPos.X + dotSize.Width / 2, dotPos.Y + dotSize.Height / 2, dotRadius, dotPaint);
					}
				}
			}
		}

		private void StyleSelectionChanged(string name)
		{
			Logger.Info("Selection changed to {0}", name);
			if (StyleNames.Contains(name)) {
				LoadStyleButton.IsEnabled = true;
				DeleteStyleButton.IsEnabled = true;
			} else {
				LoadStyleButton.IsEnabled = false;
				DeleteStyleButton.IsEnabled = false;
			}
		}

		private void SaveToIniClicked(object sender, RoutedEventArgs e)
		{
			Logger.Info("Saving style {0} to DmdDevice.ini...", StyleNameComboBox.Text);
			var styleName = NewStyleName ?? StyleNameComboBox.Text;
			_dmdConfig.SetStyle(styleName, _previewStyleDefinition);
			StyleSelectionChanged(styleName);
			StyleNameComboBox.ItemsSource = StyleNames;
			StyleNameComboBox.SelectedItem = styleName;
		}

		private void LoadFromIniClicked(object sender, RoutedEventArgs e)
		{
			_previewStyleDefinition = _dmdConfig.GetStyle(NewStyleName);
			UpdateControls();
		}

		private void DeleteFromIniClicked(object sender, RoutedEventArgs e)
		{
			_dmdConfig.RemoveStyle(NewStyleName);
			StyleNameComboBox.ItemsSource = StyleNames;
			StyleNameComboBox.Text = "";
			StyleSelectionChanged("");
		}

		private void ApplyClicked(object sender, RoutedEventArgs e)
		{
			OnStyleApplied.OnNext(_previewStyleDefinition.Copy());
			if (NewStyleName != null) {
				_dmdConfig.ApplyStyle(NewStyleName);
			}
		}

		private void ResetClicked(object sender, RoutedEventArgs e)
		{
			ApplyStyle(_originalStyle);
			UpdateControls();
		}

		private void CancelClicked(object sender, RoutedEventArgs e)
		{
			Hide();
		}

		private static string DoubleToString(double d)
		{
			return ((int)Math.Round(d)).ToString();
		}

		private static double StringToDouble(string str, double fallback)
		{
			try {
				return double.Parse(str);
			} catch (Exception) {
				return fallback;
			}
		}
		
	}
}
