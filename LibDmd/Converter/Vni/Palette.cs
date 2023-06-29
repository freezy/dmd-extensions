using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using LibDmd.Common;

namespace LibDmd.Converter.Vni
{
	/// <summary>
	/// A Palette is a list of colors. Each palette has an index and a type
	/// that defines whether it's the standard palette or not.
	///
	/// The palette is stored in the .pal file.
	/// </summary>
	public class Palette
	{
		/// <summary>
		/// Palette index.
		/// </summary>
		public readonly uint Index;

		/// <summary>
		/// The actual palette. It's an array colors.
		/// </summary>
		public readonly Color[] Colors;

		/// <summary>
		/// Whether this is the default palette.
		/// </summary>
		public bool IsDefault => (_type == 1 || _type == 2);

		/// <summary>
		/// The palette which is active by default.
		/// </summary>
		public bool IsPersistent => (_type == 1);

		/// <summary>
		/// Palette type from VNI file.
		///
		/// 0: normal, 1: default
		/// </summary>
		private readonly int _type;

		private readonly Dictionary<int, Color[]> _colors = new Dictionary<int, Color[]>();

		//private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Palette(BinaryReader reader)
		{
			Index = reader.ReadUInt16BE();
			//Logger.Trace("  [{1}] [palette] Read index as {0}", Index, reader.BaseStream.Position);
			var numColors = reader.ReadUInt16BE();
			//Logger.Trace("  [{1}] [palette] Read number of colors as {0}", numColors, reader.BaseStream.Position);
			_type = reader.ReadByte();
			//Logger.Trace("  [{1}] [palette] Read type as {0}", Type, reader.BaseStream.Position);
			Colors = new Color[numColors];
			var j = 0;
			for (var i = 0; i < numColors * 3; i += 3) {
				Colors[j] = Color.FromRgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
				j++;
			}
			//Logger.Trace("  [{1}] [palette] Read {0} bytes of color data", numColors * 3, reader.BaseStream.Position);
		}

		/// <summary>
		/// Returns the palette for a given bit length
		/// </summary>
		/// <param name="bitLength">Bit length</param>
		/// <returns>Number of colors depending on bit length</returns>
		public Color[] GetColors(int bitLength)
		{
			if (!_colors.ContainsKey(bitLength)) {
				_colors.Add(bitLength, ColorUtil.GetPalette(Colors, (int)Math.Pow(2, bitLength)));
			}
			return _colors[bitLength];
		}
	}
}
