using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	/// <summary>
	/// Interaction logic for AlphaNumericShaderControl.xaml
	/// </summary>
	public partial class AlphaNumericShaderControl : IVirtualControl
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsAvailable => true;

		public DisplaySetting DisplaySetting { get; set; }
		public bool IgnoreAspectRatio { get; set; }
		public VirtualDisplay Host { get; set; }

		private const int Dpi = 96;

		private WriteableBitmap _writeableBitmap;

		public AlphaNumericShaderControl()
		{
			DataContext = this;
			InitializeComponent();
			DisplaySetting = new DisplaySetting();

			SizeChanged += SizeChanged_Event;
			CompositionTarget.Rendering += (o, e) => DrawImage(_writeableBitmap);
		}

		public void Init()
		{
			Logger.Info("Shader: NumLines = {0}, NumChars = {1}", DisplaySetting.NumLines, DisplaySetting.NumChars);
			ShaderEffect.NumLines = DisplaySetting.NumLines;
			ShaderEffect.NumChars = DisplaySetting.NumChars;
			ShaderEffect.NumSegments = SegmentSize[DisplaySetting.SegmentType];

			ObservableExtensions.Subscribe(Host.WindowResized, pos => CreateImage((int)pos.Width, (int)pos.Height));
		}

		public void CreateImage(int width, int height)
		{
			Logger.Debug("Creating image...");
			SetBitmap(new WriteableBitmap(width, height, Dpi, Dpi, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent));
			ShaderEffect.TargetWidth = width;
			ShaderEffect.TargetHeight = height;
			Logger.Info("Shader: {0}x{1}", ShaderEffect.TargetWidth, ShaderEffect.TargetHeight);
		}

		public void UpdateData(ushort[] data)
		{
			var numSegments = SegmentSize[DisplaySetting.SegmentType];
			var width = numSegments;
			var height = DisplaySetting.NumChars;

			var bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
			var bufferSize = (Math.Abs(bmp.BackBufferStride) * height + 2);
			var frameBuffer = new byte[bufferSize];

			var index = 0;
			bmp.Lock();
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x++) {
					var seg = data[y] >> x & 0x1;
					var val = (byte)(seg == 1 ? 0xff : 0x00);
					frameBuffer[index] = val;
					frameBuffer[index + 1] = val;
					frameBuffer[index + 2] = val;
					frameBuffer[index + 3] = val;
					index += 4;
				}
			}
			bmp.WritePixels(new Int32Rect(0, 0, width, height), frameBuffer, bmp.BackBufferStride, 0);
			bmp.Unlock();
			bmp.Freeze();

			try {
				Dispatcher.Invoke(() => ShaderEffect.Input = new ImageBrush(bmp));

			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Virtual DMD renderer task seems to be lost.");
			}
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (writeableBitmap == null)
			{
				//Logger.Debug("Skipping: _data = {0}, writeableBitmap = {1}, count = {2}", _data, writeableBitmap, _segmentsForegroundRasterized.Count);
				return;
			}

			var width = (int)writeableBitmap.Width;
			var height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			var surfaceInfo = new SKImageInfo
			{
				Width = width,
				Height = height,
				ColorType = SKColorType.Bgra8888,
				AlphaType = SKAlphaType.Premul,
			};
			using (var surface = SKSurface.Create(surfaceInfo, writeableBitmap.BackBuffer, width * 4))
			{
				surface.Canvas.Clear(SKColors.Black);
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		public void RenderSegments(ushort[] data)
		{
			UpdateData(data);
		}

		public void UpdateStyle(RasterizeStyleDefinition styleDef)
		{
		}

		public void ClearDisplay()
		{
		}

		private void SetBitmap(WriteableBitmap bitmap)
		{
			AlphanumericDisplay.Source = _writeableBitmap = bitmap;
		}

		private void SizeChanged_Event(object sender, SizeChangedEventArgs e)
		{
			if (!Host.Resizing) {
				CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
			}
		}
		public void Dispose()
		{
		}

		private readonly Dictionary<SegmentType, int> SegmentSize = new Dictionary<SegmentType, int> {
			{ SegmentType.Alphanumeric, 16 },
			{ SegmentType.Numeric8, 8 },
			{ SegmentType.Numeric10, 10 },
		};
	}
}
