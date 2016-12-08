using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using NLog;

namespace LibDmd.Processor.Coloring
{
	/// <summary>
	/// A palette is a list of colors. It comes with an index and a type for
	/// telling if it's the default palette or not.
	/// </summary>
	public class Palette
	{
		/// <summary>
		/// Palette index
		/// </summary>
		public readonly uint Index;

		/// <summary>
		/// type of palette. 0: normal, 1: default (only one palette per file could be marked as default)
		/// </summary>
		public readonly int Type; //  0: normal, 1: default

		/// <summary>
		/// RGB data. Three values (red, green, blue) for each color.
		/// </summary>
		public readonly byte[] Colors;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Color[] _palette;

		public Palette(BinaryReader reader)
		{
			Index = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [palette] Read index as {0}", Index, reader.BaseStream.Position);
			var numColors = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [palette] Read number of colors as {0}", numColors, reader.BaseStream.Position);
			Type = reader.ReadByte();
			Logger.Trace("  [{1}] [palette] Read type as {0}", Type, reader.BaseStream.Position);
			Colors = reader.ReadBytesRequired(numColors * 3);
			Logger.Trace("  [{1}] [palette] Read {0} bytes of color data", Colors.Length, reader.BaseStream.Position);
		}

		public Color[] GetPalette()
		{
			if (_palette == null) {
				_palette = new Color[Colors.Length / 3];
				var j = 0;
				for (var i = 0; i < _palette.Length; i += 3) {
					_palette[j] = Color.FromRgb(Colors[i], Colors[i + 1], Colors[i + 2]);
					j++;
				}
			}
			return _palette;
		}
	}
}
