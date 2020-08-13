using System;
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

		private readonly VirtualDmdConfig _config;

		private readonly BitmapImage _preview;

		private double _brightness = 1.0;
		private double _dotSize = 1.0;
		private double _dotGlow = 0.0;
		private double _backGlow = 0.0;
		private Color _glassColor = Color.FromArgb(0, 0, 0, 0);
		public ISubject<VirtualDmdConfig> OnConfigUpdated { get; } = new Subject<VirtualDmdConfig>();

		public DmdSettings(VirtualDmdConfig config)
		{
			_config = config;
			_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x32.png"));

			DataContext = this;
			InitializeComponent();

			ResetClicked(null, null);

			IgnoreAspectRatio.Unchecked += (sender, e) => UpdatePreview();
			IgnoreAspectRatio.Checked += (sender, e) => UpdatePreview();

			DotSizeSlider.ValueChanged += (sender, e) => DotSizeValue.Text = DoubleToString(DotSizeSlider.Value);
			DotSizeValue.TextChanged += (sender, e) => DotSizeSlider.Value = StringToDouble(DotSizeValue.Text, DotSizeSlider.Value);
			DotSizeSlider.ValueChanged += (sender, e) => _dotSize = DotSizeSlider.Value;
			DotSizeSlider.ValueChanged += (sender, e) => UpdatePreview();

			DotGlowSlider.ValueChanged += (sender, e) => DotGlowValue.Text = DoubleToString(DotGlowSlider.Value);
			DotGlowValue.TextChanged += (sender, e) => DotGlowSlider.Value = StringToDouble(DotGlowValue.Text, DotGlowSlider.Value);
			DotGlowSlider.ValueChanged += (sender, e) => _dotGlow = DotGlowSlider.Value;
			DotGlowSlider.ValueChanged += (sender, e) => UpdatePreview();

			BrightnessSlider.ValueChanged += (sender, e) => BrightnessValue.Text = DoubleToString(BrightnessSlider.Value);
			BrightnessValue.TextChanged += (sender, e) => BrightnessSlider.Value = StringToDouble(BrightnessValue.Text, BrightnessSlider.Value);
			BrightnessSlider.ValueChanged += (sender, e) => _brightness = BrightnessSlider.Value;
			BrightnessSlider.ValueChanged += (sender, e) => UpdatePreview();

			BackLevelSlider.ValueChanged += (sender, e) => BackLevelValue.Text = DoubleToString(BackLevelSlider.Value);
			BackLevelValue.TextChanged += (sender, e) => BackLevelSlider.Value = StringToDouble(BackLevelValue.Text, BackLevelSlider.Value);
			BackLevelSlider.ValueChanged += (sender, e) => _backGlow = BackLevelSlider.Value;
			BackLevelSlider.ValueChanged += (sender, e) => UpdatePreview();

			GlassPath.TextChanged += (sender, e) => UpdatePreview();

			GlassDMDLightingSlider.ValueChanged += (sender, e) => GlassDMDLightingValue.Text = DoubleToString(GlassDMDLightingSlider.Value);
			GlassDMDLightingValue.TextChanged += (sender, e) => GlassDMDLightingSlider.Value = StringToDouble(GlassDMDLightingValue.Text, GlassDMDLightingSlider.Value);
			GlassDMDLightingSlider.ValueChanged += (sender, e) => _glassColor = Color.FromArgb((byte)(255 * GlassDMDLightingSlider.Value), GlassColor.SelectedColor.Value.R, GlassColor.SelectedColor.Value.G, GlassColor.SelectedColor.Value.B);
			GlassDMDLightingSlider.ValueChanged += (sender, e) => UpdatePreview();

			GlassColor.SelectedColorChanged += (sender, e) => _glassColor = Color.FromArgb((byte)(255 * GlassDMDLightingSlider.Value), GlassColor.SelectedColor.Value.R, GlassColor.SelectedColor.Value.G, GlassColor.SelectedColor.Value.B);
			GlassColor.SelectedColorChanged += (sender, e) => UpdatePreview();

			GlassPadding.OnPaddingChanged.Subscribe(padding => UpdatePreview());

			FramePath.TextChanged += (sender, e) => UpdatePreview();
			FramePadding.OnPaddingChanged.Subscribe(padding => UpdatePreview());
		}

		private void UpdatePreview()
		{
			DMD.IgnoreAspectRatio = IgnoreAspectRatio.IsChecked == true;
			DMD.Glass = GlassPath.Text;
			DMD.GlassPad = GlassPadding.Pad;
			DMD.Frame = FramePath.Text;
			DMD.FramePad = FramePadding.Pad;
			DMD.BackGlow = _backGlow;
			DMD.Brightness = _brightness;
			DMD.DotSize = _dotSize;
			DMD.DotGlow = _dotGlow;
			DMD.GlassColor = _glassColor;
			DMD.RenderBitmap(_preview);
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

		private void SaveForGameClicked(object sender, RoutedEventArgs e)
		{
			_config.SetOptions(_brightness, _dotSize, _dotGlow, _backGlow, GlassPath.Text, GlassPadding.Pad, _glassColor, FramePath.Text, FramePadding.Pad, true);
			OnConfigUpdated.OnNext(_config);
		}

		private void SaveGloballyClicked(object sender, RoutedEventArgs e)
		{
			_config.SetOptions(_brightness, _dotSize, _dotGlow, _backGlow, GlassPath.Text, GlassPadding.Pad, _glassColor, FramePath.Text, FramePadding.Pad, false);
			OnConfigUpdated.OnNext(_config);
		}

		private void ResetClicked(object sender, RoutedEventArgs e)
		{
			_dotSize = _config.DotSize;
			_brightness = _config.Brightness;
			_dotGlow = _config.DotGlow;
			_backGlow = _config.BackGlow;
			_glassColor = _config.GlassColor;
			IgnoreAspectRatio.IsChecked = _config.IgnoreAr;

			BrightnessValue.Text = DoubleToString(_brightness);
			BrightnessSlider.Value = _brightness;

			DotSizeValue.Text = DoubleToString(_dotSize);
			DotSizeSlider.Value = _dotSize;

			DotGlowValue.Text = DoubleToString(_dotGlow);
			DotGlowSlider.Value = _dotGlow;

			BackLevelValue.Text = DoubleToString(_backGlow);
			BackLevelSlider.Value = _backGlow;

			GlassDMDLightingValue.Text = DoubleToString(_glassColor.A / 255.0);
			GlassDMDLightingSlider.Value = _glassColor.A / 255.0;

			GlassColor.SelectedColor = _glassColor;

			GlassPath.Text = _config.GlassTexture;
			GlassPadding.Pad = _config.GlassPadding;

			FramePath.Text = _config.FrameTexture;
			FramePadding.Pad = _config.FramePadding;

			UpdatePreview();
		}

		private void CancelClicked(object sender, RoutedEventArgs e)
		{
			Hide();
		}

		private static string DoubleToString(double d)
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
