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

		private static readonly AlphaNumericResources Res = AlphaNumericResources.GetInstance();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly DisplaySetting _displaySetting;
		private readonly AlphanumericControl _control;

		public VirtualAlphaNumericSettings(AlphanumericControl control, double top, double left)
		{
			Top = top;
			Left = left;
			_control = control;

			InitializeComponent();
			Title = "[" + control.DisplaySetting.Display + "] " + Title;

			_displaySetting = new DisplaySetting(
				control.DisplaySetting.Display + 100, 
				control.DisplaySetting.SegmentType, 
				control.DisplaySetting.CopyStyle(), 
				1, 
				1, 
				(int)Preview.Width, 
				(int)Preview.Height
			);
			var writeableBitmap = new WriteableBitmap((int)Preview.Width, (int)Preview.Height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
			Preview.Source = writeableBitmap;

			CompositionTarget.Rendering += (o, e) => DrawPreview(writeableBitmap);

			// name each layer
			ForegroundStyle.Label = "Foreground Layer";
			InnerGlowStyle.Label = "Inner Glow Layer";
			OuterGlowStyle.Label = "Outer Glow Layer";
			BackgroundStyle.Label = "Unlit Layer";

			// save our editable copy of the control's style
			ForegroundStyle.RasterizeStyle = _displaySetting.Style.Foreground;
			InnerGlowStyle.RasterizeStyle = _displaySetting.Style.InnerGlow;
			OuterGlowStyle.RasterizeStyle = _displaySetting.Style.OuterGlow;
			BackgroundStyle.RasterizeStyle = _displaySetting.Style.Background;
		
			// rasterize preview a first time
			Res.Rasterize(_displaySetting, true);

			var segments = new[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14};

			// subscribe to control changes that trigger rasterization
			ForegroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.Foreground, layerStyle);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.Foreground, layerStyle, segments, _displaySetting.Style.SkewAngle);
			});
			InnerGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.InnerGlow, layerStyle);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.InnerGlow, layerStyle, segments, _displaySetting.Style.SkewAngle);
			});
			OuterGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.OuterGlow, layerStyle);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.OuterGlow, layerStyle, segments, _displaySetting.Style.SkewAngle);
			});
			BackgroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.Background, layerStyle);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.Background, layerStyle, new [] { AlphaNumericResources.FullSegment }, _displaySetting.Style.SkewAngle);
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
				if (BackgroundStyle.RasterizeStyle.IsEnabled) {
					DrawFullSegment(canvas, pos);
				}
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
				for (var j = 0; j < Res.SegmentSize[_displaySetting.SegmentType]; j++) {
					var rasterizedSegment = Res.GetRasterized(_displaySetting.Display, layer, _displaySetting.SegmentType, j);
					if (((seg >> j) & 0x1) != 0 && rasterizedSegment != null) {
						canvas.DrawSurface(rasterizedSegment, canvasPosition, surfacePaint);
					}
				}
			}
		}

		private void DrawFullSegment(SKCanvas canvas, SKPoint position)
		{
			var segment = Res.GetRasterized(_displaySetting.Display, RasterizeLayer.Background, _displaySetting.SegmentType, AlphaNumericResources.FullSegment);
			if (segment != null) {
				canvas.DrawSurface(segment, position);
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Hide();
		}

		private void ApplyButton_Click(object sender, RoutedEventArgs e)
		{
			_control.UpdateStyle(_displaySetting.CopyStyle());
		}
	}
}
