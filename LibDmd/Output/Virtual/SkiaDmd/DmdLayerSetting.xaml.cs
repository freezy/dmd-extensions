using System;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibDmd.Output.Virtual.AlphaNumeric;
using SkiaSharp;
using SkiaSharp.Views.WPF;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	/// <summary>
	/// Interaction logic for DmdLayerSetting.xaml
	/// </summary>
	public partial class DmdLayerSetting : UserControl
	{
		public ISubject<DmdLayerStyleDefinition> OnLayerChanged { get; } = new Subject<DmdLayerStyleDefinition>();

		public DmdLayerStyleDefinition DmdStyleDefinition
		{
			get => _dmdLayerStyleDefinition;
			set {
				_dmdLayerStyleDefinition = value;
				UpdateControls();
			}
		}

		public string Label
		{
			get => LayerEnabled.Content.ToString();
			set => LayerEnabled.Content = value;
		}

		private DmdLayerStyleDefinition _dmdLayerStyleDefinition;
		private Color _color;

		public DmdLayerSetting()
		{
			InitializeComponent();
			LayerEnabled.Checked += (sender, e) => _dmdLayerStyleDefinition.IsEnabled = true;
			LayerEnabled.Unchecked += (sender, e) => _dmdLayerStyleDefinition.IsEnabled = false;

			DotSizeSlider.ValueChanged += (sender, e) => DotSizeValue.Text = DoubleToString(DotSizeSlider.Value);
			DotSizeValue.TextChanged += (sender, e) => DotSizeSlider.Value = StringToDouble(DotSizeValue.Text, DotSizeSlider.Value);

			OpacitySlider.ValueChanged += (sender, e) => OpacityValue.Text = DoubleToString(OpacitySlider.Value);
			OpacityValue.TextChanged += (sender, e) => OpacitySlider.Value = StringToDouble(OpacityValue.Text, OpacitySlider.Value);

			LuminositySlider.ValueChanged += (sender, e) => LuminosityValue.Text = DoubleToString(LuminositySlider.Value);
			LuminosityValue.TextChanged += (sender, e) => LuminositySlider.Value = StringToDouble(LuminosityValue.Text, LuminositySlider.Value);

			RoundedSlider.ValueChanged += (sender, e) => RoundedValue.Text = DoubleToString(RoundedSlider.Value);
			RoundedValue.TextChanged += (sender, e) => RoundedSlider.Value = StringToDouble(RoundedValue.Text, RoundedSlider.Value);

			BlurSlider.ValueChanged += (sender, e) => BlurValue.Text = DoubleToString(BlurSlider.Value);
			BlurValue.TextChanged += (sender, e) => BlurSlider.Value = StringToDouble(BlurValue.Text, BlurSlider.Value);

			LayerEnabled.Checked += (sender, e) => Changed();
			LayerEnabled.Unchecked += (sender, e) => Changed();
			RoundedEnabled.Checked += (sender, e) => Changed();
			RoundedEnabled.Unchecked += (sender, e) => Changed();
			BlurEnabled.Checked += (sender, e) => Changed();
			BlurEnabled.Unchecked += (sender, e) => Changed();

			DotSizeSlider.ValueChanged += (sender, e) => Changed();
			OpacitySlider.ValueChanged += (sender, e) => Changed();
			LuminositySlider.ValueChanged += (sender, e) => Changed();
			RoundedSlider.ValueChanged += (sender, e) => Changed();
			BlurSlider.ValueChanged += (sender, e) => Changed();
		}

		public void UpdateControls()
		{
			LayerEnabled.IsChecked = _dmdLayerStyleDefinition.IsEnabled;

			DotSizeValue.Text = _dmdLayerStyleDefinition.Size.ToString();
			DotSizeSlider.Value= _dmdLayerStyleDefinition.Size;
			OpacityValue.Text = _dmdLayerStyleDefinition.Opacity.ToString();
			OpacitySlider.Value= _dmdLayerStyleDefinition.Opacity;
			LuminosityValue.Text = _dmdLayerStyleDefinition.Luminosity.ToString();
			LuminositySlider.Value= _dmdLayerStyleDefinition.Luminosity;
			RoundedEnabled.IsChecked = _dmdLayerStyleDefinition.IsRoundedEnabled;
			RoundedValue.Text = _dmdLayerStyleDefinition.Rounded.ToString();
			RoundedSlider.Value= _dmdLayerStyleDefinition.Rounded;
			BlurEnabled.IsChecked = _dmdLayerStyleDefinition.IsBlurEnabled;
			BlurValue.Text = _dmdLayerStyleDefinition.Blur.ToString();
			BlurSlider.Value= _dmdLayerStyleDefinition.Blur;

			ToggleRoundedEnabled(_dmdLayerStyleDefinition.IsRoundedEnabled);
			ToggleBlurEnabled(_dmdLayerStyleDefinition.IsBlurEnabled);
			ToggleLayerEnabled(_dmdLayerStyleDefinition.IsEnabled);
		}

		private void Changed()
		{
			if (LayerEnabled.IsChecked == null || BlurEnabled.IsChecked == null || RoundedEnabled.IsChecked == null) {
				return;
			}
			OnLayerChanged.OnNext(new DmdLayerStyleDefinition {
				IsEnabled = (bool)LayerEnabled.IsChecked,
				Size = DotSizeSlider.Value,
				Opacity = OpacitySlider.Value,
				Luminosity = (float)LuminositySlider.Value,
				IsRoundedEnabled = (bool)RoundedEnabled.IsChecked,
				Rounded = RoundedSlider.Value,
				IsBlurEnabled = (bool)BlurEnabled.IsChecked,
				Blur = BlurSlider.Value
			});
		}

		private void UpdateLayerEnabled(object sender, RoutedEventArgs e)
		{
			ToggleLayerEnabled(LayerEnabled.IsChecked != null && (bool) LayerEnabled.IsChecked);
		}

		private void ToggleLayerEnabled(bool enabled)
		{
			DotSizeSlider.IsEnabled = enabled;
			DotSizeValue.IsEnabled = enabled;
			OpacitySlider.IsEnabled = enabled;
			OpacityValue.IsEnabled = enabled;
			LuminositySlider.IsEnabled = enabled;
			LuminosityValue.IsEnabled = enabled;
			RoundedEnabled.IsEnabled = enabled;
			RoundedSlider.IsEnabled = enabled;
			RoundedValue.IsEnabled = enabled;
			BlurEnabled.IsEnabled = enabled;
			BlurSlider.IsEnabled = enabled;
			BlurValue.IsEnabled = enabled;
		}

		private void UpdateBlurEnabled(object sender, RoutedEventArgs e)
		{
			ToggleBlurEnabled(BlurEnabled.IsChecked != null && (bool) BlurEnabled.IsChecked);
		}

		private void UpdateRoundedEnabled(object sender, RoutedEventArgs e)
		{
			ToggleRoundedEnabled(RoundedEnabled.IsChecked != null && (bool)RoundedEnabled.IsChecked);
		}

		private void ToggleBlurEnabled(bool enabled)
		{
			BlurSlider.IsEnabled = enabled;
			BlurValue.IsEnabled = enabled;
		}

		private void ToggleRoundedEnabled(bool enabled)
		{
			RoundedSlider.IsEnabled = enabled;
			RoundedValue.IsEnabled = enabled;
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
