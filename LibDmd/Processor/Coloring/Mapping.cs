using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Processor.Coloring
{
	public class Mapping
	{
		/// <summary>
		/// MD5 hash of key frame
		/// </summary>
		public readonly byte[] Hash;

		/// <summary>
		/// Palette index
		/// </summary>
		public readonly int PaletteIndex;

		/// <summary>
		/// Offset in fsq file for replacement frames seq (or 0 if just palette switching)
		/// </summary>
		public readonly ulong Offset;

		/// <summary>
		/// Duration until switch back to default palette (if 0 don’t switch back at all)
		/// </summary>
		public readonly int Duration;

		public Mapping(BinaryReader reader)
		{
			Hash = reader.ReadBytes(16);
			PaletteIndex = reader.ReadUInt16BE();
			Offset = reader.ReadUInt64BE();
			Duration = reader.ReadUInt16BE();

		}
	}
}
