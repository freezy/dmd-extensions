using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// Interaction logic for DmdSettings.xaml
	/// </summary>
	public partial class DmdSettings
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly IConfiguration _config;
		private readonly VirtualDmdConfig _dmdConfig;
		private readonly DmdStyle _originalStyle;
		private readonly FrameFormat _lastFrameFormat;

		private BitmapImage _preview;
		private DmdStyle _previewStyle;
		public List<string> StyleNames => _config == null ? null : _dmdConfig.GetStyleNames(); 
		public string NewStyleName { get; set; }

		public ISubject<DmdStyle> OnConfigUpdated { get; } = new Subject<DmdStyle>();

		private string _lastFramePath;
		private string _lastGlassPath;

		public DmdSettings(DmdStyle style, IConfiguration config, FrameFormat lastFrameFormat)
		{
			_config = config;
			_originalStyle = style.Copy();
			_previewStyle = _originalStyle.Copy();
			_dmdConfig = _config?.VirtualDmd as VirtualDmdConfig;
			_lastFrameFormat = lastFrameFormat;

			DataContext = this;
			InitializeComponent();

			if (_dmdConfig != null) {
				StyleNameComboBox.SelectedValue = _dmdConfig.StyleName;
			}

			UpdateControls();
			LoadPreview();

			// if there's no game name, don't use the split button for applying.
			if (_dmdConfig?.HasGameName == false) {
				ApplyButton.Visibility = Visibility.Hidden;
				ApplyGloballyButton.Visibility = Visibility.Visible;

			} else {
				ApplyButton.Visibility = Visibility.Visible;
				ApplyButton.Content = $"Apply to \"{_dmdConfig.GameName}\"";
				ApplyGloballyButton.Visibility = Visibility.Hidden;
			}

			if (config == null)
			{
				SaveGroup.Visibility = Visibility.Collapsed;
			}

			DotSize.OnValueChanged.Subscribe(value => {
				_previewStyle.DotSize = value;
				UpdatePreview();
			});

			DotRounding.OnValueChanged.Subscribe(value => {
				_previewStyle.DotRounding = value;
				UpdatePreview();
			});

			DotSharpness.OnValueChanged.Subscribe(value => {
				_previewStyle.DotSharpness = value;
				UpdatePreview();
			});

			UnlitDotColor.SelectedColorChanged += (sender, e) => _previewStyle.UnlitDot = UnlitDotColor.SelectedColor.Value;
			UnlitDotColor.SelectedColorChanged += (sender, e) => UpdatePreview();

			DotGlow.OnValueChanged.Subscribe(value => {
				_previewStyle.DotGlow = value;
				UpdatePreview();
			});

			Brightness.OnValueChanged.Subscribe(value => {
				_previewStyle.Brightness = value;
				UpdatePreview();
			});

			BackLevel.OnValueChanged.Subscribe(value => {
				_previewStyle.BackGlow = value;
				UpdatePreview();
			});

			Gamma.OnValueChanged.Subscribe(value => {
				_previewStyle.Gamma = value;
				UpdatePreview();
			});

			TintAmount.OnValueChanged.Subscribe(value => {
				_previewStyle.Tint = Color.FromArgb((byte)(value * 255), TintColor.SelectedColor.Value.R, TintColor.SelectedColor.Value.G, TintColor.SelectedColor.Value.B);
				LoadPreview();
			});
			TintColor.SelectedColorChanged += (sender, e) =>
			{
				// if tint color is changed but the amount is 0, assume the user wants to actually apply the tint, so bump it to 1.
				var tintAmount = TintAmount.Value == 0 ? 1 : TintAmount.Value;
				var tintColor = TintColor.SelectedColor ?? Colors.OrangeRed; // just to be sure
				_previewStyle.Tint = Color.FromArgb((byte)(tintAmount * 255), tintColor.R, tintColor.G, tintColor.B);
				UpdatePreview();
			};

			GlassPath.TextChanged += (sender, e) => _previewStyle.GlassTexture = GlassPath.Text;
			GlassPath.TextChanged += (sender, e) => UpdatePreview();

			GlassDMDLightingSlider.ValueChanged += (sender, e) => GlassDMDLightingValue.Text = DoubleToString2(GlassDMDLightingSlider.Value);
			GlassDMDLightingValue.TextChanged += (sender, e) => GlassDMDLightingSlider.Value = StringToDouble(GlassDMDLightingValue.Text, GlassDMDLightingSlider.Value);
			GlassDMDLightingSlider.ValueChanged += (sender, e) => _previewStyle.GlassLighting = GlassDMDLightingSlider.Value;
			GlassDMDLightingSlider.ValueChanged += (sender, e) => UpdatePreview();

			GlassColor.SelectedColorChanged += (sender, e) => _previewStyle.GlassColor = Color.FromArgb(255, GlassColor.SelectedColor.Value.R, GlassColor.SelectedColor.Value.G, GlassColor.SelectedColor.Value.B);
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
			DotSize.Update(_previewStyle.DotSize);
			DotRounding.Update(_previewStyle.DotRounding);
			DotSharpness.Update(_previewStyle.DotSharpness);
			Brightness.Update(_previewStyle.Brightness);
			Gamma.Update(_previewStyle.Gamma);
			DotGlow.Update(_previewStyle.DotGlow);
			BackLevel.Update(_previewStyle.BackGlow);

			TintAmount.Value = _previewStyle.Tint.ScA;
			Color tintColor = _previewStyle.Tint;
			tintColor.A = 255;
			TintColor.SelectedColor = tintColor;

			Color unlitColor = _previewStyle.UnlitDot;
			unlitColor.A = 255;
			UnlitDotColor.SelectedColor = unlitColor;

			GlassDMDLightingValue.Text = DoubleToString2(_previewStyle.GlassLighting);
			GlassDMDLightingSlider.Value = _previewStyle.GlassLighting;

			Color glassColor = _previewStyle.GlassColor;
			glassColor.A = 255;
			GlassColor.SelectedColor = glassColor;

			GlassPath.Text = _previewStyle.GlassTexture;
			GlassPadding.Pad = _previewStyle.GlassPadding;

			FramePath.Text = _previewStyle.FrameTexture;
			FramePadding.Pad = _previewStyle.FramePadding;

			var showTintWarning = _lastFrameFormat != FrameFormat.Gray2 && _lastFrameFormat != FrameFormat.Gray4;
			TintWarning.Visibility = showTintWarning ? Visibility.Visible : Visibility.Collapsed;
		}

		private void LoadPreview()
		{
			if (PreviewMono32x8.IsChecked == true && !_previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-32x8-mono.png"));
			else if (PreviewMono32x8.IsChecked == true && _previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-32x8-tint.png"));
			else if (PreviewMono128x32.IsChecked == true && !_previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x32-mono.png"));
			else if (PreviewMono128x32.IsChecked == true && _previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x32-tint.png"));
			else if (PreviewColor128x32.IsChecked == true)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x32-color.png"));
			else if (PreviewMono128x16.IsChecked == true && !_previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x16-mono.png"));
			else if (PreviewMono128x16.IsChecked == true && _previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-128x16-tint.png"));
			else if (PreviewMono192x64.IsChecked == true && !_previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-192x64-mono.png"));
			else if (PreviewMono192x64.IsChecked == true && _previewStyle.HasTint)
				_preview = new BitmapImage(Global.MakePackUri("Output/Virtual/Dmd/preview-192x64-tint.png"));
			UpdatePreview();
		}

		private void UpdatePreview()
		{
			DmdPreview.SetDimensions(new Dimensions(_preview.PixelWidth, _preview.PixelHeight));
			DmdPreview.SetStyle(_previewStyle, _config.DataPath);
			if (PreviewColor128x32.IsChecked == true || !_previewStyle.HasTint) {
				DmdPreview.RenderBitmap(new BmpFrame(_preview));
			} else {
				DmdPreview.RenderGray4(new DmdFrame(_preview.Dimensions(), ImageUtil.ConvertToGray4(_preview, 1.0), 2));
			}
			var baseWidth = 128.0 * 5.0; // Need to be a multiple of 128.0 and 192.0 to avoid aliasing of the previews
			var baseHeight = 32.0 * 5.0; // Need to be a multiple of 64.0 to avoid aliasing of the previews
			if (DmdPreview.AspectRatio > 4.0)
			{
				DmdPreview.Width = baseWidth;
				DmdPreview.Height = DmdPreview.Width / DmdPreview.AspectRatio;
				var margin = (baseHeight - DmdPreview.Height) *0.5;
				DmdPreview.Margin = new Thickness(0.0, margin, 0.0, margin + 16.0);
			}
			else
			{
				DmdPreview.Height = baseHeight;
				DmdPreview.Width = DmdPreview.Height * DmdPreview.AspectRatio;
				var margin = (baseWidth - DmdPreview.Width) * 0.5;
				DmdPreview.Margin = new Thickness(margin, 0.0, margin, 16.0);
			}
		}

		private void SelectGlassClicked(object sender, RoutedEventArgs e)
		{
			SelectTextureClicked(ref _lastGlassPath, ref GlassPath, "glasses");
		}

		private void SelectFrameClicked(object sender, RoutedEventArgs e)
		{
			SelectTextureClicked(ref _lastFramePath, ref FramePath, "frames");
		}

		private void SelectTextureClicked(ref string lastPath, ref TextBox textbox, string textureFolder) {
			var fileDialog = new System.Windows.Forms.OpenFileDialog();
			var initialPath = GetTextureFolder(lastPath, textureFolder);
			if (initialPath != null) {
				fileDialog.InitialDirectory = initialPath;
			}

			var result = fileDialog.ShowDialog();
			switch (result) {
				case System.Windows.Forms.DialogResult.OK:
					var file = TrimTextureFolder(fileDialog.FileName);
					textbox.Text = file;
					textbox.ToolTip = file;
					lastPath = Path.GetDirectoryName(fileDialog.FileName);
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
				_dmdConfig.ApplyStyle(NewStyleName, true);
			}
		}

		private void ApplyGloballyClicked(object sender, RoutedEventArgs e)
		{
			OnConfigUpdated.OnNext(_previewStyle.Copy());
			if (NewStyleName != null) {
				_dmdConfig.ApplyStyle(NewStyleName, false);
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
			Close();
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
			var previewStyle = _dmdConfig.GetStyle(NewStyleName);
			if (previewStyle != null) {
				_previewStyle = previewStyle;
				UpdateControls();
				UpdatePreview();
			}
		}

		private void DeleteFromIniClicked(object sender, RoutedEventArgs e)
		{
			_dmdConfig.RemoveStyle(NewStyleName);
			StyleNameComboBox.ItemsSource = StyleNames;
			StyleNameComboBox.Text = "";
			StyleSelectionChanged("");
		}

		private string GetTextureFolder(string lastPath, string textureFolder)
		{
			var initialPath = lastPath;
			if (initialPath != null) {
				return initialPath;
			}
			if (_config.DataPath != null) {
				initialPath = _config.DataPath;
				if (Directory.Exists(Path.Combine(initialPath, "textures"))) {
					initialPath = Path.Combine(initialPath, "textures");
				}
				if (Directory.Exists(Path.Combine(initialPath, textureFolder))) {
					initialPath = Path.Combine(initialPath, textureFolder);
				}
			}
			return initialPath;
		}

		private string TrimTextureFolder(string path)
		{
			if (_config.DataPath == null) {
				return path;
			}

			var dataPath = Path.GetFullPath(_config.DataPath);
			path = Path.GetFullPath(path);
			if (path.StartsWith(dataPath)) {
				return path.Substring(dataPath.Length + 1);
			}
			return path;
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
