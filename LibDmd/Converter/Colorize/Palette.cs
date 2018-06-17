using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	/// <summary>
	/// Ä palettä isch ä Lischtä vo Farbä. Jedi Palettä het än Index und a Tip wo
	/// seit obs d Standard-Palettä isch odr nid.
	/// </summary>
	public class Palette
	{
		/// <summary>
		/// Dr Palettäindex
		/// </summary>
		public readonly uint Index;

		/// <summary>
		/// Dr Palettätip.
		/// </summary>
		public readonly int Type; //  0: normal, 1: default

		/// <summary>
		/// Das sind d Farbä vo dr Palettä.
		/// </summary>
		public readonly Color[] Colors;

        /// <summary>
        /// Isch true wenns d Haiptpalettä isch
        /// </summary>
        public bool IsDefault => (Type == 1 || Type == 2);
        public bool IsPersistent => (Type == 1);

		private readonly Dictionary<int, Color[]> _colors = new Dictionary<int, Color[]>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Palette(BinaryReader reader)
		{
			Index = reader.ReadUInt16BE();
			//Logger.Trace("  [{1}] [palette] Read index as {0}", Index, reader.BaseStream.Position);
			var numColors = reader.ReadUInt16BE();
			//Logger.Trace("  [{1}] [palette] Read number of colors as {0}", numColors, reader.BaseStream.Position);
			Type = reader.ReadByte();
			//Logger.Trace("  [{1}] [palette] Read type as {0}", Type, reader.BaseStream.Position);
			Colors = new Color[numColors];
			var j = 0;
			for (var i = 0; i < numColors * 3; i += 3) {
				Colors[j] = Color.FromRgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
				j++;
			}
			//Logger.Trace("  [{1}] [palette] Read {0} bytes of color data", numColors * 3, reader.BaseStream.Position);
		}

		public Palette(Color[] colors)
		{
			Index = 0;
			Type = 0;
			Colors = colors;
		}

		public Color[] GetColors(int bitlength)
		{
			if (!_colors.ContainsKey(bitlength)) {
				_colors.Add(bitlength, ColorUtil.GetPalette(Colors, (int)Math.Pow(2, bitlength)));
			}
			return _colors[bitlength];
		}
	}
}
