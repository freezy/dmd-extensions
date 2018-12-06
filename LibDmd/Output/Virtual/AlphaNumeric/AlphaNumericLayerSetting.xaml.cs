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
				UpdateValues();
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

		private void UpdateValues()
		{
			DilateXValue.Text = _rasterizeLayerStyleDefinition.Dilate.X.ToString();
			DilateYValue.Text = _rasterizeLayerStyleDefinition.Dilate.Y.ToString();
			DilateXSlider.Value = _rasterizeLayerStyleDefinition.Dilate.X;
			DilateYSlider.Value = _rasterizeLayerStyleDefinition.Dilate.Y;

			BlurXValue.Text = _rasterizeLayerStyleDefinition.Blur.X.ToString();
			BlurYValue.Text = _rasterizeLayerStyleDefinition.Blur.Y.ToString();
			BlurXSlider.Value = _rasterizeLayerStyleDefinition.Blur.X;
			BlurYSlider.Value = _rasterizeLayerStyleDefinition.Blur.Y;

			LayerEnabled.IsChecked = _rasterizeLayerStyleDefinition.IsEnabled;
			DilateEnabled.IsChecked = _rasterizeLayerStyleDefinition.IsDilateEnabled;
			BlurEnabled.IsChecked = _rasterizeLayerStyleDefinition.IsBlurEnabled;

			_color = Color.FromArgb(_rasterizeLayerStyleDefinition.Color.Alpha, _rasterizeLayerStyleDefinition.Color.Red, _rasterizeLayerStyleDefinition.Color.Green, _rasterizeLayerStyleDefinition.Color.Blue);
			ColorButton.SelectedColor = _color;

			if (!_rasterizeLayerStyleDefinition.IsDilateEnabled) {
				ToggleDilate(null, null);
			}
			if (!_rasterizeLayerStyleDefinition.IsBlurEnabled) {
				ToggleBlur(null, null);
			}
			if (!_rasterizeLayerStyleDefinition.IsEnabled) {
				Toggle(null, null);
			}
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

		private void Toggle(object sender, RoutedEventArgs e)
		{
			if (LayerEnabled.IsChecked != null && (bool) LayerEnabled.IsChecked) {
				ColorButton.IsEnabled = true;
				DilateEnabled.IsEnabled = true;
				DilateXSlider.IsEnabled = true;
				DilateXValue.IsEnabled = true;
				DilateYSlider.IsEnabled = true;
				DilateYValue.IsEnabled = true;
				BlurEnabled.IsEnabled = true;
				BlurXSlider.IsEnabled = true;
				BlurXValue.IsEnabled = true;
				BlurYSlider.IsEnabled = true;
				BlurYValue.IsEnabled = true;
			} else {
				ColorButton.IsEnabled = false;
				DilateEnabled.IsEnabled = false;
				DilateXSlider.IsEnabled = false;
				DilateXValue.IsEnabled = false;
				DilateYSlider.IsEnabled = false;
				DilateYValue.IsEnabled = false;
				BlurEnabled.IsEnabled = false;
				BlurXSlider.IsEnabled = false;
				BlurXValue.IsEnabled = false;
				BlurYSlider.IsEnabled = false;
				BlurYValue.IsEnabled = false;
			}
		}

		private void ToggleDilate(object sender, RoutedEventArgs e)
		{
			if (DilateEnabled.IsChecked != null && (bool)DilateEnabled.IsChecked) {
				DilateXSlider.IsEnabled = true;
				DilateXValue.IsEnabled = true;
				DilateYSlider.IsEnabled = true;
				DilateYValue.IsEnabled = true;
			} else {
				DilateXSlider.IsEnabled = false;
				DilateXValue.IsEnabled = false;
				DilateYSlider.IsEnabled = false;
				DilateYValue.IsEnabled = false;
			}
		}

		private void ToggleBlur(object sender, RoutedEventArgs e)
		{
			if (BlurEnabled.IsChecked != null && (bool)BlurEnabled.IsChecked) {
				BlurXSlider.IsEnabled = true;
				BlurXValue.IsEnabled = true;
				BlurYSlider.IsEnabled = true;
				BlurYValue.IsEnabled = true;
			} else {
				BlurXSlider.IsEnabled = false;
				BlurXValue.IsEnabled = false;
				BlurYSlider.IsEnabled = false;
				BlurYValue.IsEnabled = false;
			}
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
