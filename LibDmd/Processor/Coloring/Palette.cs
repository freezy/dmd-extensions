using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
		public readonly int Index;

		/// <summary>
		/// type of palette. 0: normal, 1: default (only one palette per file could be marked as default)
		/// </summary>
		public readonly int Type; //  0: normal, 1: default

		/// <summary>
		/// RGB data. Three values (red, green, blue) for each color.
		/// </summary>
		public readonly byte[] Colors;

		public Palette(BinaryReader reader)
		{
			Index = reader.ReadUInt16BE();
			var numColors = reader.ReadUInt16BE();
			Type = reader.ReadByte();
			Colors = reader.ReadBytes(numColors * 3);
		}
	}
}
