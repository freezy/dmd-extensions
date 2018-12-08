﻿using System;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	/// <summary>
	/// Interaction logic for AlphaNumericSettings.xaml
	/// </summary>
	public partial class VirtualAlphaNumericSettings : Window
	{
		private static readonly AlphaNumericResources Res = AlphaNumericResources.GetInstance();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static int Dpi = 96;

		private readonly DisplaySetting _displaySetting;
		private readonly AlphanumericControl _control;
		private ushort[] _data = { };

		public VirtualAlphaNumericSettings(AlphanumericControl control, double top, double left)
		{
			Top = top;
			Left = left;
			_control = control;

			InitializeComponent();
			Title = "[" + control.DisplaySetting.Display + "] " + Title;
			PreviewText.TextChanged += PreviewTextChanged;
			PreviewText.Text = "  DMDEXT  ";

			_displaySetting = new DisplaySetting(
				control.DisplaySetting.Display + 100, 
				control.DisplaySetting.SegmentType, 
				control.DisplaySetting.StyleDefinition.Copy(), 
				10, 
				1, 
				(int)Preview.Width, 
				(int)Preview.Height
			);
			Logger.Info("Creating preview image at {0}x{1}", (int)Preview.Width, (int)Preview.Height);
			var writeableBitmap = new WriteableBitmap((int)Preview.Width, (int)Preview.Height, Dpi, Dpi, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
			Preview.Source = writeableBitmap;

			CompositionTarget.Rendering += (o, e) => DrawPreview(writeableBitmap);

			// name each layer
			ForegroundStyle.Label = "Foreground Layer";
			InnerGlowStyle.Label = "Inner Glow Layer";
			OuterGlowStyle.Label = "Outer Glow Layer";
			BackgroundStyle.Label = "Unlit Layer";

			ApplySetting();

			var segments = new[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14};

			// subscribe to control changes that trigger rasterization
			ForegroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyleDef => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.Foreground, layerStyleDef);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.Foreground, layerStyleDef, _displaySetting.Style.Foreground, segments, _displaySetting.StyleDefinition.SkewAngle);
			});
			InnerGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.InnerGlow, layerStyle);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.InnerGlow, layerStyle, _displaySetting.Style.InnerGlow, segments, _displaySetting.StyleDefinition.SkewAngle);
			});
			OuterGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.OuterGlow, layerStyle);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.OuterGlow, layerStyle, _displaySetting.Style.OuterGlow, segments, _displaySetting.StyleDefinition.SkewAngle);
			});
			BackgroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				_displaySetting.ApplyLayerStyle(RasterizeLayer.Background, layerStyle);
				Res.RasterizeLayer(_displaySetting, RasterizeLayer.Background, layerStyle, _displaySetting.Style.Background, new [] { AlphaNumericResources.FullSegment }, _displaySetting.StyleDefinition.SkewAngle);
			});
		}

		private void PreviewTextChanged(object sender, TextChangedEventArgs e)
		{
			_data = AlphaNumericPainter.GenerateAlphaNumeric(PreviewText.Text);
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
				AlphaNumericPainter.DrawDisplay(surface, _displaySetting, _data);
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void ApplySetting()
		{
			// apply style to controls
			ForegroundStyle.RasterizeStyleDefinition = _displaySetting.StyleDefinition.Foreground;
			InnerGlowStyle.RasterizeStyleDefinition = _displaySetting.StyleDefinition.InnerGlow;
			OuterGlowStyle.RasterizeStyleDefinition = _displaySetting.StyleDefinition.OuterGlow;
			BackgroundStyle.RasterizeStyleDefinition = _displaySetting.StyleDefinition.Background;

			// rasterize preview a first time
			Res.Rasterize(_displaySetting, true);
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Hide();
		}

		private void ApplyButton_Click(object sender, RoutedEventArgs e)
		{
			_control.UpdateStyle(_displaySetting.StyleDefinition.Copy());
		}

		private void Reset_Click(object sender, RoutedEventArgs e)
		{
			_displaySetting.ApplyStyle(_control.DisplaySetting.StyleDefinition);
			ApplySetting();
		}
	}
}