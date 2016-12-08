using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace LibDmd.Processor.Coloring
{
	public class Mapping
	{
		/// <summary>
		/// MD5 hash of key frame
		/// </summary>
		public readonly uint Crc32;

		/// <summary>
		/// Mode
		/// </summary>
		public readonly int Mode;

		/// <summary>
		/// Palette index
		/// </summary>
		public readonly ushort PaletteIndex;

		/// <summary>
		/// Duration until switch back to default palette (if 0 don’t switch back at all)
		/// </summary>
		public readonly uint Duration;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Mapping(BinaryReader reader)
		{
			Crc32 = reader.ReadUInt32BE();
			Logger.Trace("  [{1}] [palette] Read crc32 as {0}", Crc32, reader.BaseStream.Position);
			Mode = reader.ReadByte();
			Logger.Trace("  [{1}] [palette] Read mode as {0}", Mode, reader.BaseStream.Position);
			PaletteIndex = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [palette] Read index as {0}", PaletteIndex, reader.BaseStream.Position);
			Duration = reader.ReadUInt32BE();
			Logger.Trace("  [{1}] [palette] Read duration as {0}", Duration, reader.BaseStream.Position);
		}
	}
}
