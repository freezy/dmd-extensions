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

		private readonly IVirtualDmdConfig _config;

		private readonly BitmapImage _preview;

		private double _brightness = 1.0;
		private double _dotSize = 1.0;
		private double _dotRounding = 0.0;
		private Color _unlitDot = Color.FromArgb(0, 0, 0, 0);
		private double _dotGlow = 0.0;
		private double _backGlow = 0.0;
		private Color _glassColor = Color.FromArgb(0, 0, 0, 0);
		public ISubject<IVirtualDmdConfig> OnConfigUpdated { get; } = new Subject<IVirtualDmdConfig>();

		public DmdSettings(IVirtualDmdConfig config)
		{
			_config = config;
			_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-32x8.png"));

			DataContext = this;
			InitializeComponent();

			ResetClicked(null, null);

			IgnoreAspectRatio.Unchecked += (sender, e) => UpdatePreview();
			IgnoreAspectRatio.Checked += (sender, e) => UpdatePreview();

			DotSizeSlider.ValueChanged += (sender, e) => DotSizeValue.Text = DoubleToString2(DotSizeSlider.Value);
			DotSizeValue.TextChanged += (sender, e) => DotSizeSlider.Value = StringToDouble(DotSizeValue.Text, DotSizeSlider.Value);
			DotSizeSlider.ValueChanged += (sender, e) => _dotSize = DotSizeSlider.Value;
			DotSizeSlider.ValueChanged += (sender, e) => UpdatePreview();

			DotRoundingSlider.ValueChanged += (sender, e) => DotRoundingValue.Text = DoubleToString2(DotRoundingSlider.Value);
			DotRoundingValue.TextChanged += (sender, e) => DotRoundingSlider.Value = StringToDouble(DotRoundingValue.Text, DotRoundingSlider.Value);
			DotRoundingSlider.ValueChanged += (sender, e) => _dotRounding = DotRoundingSlider.Value;
			DotRoundingSlider.ValueChanged += (sender, e) => UpdatePreview();

			UnlitDotColor.SelectedColorChanged += (sender, e) => _unlitDot = UnlitDotColor.SelectedColor.Value;
			UnlitDotColor.SelectedColorChanged += (sender, e) => UpdatePreview();

			DotGlowSlider.ValueChanged += (sender, e) => DotGlowValue.Text = DoubleToString2(DotGlowSlider.Value);
			DotGlowValue.TextChanged += (sender, e) => DotGlowSlider.Value = StringToDouble(DotGlowValue.Text, DotGlowSlider.Value);
			DotGlowSlider.ValueChanged += (sender, e) => _dotGlow = DotGlowSlider.Value;
			DotGlowSlider.ValueChanged += (sender, e) => UpdatePreview();

			BrightnessSlider.ValueChanged += (sender, e) => BrightnessValue.Text = DoubleToString2(BrightnessSlider.Value);
			BrightnessValue.TextChanged += (sender, e) => BrightnessSlider.Value = StringToDouble(BrightnessValue.Text, BrightnessSlider.Value);
			BrightnessSlider.ValueChanged += (sender, e) => _brightness = BrightnessSlider.Value;
			BrightnessSlider.ValueChanged += (sender, e) => UpdatePreview();

			BackLevelSlider.ValueChanged += (sender, e) => BackLevelValue.Text = DoubleToString2(BackLevelSlider.Value);
			BackLevelValue.TextChanged += (sender, e) => BackLevelSlider.Value = StringToDouble(BackLevelValue.Text, BackLevelSlider.Value);
			BackLevelSlider.ValueChanged += (sender, e) => _backGlow = BackLevelSlider.Value;
			BackLevelSlider.ValueChanged += (sender, e) => UpdatePreview();

			GlassPath.TextChanged += (sender, e) => UpdatePreview();

			GlassDMDLightingSlider.ValueChanged += (sender, e) => GlassDMDLightingValue.Text = DoubleToString2(GlassDMDLightingSlider.Value);
			GlassDMDLightingValue.TextChanged += (sender, e) => GlassDMDLightingSlider.Value = StringToDouble(GlassDMDLightingValue.Text, GlassDMDLightingSlider.Value);
			GlassDMDLightingSlider.ValueChanged += (sender, e) => _glassColor = Color.FromArgb((byte)(255 * GlassDMDLightingSlider.Value), GlassColor.SelectedColor.Value.R, GlassColor.SelectedColor.Value.G, GlassColor.SelectedColor.Value.B);
			GlassDMDLightingSlider.ValueChanged += (sender, e) => UpdatePreview();

			GlassColor.SelectedColorChanged += (sender, e) => _glassColor = Color.FromArgb((byte)(255 * GlassDMDLightingSlider.Value), GlassColor.SelectedColor.Value.R, GlassColor.SelectedColor.Value.G, GlassColor.SelectedColor.Value.B);
			GlassColor.SelectedColorChanged += (sender, e) => UpdatePreview();

			GlassPadding.OnPaddingChanged.Subscribe(padding => UpdatePreview());

			FramePath.TextChanged += (sender, e) => UpdatePreview();
			FramePadding.OnPaddingChanged.Subscribe(padding => UpdatePreview());

			if (_config is VirtualDmdConfig)
			{
				SaveForAllButton.Visibility = Visibility.Visible;
				SaveForGameButton.Visibility = Visibility.Visible;
			}
			else
			{
				SaveForAllButton.Visibility = Visibility.Hidden;
				SaveForGameButton.Visibility = Visibility.Hidden;
			}

		}

		private void UpdatePreview()
		{
			DMD.SetDimensions(_preview.PixelWidth, _preview.PixelHeight);
			DMD.IgnoreAspectRatio = IgnoreAspectRatio.IsChecked == true;
			DMD.Glass = GlassPath.Text;
			DMD.GlassPad = GlassPadding.Pad;
			DMD.Frame = FramePath.Text;
			DMD.FramePad = FramePadding.Pad;
			DMD.BackGlow = _backGlow;
			DMD.Brightness = _brightness;
			DMD.DotSize = _dotSize;
			DMD.DotRounding = _dotRounding;
			DMD.DotGlow = _dotGlow;
			DMD.UnlitDot = _unlitDot;
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
			if (_config is VirtualDmdConfig)
			{
				((VirtualDmdConfig)_config).SetOptions(_brightness, _dotSize, _dotRounding, _unlitDot, _dotGlow, _backGlow, GlassPath.Text, GlassPadding.Pad, _glassColor, FramePath.Text, FramePadding.Pad, true);
				OnConfigUpdated.OnNext(_config);
			}
		}

		private void SaveGloballyClicked(object sender, RoutedEventArgs e)
		{
			if (_config is VirtualDmdConfig)
			{
				((VirtualDmdConfig)_config).SetOptions(_brightness, _dotSize, _dotRounding, _unlitDot, _dotGlow, _backGlow, GlassPath.Text, GlassPadding.Pad, _glassColor, FramePath.Text, FramePadding.Pad, false);
				OnConfigUpdated.OnNext(_config);
			}
		}

		private class VirtualDmdLiveConfig : IVirtualDmdConfig
		{
			public bool Enabled { get; set; }
			public bool StayOnTop { get; set; }
			public bool IgnoreAr { get; set; }
			public bool UseRegistryPosition { get; set; }
			public double Left { get; set; }
			public double Top { get; set; }
			public double Width { get; set; }
			public double Height { get; set; }
			public double DotSize { get; set; }
			public double DotRounding { get; set; }
			public Color UnlitDot { get; set; }
			public bool HasGameOverride(string key) => false;
			public double Brightness { get; set; }
			public double DotGlow { get; set; }
			public double BackGlow { get; set; }
			public string GlassTexture { get; set; }
			public Thickness GlassPadding { get; set; }
			public Color GlassColor { get; set; }
			public string FrameTexture { get; set; }
			public Thickness FramePadding { get; set; }

		}

		private void ApplyClicked(object sender, RoutedEventArgs e)
		{
			var config = new VirtualDmdLiveConfig()
			{
				DotSize = _dotSize,
				DotRounding = _dotRounding,
				UnlitDot = _unlitDot,
				IgnoreAr = _config.IgnoreAr,
				Brightness = _brightness,
				DotGlow = _dotGlow,
				BackGlow = _backGlow,
				GlassTexture = GlassPath.Text,
				GlassPadding = GlassPadding.Pad,
				GlassColor = _glassColor,
				FrameTexture = FramePath.Text,
				FramePadding = FramePadding.Pad,
				StayOnTop = _config.StayOnTop
			};
			OnConfigUpdated.OnNext(config);
		}

		private void ResetClicked(object sender, RoutedEventArgs e)
		{
			_dotSize = _config.DotSize;
			_dotRounding = _config.DotRounding;
			_unlitDot = _config.UnlitDot;
			_brightness = _config.Brightness;
			_dotGlow = _config.DotGlow;
			_backGlow = _config.BackGlow;
			_glassColor = _config.GlassColor;
			IgnoreAspectRatio.IsChecked = _config.IgnoreAr;

			BrightnessValue.Text = DoubleToString2(_brightness);
			BrightnessSlider.Value = _brightness;

			DotSizeValue.Text = DoubleToString2(_dotSize);
			DotSizeSlider.Value = _dotSize;

			DotRoundingValue.Text = DoubleToString2(_dotRounding);
			DotRoundingSlider.Value = _dotRounding;

			UnlitDotColor.SelectedColor = _unlitDot;

			DotGlowValue.Text = DoubleToString2(_dotGlow);
			DotGlowSlider.Value = _dotGlow;

			BackLevelValue.Text = DoubleToString2(_backGlow);
			BackLevelSlider.Value = _backGlow;

			GlassDMDLightingValue.Text = DoubleToString2(_glassColor.A / 255.0);
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
