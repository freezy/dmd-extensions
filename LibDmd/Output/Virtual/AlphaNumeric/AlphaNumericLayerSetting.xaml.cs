using System;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	/// <summary>
	/// Interaction logic for AlphaNumericLayerSetting.xaml
	/// </summary>
	public partial class VirtualAlphaNumericLayerSetting : UserControl
	{
		public ISubject<RasterizeLayerStyleDefinition> OnLayerChanged { get; } = new Subject<RasterizeLayerStyleDefinition>();

		public RasterizeLayerStyleDefinition RasterizeStyleDefinition
		{
			get => _rasterizeLayerStyleDefinition;
			set {
				_rasterizeLayerStyleDefinition = value;
				UpdateControls();
			}
		}

		public string Label
		{
			get => LayerEnabled.Content.ToString();
			set => LayerEnabled.Content = value;
		}

		private RasterizeLayerStyleDefinition _rasterizeLayerStyleDefinition;
		private Color _color;

		public VirtualAlphaNumericLayerSetting()
		{
			InitializeComponent();
			LayerEnabled.Checked += (sender, e) => _rasterizeLayerStyleDefinition.IsEnabled = true;
			LayerEnabled.Unchecked += (sender, e) => _rasterizeLayerStyleDefinition.IsEnabled = false;
			DilateXSlider.ValueChanged += (sender, e) => DilateXValue.Text = DoubleToString(DilateXSlider.Value);
			DilateXValue.TextChanged += (sender, e) => DilateXSlider.Value = StringToDouble(DilateXValue.Text, DilateXSlider.Value);
			DilateYSlider.ValueChanged += (sender, e) => DilateYValue.Text = DoubleToString(DilateYSlider.Value);
			DilateYValue.TextChanged += (sender, e) => DilateYSlider.Value = StringToDouble(DilateYValue.Text, DilateYSlider.Value);
			BlurXSlider.ValueChanged += (sender, e) => BlurXValue.Text = DoubleToString(BlurXSlider.Value);
			BlurXValue.TextChanged += (sender, e) => BlurXSlider.Value = StringToDouble(BlurXValue.Text, BlurXSlider.Value);
			BlurYSlider.ValueChanged += (sender, e) => BlurYValue.Text = DoubleToString(BlurYSlider.Value);
			BlurYValue.TextChanged += (sender, e) => BlurYSlider.Value = StringToDouble(BlurYValue.Text, BlurYSlider.Value);
			LayerEnabled.Checked += (sender, e) => Changed();
			LayerEnabled.Unchecked += (sender, e) => Changed();
			ColorButton.SelectedColorChanged += (sender, e) => Changed();
			DilateEnabled.Checked += (sender, e) => Changed();
			DilateEnabled.Unchecked += (sender, e) => Changed();
			BlurEnabled.Checked += (sender, e) => Changed();
			BlurEnabled.Unchecked += (sender, e) => Changed();
			DilateXSlider.ValueChanged += (sender, e) => Changed();
			DilateYSlider.ValueChanged += (sender, e) => Changed();
			BlurXSlider.ValueChanged += (sender, e) => Changed();
			BlurYSlider.ValueChanged += (sender, e) => Changed();
		}

		private void UpdateControls()
		{
			LayerEnabled.IsChecked = _rasterizeLayerStyleDefinition.IsEnabled;

			DilateEnabled.IsChecked = _rasterizeLayerStyleDefinition.IsDilateEnabled;
			DilateXValue.Text = _rasterizeLayerStyleDefinition.Dilate.X.ToString();
			DilateYValue.Text = _rasterizeLayerStyleDefinition.Dilate.Y.ToString();
			DilateXSlider.Value = _rasterizeLayerStyleDefinition.Dilate.X;
			DilateYSlider.Value = _rasterizeLayerStyleDefinition.Dilate.Y;

			BlurEnabled.IsChecked = _rasterizeLayerStyleDefinition.IsBlurEnabled;
			BlurXValue.Text = _rasterizeLayerStyleDefinition.Blur.X.ToString();
			BlurYValue.Text = _rasterizeLayerStyleDefinition.Blur.Y.ToString();
			BlurXSlider.Value = _rasterizeLayerStyleDefinition.Blur.X;
			BlurYSlider.Value = _rasterizeLayerStyleDefinition.Blur.Y;

			_color = Color.FromArgb(_rasterizeLayerStyleDefinition.Color.Alpha, _rasterizeLayerStyleDefinition.Color.Red, _rasterizeLayerStyleDefinition.Color.Green, _rasterizeLayerStyleDefinition.Color.Blue);
			ColorButton.SelectedColor = _color;

			ToggleDilateEnabled(_rasterizeLayerStyleDefinition.IsDilateEnabled);
			ToggleBlurEnabled(_rasterizeLayerStyleDefinition.IsBlurEnabled);
			ToggleLayerEnabled(_rasterizeLayerStyleDefinition.IsEnabled);
		}

		private void Changed()
		{
			if (LayerEnabled.IsChecked == null || BlurEnabled.IsChecked == null || DilateEnabled.IsChecked == null ||
			    ColorButton.SelectedColor == null) {
				return;
			}
			OnLayerChanged.OnNext(new RasterizeLayerStyleDefinition {
				IsEnabled = (bool)LayerEnabled.IsChecked,
				IsBlurEnabled = (bool)BlurEnabled.IsChecked,
				IsDilateEnabled = (bool)DilateEnabled.IsChecked,
				Color = new SKColor(ColorButton.SelectedColor.Value.R, ColorButton.SelectedColor.Value.G, ColorButton.SelectedColor.Value.B, ColorButton.SelectedColor.Value.A),
				Blur = new SKPoint((float)BlurXSlider.Value, (float)BlurYSlider.Value),
				Dilate = new SKPoint((float)DilateXSlider.Value, (float)DilateYSlider.Value)
			});
		}

		private void UpdateLayerEnabled(object sender, RoutedEventArgs e)
		{
			ToggleLayerEnabled(LayerEnabled.IsChecked != null && (bool) LayerEnabled.IsChecked);
		}

		private void ToggleLayerEnabled(bool enabled)
		{
			ColorButton.IsEnabled = enabled;
			DilateEnabled.IsEnabled = enabled;
			DilateXSlider.IsEnabled = enabled;
			DilateXValue.IsEnabled = enabled;
			DilateYSlider.IsEnabled = enabled;
			DilateYValue.IsEnabled = enabled;
			BlurEnabled.IsEnabled = enabled;
			BlurXSlider.IsEnabled = enabled;
			BlurXValue.IsEnabled = enabled;
			BlurYSlider.IsEnabled = enabled;
			BlurYValue.IsEnabled = enabled;
		}

		private void UpdateDilateEnabled(object sender, RoutedEventArgs e)
		{
			ToggleDilateEnabled(DilateEnabled.IsChecked != null && (bool) DilateEnabled.IsChecked);
		}

		private void ToggleDilateEnabled(bool enabled)
		{
			DilateXSlider.IsEnabled = enabled;
			DilateXValue.IsEnabled = enabled;
			DilateYSlider.IsEnabled = enabled;
			DilateYValue.IsEnabled = enabled;
		}

		private void UpdateBlurEnabled(object sender, RoutedEventArgs e)
		{
			ToggleBlurEnabled(BlurEnabled.IsChecked != null && (bool) BlurEnabled.IsChecked);
		}

		private void ToggleBlurEnabled(bool enabled)
		{
			BlurXSlider.IsEnabled = enabled;
			BlurXValue.IsEnabled = enabled;
			BlurYSlider.IsEnabled = enabled;
			BlurYValue.IsEnabled = enabled;
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
