using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
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
using LibDmd.Output.Virtual;
using SkiaSharp;

namespace LibDmd.Common
{
	/// <summary>
	/// Interaction logic for AlphaNumericLayerSetting.xaml
	/// </summary>
	public partial class VirtualAlphaNumericLayerSetting : UserControl
	{
		public ISubject<RasterizeLayerStyle> OnLayerChanged { get; } = new Subject<RasterizeLayerStyle>();

		public RasterizeLayerStyle RasterizeStyle
		{
			get => _rasterizeStyle;
			set {
				_rasterizeStyle = value;
				UpdateValues();
			}
		}

		public string Label
		{
			get => LayerEnabled.Content.ToString();
			set => LayerEnabled.Content = value;
		}

		private RasterizeLayerStyle _rasterizeStyle;
		private Color _color;

		public VirtualAlphaNumericLayerSetting()
		{
			InitializeComponent();
			LayerEnabled.Checked += (sender, e) => RasterizeStyle.IsEnabled = true;
			LayerEnabled.Unchecked += (sender, e) => RasterizeStyle.IsEnabled = false;
			DilateXSlider.ValueChanged += (sender, e) => DilateXValue.Text = DoubleToString(DilateXSlider.Value);
			DilateXValue.TextChanged += (sender, e) => DilateXSlider.Value = StringToDouble(DilateXValue.Text, DilateXSlider.Value);
			DilateYSlider.ValueChanged += (sender, e) => DilateYValue.Text = DoubleToString(DilateYSlider.Value);
			DilateYValue.TextChanged += (sender, e) => DilateYSlider.Value = StringToDouble(DilateYValue.Text, DilateYSlider.Value);
			BlurXSlider.ValueChanged += (sender, e) => BlurXValue.Text = DoubleToString(BlurXSlider.Value);
			BlurXValue.TextChanged += (sender, e) => BlurXSlider.Value = StringToDouble(BlurXValue.Text, BlurXSlider.Value);
			BlurYSlider.ValueChanged += (sender, e) => BlurYValue.Text = DoubleToString(BlurYSlider.Value);
			BlurYValue.TextChanged += (sender, e) => BlurYSlider.Value = StringToDouble(BlurYValue.Text, BlurYSlider.Value);
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
			DilateXValue.Text = _rasterizeStyle.Dilate.X.ToString();
			DilateYValue.Text = _rasterizeStyle.Dilate.Y.ToString();
			DilateXSlider.Value = _rasterizeStyle.Dilate.X;
			DilateYSlider.Value = _rasterizeStyle.Dilate.Y;

			BlurXValue.Text = _rasterizeStyle.Blur.X.ToString();
			BlurYValue.Text = _rasterizeStyle.Blur.Y.ToString();
			BlurXSlider.Value = _rasterizeStyle.Blur.X;
			BlurYSlider.Value = _rasterizeStyle.Blur.Y;

			LayerEnabled.IsChecked = _rasterizeStyle.IsEnabled;
			DilateEnabled.IsChecked = _rasterizeStyle.IsDilateEnabled;
			BlurEnabled.IsChecked = _rasterizeStyle.IsBlurEnabled;

			_color = Color.FromArgb(_rasterizeStyle.Color.Alpha, _rasterizeStyle.Color.Red, _rasterizeStyle.Color.Green, _rasterizeStyle.Color.Blue);
			ColorButton.SelectedColor = _color;

			if (!_rasterizeStyle.IsDilateEnabled) {
				ToggleDilate(null, null);
			}
			if (!_rasterizeStyle.IsBlurEnabled) {
				ToggleBlur(null, null);
			}
			if (!_rasterizeStyle.IsEnabled)
			{
				Toggle(null, null);
			}
		}

		private void Changed()
		{
			if (LayerEnabled.IsChecked == null || BlurEnabled.IsChecked == null || DilateEnabled.IsChecked == null ||
			    ColorButton.SelectedColor == null) {
				return;
			}
			OnLayerChanged.OnNext(new RasterizeLayerStyle {
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
