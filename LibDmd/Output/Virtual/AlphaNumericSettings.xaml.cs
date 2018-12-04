using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
using NLog;
using SkiaSharp;

namespace LibDmd.Common
{
	/// <summary>
	/// Interaction logic for AlphaNumericSettings.xaml
	/// </summary>
	public partial class VirtualAlphaNumericSettings : Window
	{

		private static readonly AlphaNumericResources _res = AlphaNumericResources.GetInstance();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly DisplaySetting DisplaySetting;
		private WriteableBitmap _writeableBitmap;

		public VirtualAlphaNumericSettings(AlphanumericControl control, double top, double left)
		{
			Top = top;
			Left = left;
			InitializeComponent();
			Title = "[" + control.DisplaySetting.Display + "] " + Title;

			DisplaySetting = new DisplaySetting {
				Display = control.DisplaySetting.Display + 100,
				Style = control.DisplaySetting.Style.Copy(),
				NumLines = 1,
				NumChars = 1
			};
			DisplaySetting.OnSvgsLoaded(_res.GetSvgSize(DisplaySetting.SegmentType), (int)Preview.Width, (int)Preview.Height);

			_writeableBitmap = new WriteableBitmap((int)Preview.Width, (int)Preview.Height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
			Preview.Source = _writeableBitmap;

			CompositionTarget.Rendering += (o, e) => DrawPreview(_writeableBitmap);

			// name each layer
			ForegroundStyle.Label = "Foreground Layer";
			InnerGlowStyle.Label = "Inner Glow Layer";
			OuterGlowStyle.Label = "Outer Glow Layer";
			UnlitStyle.Label = "Unlit Layer";

			// save our editable copy of the control's style
			ForegroundStyle.RasterizeStyle = DisplaySetting.Style.Foreground;
			InnerGlowStyle.RasterizeStyle = DisplaySetting.Style.InnerGlow;
			OuterGlowStyle.RasterizeStyle = DisplaySetting.Style.OuterGlow;
			UnlitStyle.RasterizeStyle = DisplaySetting.Style.Background;
		
			// rasterize preview a first time
			_res.Rasterize(DisplaySetting, true);

			// subscribe to control changes that trigger rasterization
			ForegroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				DisplaySetting.Style.Foreground = layerStyle;
				_res.Rasterize(DisplaySetting, RasterizeLayer.Foreground, layerStyle, DisplaySetting.Style.SkewAngle);
			});
			InnerGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				DisplaySetting.Style.InnerGlow = layerStyle;
				_res.Rasterize(DisplaySetting, RasterizeLayer.InnerGlow, layerStyle, DisplaySetting.Style.SkewAngle);
			});
			OuterGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				DisplaySetting.Style.OuterGlow = layerStyle;
				_res.Rasterize(DisplaySetting, RasterizeLayer.OuterGlow, layerStyle, DisplaySetting.Style.SkewAngle);
			});
		}

		private void DrawPreview(WriteableBitmap writeableBitmap)
		{
			var width = (int)writeableBitmap.Width;
			var height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			var surfaceInfo = new SKImageInfo {
				Width = width,
				Height = height,
				ColorType = SKColorType.Bgra8888,
				AlphaType = SKAlphaType.Premul,
			};
			using (var surface = SKSurface.Create(surfaceInfo, writeableBitmap.BackBuffer, width * 4)) {
				var canvas = surface.Canvas;
				var pos = new SKPoint(-15, 0);
				canvas.Clear(SKColors.Black);
				DrawFullSegment(canvas, pos);
				if (OuterGlowStyle.RasterizeStyle.IsEnabled) {
					DrawSegment(RasterizeLayer.OuterGlow, canvas, pos);
				}
				if (InnerGlowStyle.RasterizeStyle.IsEnabled) {
					DrawSegment(RasterizeLayer.InnerGlow, canvas, pos);
				}
				if (ForegroundStyle.RasterizeStyle.IsEnabled) {
					DrawSegment(RasterizeLayer.Foreground, canvas, pos);
				}
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void DrawSegment(RasterizeLayer layer, SKCanvas canvas, SKPoint canvasPosition)
		{
			const int seg = 16640;
			using (var surfacePaint = new SKPaint()) {
				for (var j = 0; j < _res.SegmentSize[DisplaySetting.SegmentType]; j++) {
					var rasterizedSegment = _res.GetRasterized(DisplaySetting.Display, layer, DisplaySetting.SegmentType, j);
					if (((seg >> j) & 0x1) != 0 && rasterizedSegment != null) {
						canvas.DrawSurface(rasterizedSegment, canvasPosition, surfacePaint);
					}
				}
			}
		}

		private void DrawFullSegment(SKCanvas canvas, SKPoint position)
		{
			var segment = _res.GetRasterized(DisplaySetting.Display, RasterizeLayer.Background, DisplaySetting.SegmentType, AlphaNumericResources.FullSegment);
			if (segment != null) {
				canvas.DrawSurface(segment, position);
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Hide();
		}
	}
}
