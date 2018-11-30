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

		private readonly AlphanumericControl _control;
		private WriteableBitmap _writeableBitmap;

		public VirtualAlphaNumericSettings(AlphanumericControl control, double top, double left)
		{
			Top = top;
			Left = left;
			InitializeComponent();

			_control = control;
			_writeableBitmap = new WriteableBitmap((int)Preview.Width, (int)Preview.Height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
			Preview.Source = _writeableBitmap;

			CompositionTarget.Rendering += (o, e) => DrawPreview(_writeableBitmap);

			ForegroundStyle.RasterizeStyle = _control.RasterizeStyle.Foreground;
			InnerGlowStyle.RasterizeStyle = _control.RasterizeStyle.InnerGlow;
			OuterGlowStyle.RasterizeStyle = _control.RasterizeStyle.OuterGlow;
			UnlitStyle.RasterizeStyle = _control.RasterizeStyle.Background;
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
				canvas.Clear(SKColors.Black);
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
