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
using ColorPickerWPF;
using ColorPickerWPF.Code;
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

			_color = Color.FromArgb(_rasterizeStyle.Color.Alpha, _rasterizeStyle.Color.Red, _rasterizeStyle.Color.Green, _rasterizeStyle.Color.Blue);
			ColorButton.Background = new SolidColorBrush(_color);
			ColorValue.Text = _color.ToHexString();
		}

		private void ColorButton_Click(object sender, RoutedEventArgs e)
		{
			ColorPickerWindow.ShowDialog(out _color, ColorPickerDialogOptions.SimpleView);
		}
	}
}
