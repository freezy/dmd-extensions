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
using SkiaSharp;

namespace LibDmd.Common
{
	/// <summary>
	/// Interaction logic for AlphaNumericSettings.xaml
	/// </summary>
	public partial class VirtualAlphaNumericSettings : Window
	{

		private static readonly AlphaNumericResources _res = AlphaNumericResources.GetInstance();

		private readonly AlphanumericControl _control;
		private WriteableBitmap _writeableBitmap;
		private int _previewDisplayNumber;
		private SegmentType _segmentType = SegmentType.Alphanumeric;

		public VirtualAlphaNumericSettings(AlphanumericControl control, int displayNumber, double top, double left)
		{
			Top = top;
			Left = left;
			InitializeComponent();

			_previewDisplayNumber = displayNumber + 100;
			_control = control;
			_writeableBitmap = new WriteableBitmap((int)Preview.Width, (int)Preview.Height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
			Preview.Source = _writeableBitmap;

			CompositionTarget.Rendering += (o, e) => DrawPreview(_writeableBitmap);

			ForegroundStyle.Label = "Foreground Layer";
			InnerGlowStyle.Label = "Inner Glow Layer";
			OuterGlowStyle.Label = "Outer Glow Layer";
			UnlitStyle.Label = "Unlit Layer";

			ForegroundStyle.RasterizeStyle = _control.RasterizeStyle.Foreground;
			InnerGlowStyle.RasterizeStyle = _control.RasterizeStyle.InnerGlow;
			OuterGlowStyle.RasterizeStyle = _control.RasterizeStyle.OuterGlow;
			UnlitStyle.RasterizeStyle = _control.RasterizeStyle.Background;

			var dim = new RasterizeDimensions(_res.GetSvgSize(_segmentType), (int)_writeableBitmap.Width, (int)_writeableBitmap.Height, 1, 1, _control.RasterizeStyle);
			_res.Rasterize(_previewDisplayNumber, _segmentType, dim, _control.RasterizeStyle);
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
				DrawSegment(RasterizeLayer.OuterGlow, canvas, pos);
				DrawSegment(RasterizeLayer.InnerGlow, canvas, pos);
				DrawSegment(RasterizeLayer.Foreground, canvas, pos);
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void DrawSegment(RasterizeLayer layer, SKCanvas canvas, SKPoint canvasPosition)
		{
			const int seg = 16640;
			using (var surfacePaint = new SKPaint()) {
				for (var j = 0; j < _res.SegmentSize[_segmentType]; j++) {
					var rasterizedSegment = _res.GetRasterized(_previewDisplayNumber, layer, _segmentType, j);
					if (((seg >> j) & 0x1) != 0 && rasterizedSegment != null) {
						canvas.DrawSurface(rasterizedSegment, canvasPosition, surfacePaint);
					}
				}
			}
		}

		private void DrawFullSegment(SKCanvas canvas, SKPoint position)
		{
			var segment = _res.GetRasterized(_previewDisplayNumber, RasterizeLayer.Background, _segmentType, AlphaNumericResources.FullSegment);
			if (segment != null) {
				canvas.DrawSurface(segment, position);
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
