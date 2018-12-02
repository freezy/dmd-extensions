using System;
using System.Collections.Generic;
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
using LibDmd.Output.Virtual;

namespace LibDmd.Common
{
	/// <summary>
	/// Interaction logic for AlphaNumericLayerSetting.xaml
	/// </summary>
	public partial class VirtualAlphaNumericLayerSetting : UserControl
	{

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

			ToggleBlur(null, null);
			ToggleDilate(null, null);
			Toggle(null, null);
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
	}
}
