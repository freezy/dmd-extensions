using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	internal class AlphaNumericPainter
	{
		private static readonly AlphaNumericResources Res = AlphaNumericResources.GetInstance();

		public static void DrawDisplay(SKSurface surface, DisplaySetting ds, ushort[] data, ConcurrentDictionary<int, double> switchPercentage = null)
		{
			var canvas = surface.Canvas;
			canvas.Clear(ds.StyleDefinition.BackgroundColor);
			if (ds.StyleDefinition.Background.IsEnabled) {
				DrawSegments(ds, canvas, (i, c, p) => DrawFullSegment(ds, c, p));
			}
			if (ds.StyleDefinition.OuterGlow.IsEnabled) {
				DrawSegments(ds, canvas, (i, c, p) => DrawSegment(ds, RasterizeLayer.OuterGlow, data.Length > i ? data[i] : (ushort)0, c, p, GetPercentage(switchPercentage, i)));
			}
			if (ds.StyleDefinition.InnerGlow.IsEnabled) {
				DrawSegments(ds, canvas, (i, c, p) => DrawSegment(ds, RasterizeLayer.InnerGlow, data.Length > i ? data[i] : (ushort)0, c, p, GetPercentage(switchPercentage, i)));
			}
			if (ds.StyleDefinition.Foreground.IsEnabled) {
				DrawSegments(ds, canvas, (i, c, p) => DrawSegment(ds, RasterizeLayer.Foreground, data.Length > i ? data[i] : (ushort)0, c, p, GetPercentage(switchPercentage, i)));
			}
		}

		private static double GetPercentage(ConcurrentDictionary<int, double> switchPercentage, int pos)
		{
			if (switchPercentage == null) {
				return 1;
			}
			return switchPercentage.ContainsKey(pos) ? switchPercentage[pos] : 1;
		}

		private static void DrawFullSegment(DisplaySetting ds, SKCanvas canvas, SKPoint position)
		{
			var segment = Res.GetRasterized(ds.Display, RasterizeLayer.Background, ds.SegmentType, ds.StyleDefinition.SegmentWeight, AlphaNumericResources.FullSegment);
			if (segment != null) {
				canvas.DrawSurface(segment, position);
			}
		}

		public static void DrawSegments(DisplaySetting displaySetting, SKCanvas canvas, Action<int, SKCanvas, SKPoint> draw)
		{
			float posX = displaySetting.Dim.OuterPadding;
			float posY = displaySetting.Dim.OuterPadding;
			for (var j = 0; j < displaySetting.NumLines; j++) {
				for (var i = 0; i < displaySetting.NumChars; i++) {
					draw(i + displaySetting.NumChars * j, canvas, new SKPoint(posX - displaySetting.Dim.SegmentPadding, posY - displaySetting.Dim.SegmentPadding));
					posX += displaySetting.Dim.SvgWidth;
				}
				posX = displaySetting.Dim.OuterPadding;
				posY += displaySetting.Dim.SvgHeight + displaySetting.Dim.LinePadding;
			}
		}

		public static void DrawSegment(DisplaySetting displaySetting, RasterizeLayer layer, ushort seg, SKCanvas canvas, SKPoint canvasPosition, double percentage)
		{
			using (var surfacePaint = new SKPaint()) {
				if (percentage < 1) {
					surfacePaint.Color = new SKColor(0, 0, 0, (byte)Math.Round(percentage * 255));
				}
				for (var j = 0; j < Res.SegmentSize[displaySetting.SegmentType]; j++) {
					var rasterizedSegment = Res.GetRasterized(displaySetting.Display, layer, displaySetting.SegmentType, displaySetting.StyleDefinition.SegmentWeight, j);
					if (((seg >> j) & 0x1) != 0 && rasterizedSegment != null) {
						canvas.DrawSurface(rasterizedSegment, canvasPosition, surfacePaint);
					}
				}
			}
		}

		public static ushort[] GenerateAlphaNumeric(string text)
		{
			var data = new ushort[text.Length];
			for (var i = 0; i < text.Length; i++) {
				if (AlphaNumericMap.ContainsKey(text[i])) {
					data[i] = AlphaNumericMap[text[i]];
				} else {
					data[i] = AlphaNumericMap[' '];
				}
			}
			return data;
		}

		private static readonly Dictionary<char, ushort> AlphaNumericMap = new Dictionary<char, ushort>
		{
			{ '0', 0x443f },
			{ '1', 0x406 },
			{ '2', 0x85b },
			{ '3', 0x80f },
			{ '4', 0x866 },
			{ '5', 0x1069 },
			{ '6', 0x87d },
			{ '7', 0x7 },
			{ '8', 0x87f },
			{ '9', 0x86f },
			{ ' ', 0x0 },
			{ '!', 0x86 },
			{ '"', 0x202 },
			{ '#', 0x2a4e },
			{ '$', 0x2a6d },
			{ '%', 0x7f64 },
			{ '&', 0x1359 },
			{ '\'', 0x200 },
			{ '(', 0x1400 },
			{ ')', 0x4100 },
			{ '*', 0x7f40 },
			{ '+', 0x2a40 },
			{ ',', 0x4000 },
			{ '-', 0x840 },
			{ '.', 0x80 },
			{ '/', 0x4400 },
			{ ':', 0x2200 },
			{ ';', 0x4200 },
			{ '<', 0x1440 },
			{ '=', 0x848 },
			{ '>', 0x4900 },
			{ '?', 0x2883 },
			{ '@', 0xa3b },
			{ 'A', 0x877 },
			{ 'B', 0x2a0f },
			{ 'C', 0x39 },
			{ 'D', 0x220f },
			{ 'E', 0x79 },
			{ 'F', 0x71 },
			{ 'G', 0x83d },
			{ 'H', 0x876 },
			{ 'I', 0x2209 },
			{ 'J', 0x1e },
			{ 'K', 0x1470 },
			{ 'L', 0x38 },
			{ 'M', 0x536 },
			{ 'N', 0x1136 },
			{ 'O', 0x3f },
			{ 'P', 0x873 },
			{ 'Q', 0x103f },
			{ 'R', 0x1873 },
			{ 'S', 0x86d },
			{ 'T', 0x2201 },
			{ 'U', 0x3e },
			{ 'V', 0x4430 },
			{ 'W', 0x5036 },
			{ 'X', 0x5500 },
			{ 'Y', 0x86e },
			{ 'Z', 0x4409 },
			{ '[', 0x39 },
			{ '\\', 0x1100 },
			{ ']', 0xf },
			{ '^', 0x5000 },
			{ '_', 0x8 },
			{ '`', 0x100 },
			{ 'a', 0x2058 },
			{ 'b', 0x1078 },
			{ 'c', 0x858 },
			{ 'd', 0x480e },
			{ 'e', 0x4058 },
			{ 'f', 0x2c40 },
			{ 'g', 0xc0e },
			{ 'h', 0x2070 },
			{ 'i', 0x2000 },
			{ 'j', 0x4210 },
			{ 'k', 0x3600 },
			{ 'l', 0x30 },
			{ 'm', 0x2854 },
			{ 'n', 0x2050 },
			{ 'o', 0x85c },
			{ 'p', 0x170 },
			{ 'q', 0xc06 },
			{ 'r', 0x50 },
			{ 's', 0x1808 },
			{ 't', 0x78 },
			{ 'u', 0x1c },
			{ 'v', 0x4010 },
			{ 'w', 0x5014 },
			{ 'x', 0x5500 },
			{ 'y', 0xa0e },
			{ 'z', 0x4048 },
			{ '{', 0x4149 },
			{ '|', 0x2200 },
			{ '}', 0x1c09 },
			{ '~', 0x4c40 },
		};
	}
}
