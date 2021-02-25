using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.DmdDevice;
using NLog;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// Interaction logic for DmdSettings.xaml
	/// </summary>
	public partial class DmdSettings
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly Configuration _config;
		private readonly VirtualDmdConfig _dmdConfig;
		private readonly DmdStyle _originalStyle;
		private BitmapImage _preview;
		private DmdStyle _previewStyle;
		public List<string> StyleNames => _config == null ? null : _dmdConfig.GetStyleNames();
		public string NewStyleName { get; set; }

		public ISubject<DmdStyle> OnConfigUpdated { get; } = new Subject<DmdStyle>();

		public DmdSettings(DmdStyle style, Configuration config)
		{
			_config = config;
			_originalStyle = style.Copy();
			_previewStyle = _originalStyle.Copy();
			_dmdConfig = _config?.VirtualDmd as VirtualDmdConfig;

			DataContext = this;
			InitializeComponent();

			UpdateControls();
			LoadPreview();

			if (config == null)
			{
				SaveGroup.Visibility = Visibility.Collapsed;
			}

			DotSizeSlider.ValueChanged += (sender, e) => DotSizeValue.Text = DoubleToString2(DotSizeSlider.Value);
			DotSizeValue.TextChanged += (sender, e) => DotSizeSlider.Value = StringToDouble(DotSizeValue.Text, DotSizeSlider.Value);
			DotSizeSlider.ValueChanged += (sender, e) => _previewStyle.DotSize = DotSizeSlider.Value;
			DotSizeSlider.ValueChanged += (sender, e) => UpdatePreview();

			DotRoundingSlider.ValueChanged += (sender, e) => DotRoundingValue.Text = DoubleToString2(DotRoundingSlider.Value);
			DotRoundingValue.TextChanged += (sender, e) => DotRoundingSlider.Value = StringToDouble(DotRoundingValue.Text, DotRoundingSlider.Value);
			DotRoundingSlider.ValueChanged += (sender, e) => _previewStyle.DotRounding = DotRoundingSlider.Value;
			DotRoundingSlider.ValueChanged += (sender, e) => UpdatePreview();

			UnlitDotColor.SelectedColorChanged += (sender, e) => _previewStyle.UnlitDot = UnlitDotColor.SelectedColor.Value;
			UnlitDotColor.SelectedColorChanged += (sender, e) => UpdatePreview();

			DotGlowSlider.ValueChanged += (sender, e) => DotGlowValue.Text = DoubleToString2(DotGlowSlider.Value);
			DotGlowValue.TextChanged += (sender, e) => DotGlowSlider.Value = StringToDouble(DotGlowValue.Text, DotGlowSlider.Value);
			DotGlowSlider.ValueChanged += (sender, e) => _previewStyle.DotGlow = DotGlowSlider.Value;
			DotGlowSlider.ValueChanged += (sender, e) => UpdatePreview();

			BrightnessSlider.ValueChanged += (sender, e) => BrightnessValue.Text = DoubleToString2(BrightnessSlider.Value);
			BrightnessValue.TextChanged += (sender, e) => BrightnessSlider.Value = StringToDouble(BrightnessValue.Text, BrightnessSlider.Value);
			BrightnessSlider.ValueChanged += (sender, e) => _previewStyle.Brightness = BrightnessSlider.Value;
			BrightnessSlider.ValueChanged += (sender, e) => UpdatePreview();

			BackLevelSlider.ValueChanged += (sender, e) => BackLevelValue.Text = DoubleToString2(BackLevelSlider.Value);
			BackLevelValue.TextChanged += (sender, e) => BackLevelSlider.Value = StringToDouble(BackLevelValue.Text, BackLevelSlider.Value);
			BackLevelSlider.ValueChanged += (sender, e) => _previewStyle.BackGlow = BackLevelSlider.Value;
			BackLevelSlider.ValueChanged += (sender, e) => UpdatePreview();

			GammaSlider.ValueChanged += (sender, e) => GammaValue.Text = DoubleToString2(GammaSlider.Value);
			GammaValue.TextChanged += (sender, e) => GammaSlider.Value = StringToDouble(GammaValue.Text, GammaSlider.Value);
			GammaSlider.ValueChanged += (sender, e) => _previewStyle.Gamma = GammaSlider.Value;
			GammaSlider.ValueChanged += (sender, e) => UpdatePreview();

			GlassPath.TextChanged += (sender, e) => _previewStyle.GlassTexture = GlassPath.Text;
			GlassPath.TextChanged += (sender, e) => UpdatePreview();

			GlassDMDLightingSlider.ValueChanged += (sender, e) => GlassDMDLightingValue.Text = DoubleToString2(GlassDMDLightingSlider.Value);
			GlassDMDLightingValue.TextChanged += (sender, e) => GlassDMDLightingSlider.Value = StringToDouble(GlassDMDLightingValue.Text, GlassDMDLightingSlider.Value);
			GlassDMDLightingSlider.ValueChanged += (sender, e) => _previewStyle.GlassLighting = GlassDMDLightingSlider.Value;
			GlassDMDLightingSlider.ValueChanged += (sender, e) => UpdatePreview();

			GlassColor.SelectedColorChanged += (sender, e) => _previewStyle.GlassColor = Color.FromArgb((byte)(255 * GlassDMDLightingSlider.Value), GlassColor.SelectedColor.Value.R, GlassColor.SelectedColor.Value.G, GlassColor.SelectedColor.Value.B);
			GlassColor.SelectedColorChanged += (sender, e) => UpdatePreview();

			GlassPadding.OnPaddingChanged.Subscribe(padding => _previewStyle.GlassPadding = padding);
			GlassPadding.OnPaddingChanged.Subscribe(padding => UpdatePreview());

			FramePath.TextChanged += (sender, e) => _previewStyle.FrameTexture = FramePath.Text;
			FramePath.TextChanged += (sender, e) => UpdatePreview();
			FramePadding.OnPaddingChanged.Subscribe(padding => _previewStyle.FramePadding = padding);
			FramePadding.OnPaddingChanged.Subscribe(padding => UpdatePreview());

			PreviewMono32x8.Checked += (sender, e) => LoadPreview();
			PreviewMono128x32.Checked += (sender, e) => LoadPreview();
			PreviewColor128x32.Checked += (sender, e) => LoadPreview();
			PreviewMono128x16.Checked += (sender, e) => LoadPreview();
			PreviewMono192x64.Checked += (sender, e) => LoadPreview();
		}

		private void UpdateControls()
		{
			BrightnessValue.Text = DoubleToString2(_previewStyle.Brightness);
			BrightnessSlider.Value = _previewStyle.Brightness;

			DotSizeValue.Text = DoubleToString2(_previewStyle.DotSize);
			DotSizeSlider.Value = _previewStyle.DotSize;

			DotRoundingValue.Text = DoubleToString2(_previewStyle.DotRounding);
			DotRoundingSlider.Value = _previewStyle.DotRounding;

			GammaValue.Text = DoubleToString2(_previewStyle.Gamma);
			GammaSlider.Value = _previewStyle.Gamma;

			Color unlitColor = _previewStyle.UnlitDot;
			unlitColor.A = 255;
			UnlitDotColor.SelectedColor = unlitColor;

			DotGlowValue.Text = DoubleToString2(_previewStyle.DotGlow);
			DotGlowSlider.Value = _previewStyle.DotGlow;

			BackLevelValue.Text = DoubleToString2(_previewStyle.BackGlow);
			BackLevelSlider.Value = _previewStyle.BackGlow;

			GlassDMDLightingValue.Text = DoubleToString2(_previewStyle.GlassLighting);
			GlassDMDLightingSlider.Value = _previewStyle.GlassLighting;

			Color glassColor = _previewStyle.GlassColor;
			glassColor.A = 255;
			GlassColor.SelectedColor = glassColor;

			GlassPath.Text = _previewStyle.GlassTexture;
			GlassPadding.Pad = _previewStyle.GlassPadding;

			FramePath.Text = _previewStyle.FrameTexture;
			FramePadding.Pad = _previewStyle.FramePadding;
		}

		private void LoadPreview()
		{
			Logger.Info("Loading preview...");
			if (PreviewMono32x8.IsChecked == true)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-32x8-mono.png"));
			else if (PreviewMono128x32.IsChecked == true)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x32-mono.png"));
			else if (PreviewColor128x32.IsChecked == true)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x32-color.png"));
			else if (PreviewMono128x16.IsChecked == true)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x16-mono.png"));
			else if (PreviewMono192x64.IsChecked == true)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-192x64-mono.png"));
			UpdatePreview();
		}

		private void UpdatePreview()
		{
			DMD.SetDimensions(_preview.PixelWidth, _preview.PixelHeight);
			DMD.SetStyle(_previewStyle);
			DMD.RenderBitmap(_preview);
			var baseWidth = 128.0 * 6.0; // Need to be a multiple of 128.0 and 192.0 to avoid aliasing of the previews
			var baseHeight = 64.0 * 3.0; // Need to be a multiple of 64.0 to avoid aliasing of the previews
			if (DMD.AspectRatio > 4.0)
			{
				DMD.Width = baseWidth;
				DMD.Height = DMD.Width / DMD.AspectRatio;
				var margin = (baseHeight - DMD.Height) *0.5;
				DMD.Margin = new Thickness(0.0, margin, 0.0, margin + 16.0);
			}
			else
			{
				DMD.Height = baseHeight;
				DMD.Width = DMD.Height * DMD.AspectRatio;
				var margin = (baseWidth - DMD.Width) * 0.5;
				DMD.Margin = new Thickness(margin, 0.0, margin, 16.0);
			}
		}

		private void SelectGlassClicked(object sender, RoutedEventArgs e)
		{
			var fileDialog = new System.Windows.Forms.OpenFileDialog();
			var result = fileDialog.ShowDialog();
			switch (result)
			{
				case System.Windows.Forms.DialogResult.OK:
					var file = fileDialog.FileName;
					GlassPath.Text = file;
					GlassPath.ToolTip = file;
					break;
				case System.Windows.Forms.DialogResult.Cancel:
				default:
					break;
			}
		}

		private void SelectFrameClicked(object sender, RoutedEventArgs e)
		{
			var fileDialog = new System.Windows.Forms.OpenFileDialog();
			var result = fileDialog.ShowDialog();
			switch (result)
			{
				case System.Windows.Forms.DialogResult.OK:
					var file = fileDialog.FileName;
					FramePath.Text = file;
					FramePath.ToolTip = file;
					break;
				case System.Windows.Forms.DialogResult.Cancel:
				default:
					break;
			}
		}

		private void ApplyClicked(object sender, RoutedEventArgs e)
		{
			OnConfigUpdated.OnNext(_previewStyle.Copy());
			if (NewStyleName != null)
			{
				_dmdConfig.ApplyStyle(NewStyleName);
			}
		}

		private void ResetClicked(object sender, RoutedEventArgs e)
		{
			_previewStyle = _originalStyle.Copy();
			UpdateControls();
			UpdatePreview();
		}

		private void CancelClicked(object sender, RoutedEventArgs e)
		{
			Hide();
		}

		private void StyleSelectionChanged(string name)
		{
			Logger.Info("Selection changed to {0}", name);
			if (StyleNames.Contains(name))
			{
				LoadStyleButton.IsEnabled = true;
				DeleteStyleButton.IsEnabled = true;
			}
			else
			{
				LoadStyleButton.IsEnabled = false;
				DeleteStyleButton.IsEnabled = false;
			}
		}

		private void SaveToIniClicked(object sender, RoutedEventArgs e)
		{
			Logger.Info("Saving style {0} to DmdDevice.ini...", StyleNameComboBox.Text);
			var styleName = NewStyleName ?? StyleNameComboBox.Text;
			_dmdConfig.SetStyle(styleName, _previewStyle);
			StyleSelectionChanged(styleName);
			StyleNameComboBox.ItemsSource = StyleNames;
			StyleNameComboBox.SelectedItem = styleName;
		}

		private void LoadFromIniClicked(object sender, RoutedEventArgs e)
		{
			_previewStyle = _dmdConfig.GetStyle(NewStyleName);
			UpdateControls();
			UpdatePreview();
		}

		private void DeleteFromIniClicked(object sender, RoutedEventArgs e)
		{
			_dmdConfig.RemoveStyle(NewStyleName);
			StyleNameComboBox.ItemsSource = StyleNames;
			StyleNameComboBox.Text = "";
			StyleSelectionChanged("");
		}

		private static string DoubleToString2(double d)
		{
			return (Math.Round(d * 100) / 100).ToString("G", CultureInfo.InvariantCulture);
		}

		private static double StringToDouble(string str, double fallback)
		{
			try
			{
				return double.Parse(str);
			}
			catch (Exception)
			{
				return fallback;
			}
		}

	}
}
