using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.DmdDevice;
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

		private DmdStyleDefinition _previewStyleDef;
		private SKSize _previewSize;
		private SKSurface _previewSurface;
		private readonly DmdStyleDefinition _originalStyle;
		private readonly Configuration _config;
		private readonly VirtualDmdConfig _dmdConfig;
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
			_dmdConfig = _config?.VirtualDmd as VirtualDmdConfig;

			DataContext = this;
			InitializeComponent();

			_previewSize = new SKSize((float)Preview.Width, (float)Preview.Height);
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
			});
			InnerGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				ApplyLayerStyle(DmdLayer.InnerGlow, layerStyle);
			});
			OuterGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				ApplyLayerStyle(DmdLayer.OuterGlow, layerStyle);
			});
			BackgroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				ApplyLayerStyle(DmdLayer.Background, layerStyle);
			});
		}

		private void ApplyStyle(DmdStyleDefinition styleDefinition)
		{
			_previewStyleDef = styleDefinition.Copy();
		}

		private void ApplyLayerStyle(DmdLayer layer, DmdLayerStyleDefinition layerStyleDef)
		{
			switch (layer) {
				case DmdLayer.OuterGlow:
					_previewStyleDef.OuterGlow = layerStyleDef;
					break;
				case DmdLayer.InnerGlow:
					_previewStyleDef.InnerGlow = layerStyleDef;
					break;
				case DmdLayer.Foreground:
					_previewStyleDef.Foreground = layerStyleDef;
					break;
				case DmdLayer.Background:
					_previewStyleDef.Background = layerStyleDef;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
			}
			ReDraw();
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
				_previewStyleDef.BackgroundColor = new SKColor(BackgroundColor.SelectedColor.Value.R,
					BackgroundColor.SelectedColor.Value.G, BackgroundColor.SelectedColor.Value.B,
					BackgroundColor.SelectedColor.Value.A);
				ReDraw();
			};
		}

		private void UpdateControls()
		{
			BackgroundColor.SelectedColor = _previewStyleDef.BackgroundColor.ToColor();

			ForegroundStyle.DmdStyleDefinition = _previewStyleDef.Foreground;
			InnerGlowStyle.DmdStyleDefinition = _previewStyleDef.InnerGlow;
			OuterGlowStyle.DmdStyleDefinition = _previewStyleDef.OuterGlow;
			BackgroundStyle.DmdStyleDefinition = _previewStyleDef.Background;
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
			var data = new DmdFrame(FrameFormat.Rgb24, _previewDmdData, _previewDmdWidth, _previewDmdHeight);
			DmdPainter.Paint(data, canvas, width, height, _previewStyleDef, false);
		}

		private void StyleSelectionChanged(string name)
		{
			Logger.Info("Selection changed to {0}", name);
			if (StyleNames != null && StyleNames.Contains(name)) {
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
			_dmdConfig.SetStyle(styleName, _previewStyleDef);
			StyleSelectionChanged(styleName);
			StyleNameComboBox.ItemsSource = StyleNames;
			StyleNameComboBox.SelectedItem = styleName;
		}

		private void LoadFromIniClicked(object sender, RoutedEventArgs e)
		{
			_previewStyleDef = _dmdConfig.GetStyle(NewStyleName);
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
			OnStyleApplied.OnNext(_previewStyleDef.Copy());
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
    }
}
