using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace LibDmd
{
	/// <summary>
	/// Named colors, replacing <c>System.Windows.Media.Colors</c> in the
	/// cross-platform core. Values match the WPF / sRGB definitions so that
	/// rendered output is identical to the legacy Windows build.
	/// </summary>
	public static class DmdColors
	{
		public static DmdColor Transparent => DmdColor.FromArgb(0x00, 0xff, 0xff, 0xff);
		public static DmdColor Black => DmdColor.FromRgb(0x00, 0x00, 0x00);
		public static DmdColor White => DmdColor.FromRgb(0xff, 0xff, 0xff);
		public static DmdColor Red => DmdColor.FromRgb(0xff, 0x00, 0x00);
		public static DmdColor Green => DmdColor.FromRgb(0x00, 0x80, 0x00);
		public static DmdColor Blue => DmdColor.FromRgb(0x00, 0x00, 0xff);
		public static DmdColor Lime => DmdColor.FromRgb(0x00, 0xff, 0x00);
		public static DmdColor Yellow => DmdColor.FromRgb(0xff, 0xff, 0x00);
		public static DmdColor Orange => DmdColor.FromRgb(0xff, 0xa5, 0x00);
		public static DmdColor OrangeRed => DmdColor.FromRgb(0xff, 0x45, 0x00);
		public static DmdColor Cyan => DmdColor.FromRgb(0x00, 0xff, 0xff);
		public static DmdColor Aqua => DmdColor.FromRgb(0x00, 0xff, 0xff);
		public static DmdColor Magenta => DmdColor.FromRgb(0xff, 0x00, 0xff);
		public static DmdColor Fuchsia => DmdColor.FromRgb(0xff, 0x00, 0xff);
		public static DmdColor Purple => DmdColor.FromRgb(0x80, 0x00, 0x80);
		public static DmdColor Pink => DmdColor.FromRgb(0xff, 0xc0, 0xcb);
		public static DmdColor Brown => DmdColor.FromRgb(0xa5, 0x2a, 0x2a);
		public static DmdColor Gray => DmdColor.FromRgb(0x80, 0x80, 0x80);
		public static DmdColor DimGray => DmdColor.FromRgb(0x69, 0x69, 0x69);
		public static DmdColor DarkGray => DmdColor.FromRgb(0xa9, 0xa9, 0xa9);
		public static DmdColor Silver => DmdColor.FromRgb(0xc0, 0xc0, 0xc0);
		public static DmdColor Gold => DmdColor.FromRgb(0xff, 0xd7, 0x00);

		private static readonly Dictionary<string, DmdColor> Named = new Dictionary<string, DmdColor>(StringComparer.OrdinalIgnoreCase) {
			{ "Transparent", Transparent },
			{ "Black", Black },
			{ "White", White },
			{ "Red", Red },
			{ "Green", Green },
			{ "Blue", Blue },
			{ "Lime", Lime },
			{ "Yellow", Yellow },
			{ "Orange", Orange },
			{ "OrangeRed", OrangeRed },
			{ "Cyan", Cyan },
			{ "Aqua", Aqua },
			{ "Magenta", Magenta },
			{ "Fuchsia", Fuchsia },
			{ "Purple", Purple },
			{ "Pink", Pink },
			{ "Brown", Brown },
			{ "Gray", Gray },
			{ "DimGray", DimGray },
			{ "DarkGray", DarkGray },
			{ "Silver", Silver },
			{ "Gold", Gold },
		};

		public static bool TryGetNamed(string name, out DmdColor color) => Named.TryGetValue(name, out color);
	}

	/// <summary>
	/// Replacement for <c>System.Windows.Media.ColorConverter</c>, exposing the
	/// single <see cref="ConvertFromString"/> method the core relies on.
	/// </summary>
	public static class DmdColorConverter
	{
		/// <summary>
		/// Parses a color string and returns a boxed <see cref="DmdColor"/>, matching
		/// the <c>(Color)ColorConverter.ConvertFromString(...)</c> usage pattern.
		/// </summary>
		/// <exception cref="FormatException">If the string cannot be parsed.</exception>
		public static object ConvertFromString(string value) => DmdColor.FromString(value);
	}
}
