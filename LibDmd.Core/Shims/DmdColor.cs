using System;
using System.Globalization;

// ReSharper disable once CheckNamespace
namespace LibDmd
{
	/// <summary>
	/// A platform-neutral 32-bit ARGB color.
	///
	/// This replaces <c>System.Windows.Media.Color</c> in the cross-platform core
	/// (<c>LibDmd.Core</c>). The core source files keep referring to <c>Color</c> /
	/// <c>Colors</c> / <c>ColorConverter</c>; the <c>LibDmd.Core</c> project aliases
	/// those names to <see cref="DmdColor"/> / <see cref="DmdColors"/> /
	/// <see cref="DmdColorConverter"/> via MSBuild global usings, so the same shared
	/// sources compile both against WPF (the legacy Windows build) and against this
	/// neutral type (the cross-platform build) with no per-file edits.
	///
	/// The field layout (A, R, G, B as mutable bytes) mirrors WPF's <c>Color</c>,
	/// so member access (<c>.R</c>, <c>.G</c>, <c>.B</c>, <c>.A</c>) and the
	/// <c>FromRgb</c> / <c>FromArgb</c> factories behave identically.
	/// </summary>
	public struct DmdColor : IEquatable<DmdColor>
	{
		public byte A;
		public byte R;
		public byte G;
		public byte B;

		public DmdColor(byte r, byte g, byte b)
		{
			A = 0xff;
			R = r;
			G = g;
			B = b;
		}

		public DmdColor(byte a, byte r, byte g, byte b)
		{
			A = a;
			R = r;
			G = g;
			B = b;
		}

		public static DmdColor FromRgb(byte r, byte g, byte b) => new DmdColor(0xff, r, g, b);

		public static DmdColor FromArgb(byte a, byte r, byte g, byte b) => new DmdColor(a, r, g, b);

		/// <summary>
		/// Parses a hex color string, with or without leading <c>#</c>, in
		/// <c>RGB</c>, <c>ARGB</c>, <c>RRGGBB</c> or <c>AARRGGBB</c> form, or a
		/// known named color (see <see cref="DmdColors"/>).
		/// </summary>
		/// <exception cref="FormatException">If the string cannot be parsed.</exception>
		public static DmdColor FromString(string c)
		{
			if (TryFromString(c, out var color)) {
				return color;
			}
			throw new FormatException($"Cannot parse \"{c}\" as a color.");
		}

		public static bool TryFromString(string str, out DmdColor color)
		{
			color = default;
			if (string.IsNullOrEmpty(str)) {
				return false;
			}

			var c = str.Trim();
			if (c[0] == '#') {
				c = c.Substring(1);
			} else if (DmdColors.TryGetNamed(c, out color)) {
				return true;
			}

			// expand shorthand #RGB / #ARGB to #RRGGBB / #AARRGGBB
			if (c.Length == 3 || c.Length == 4) {
				var sb = new char[c.Length * 2];
				for (var i = 0; i < c.Length; i++) {
					sb[i * 2] = c[i];
					sb[i * 2 + 1] = c[i];
				}
				c = new string(sb);
			}

			switch (c.Length) {
				case 6:
					if (TryParseHex(c, 0, out var r6) && TryParseHex(c, 2, out var g6) && TryParseHex(c, 4, out var b6)) {
						color = new DmdColor(0xff, r6, g6, b6);
						return true;
					}
					return false;

				case 8:
					if (TryParseHex(c, 0, out var a8) && TryParseHex(c, 2, out var r8) && TryParseHex(c, 4, out var g8) && TryParseHex(c, 6, out var b8)) {
						color = new DmdColor(a8, r8, g8, b8);
						return true;
					}
					return false;

				default:
					return false;
			}
		}

		private static bool TryParseHex(string s, int pos, out byte value)
			=> byte.TryParse(s.Substring(pos, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

		public bool Equals(DmdColor other) => A == other.A && R == other.R && G == other.G && B == other.B;

		public override bool Equals(object obj) => obj is DmdColor other && Equals(other);

		public override int GetHashCode() => (A << 24) | (R << 16) | (G << 8) | B;

		public static bool operator ==(DmdColor left, DmdColor right) => left.Equals(right);

		public static bool operator !=(DmdColor left, DmdColor right) => !left.Equals(right);

		public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
	}
}
