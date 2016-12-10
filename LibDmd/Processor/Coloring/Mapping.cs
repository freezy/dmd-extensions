using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Common;
using NLog;

namespace LibDmd.Processor.Coloring
{
	public class Mapping
	{
		/// <summary>
		/// D Checkum fürs File
		/// </summary>
		public readonly uint Checksum;

		/// <summary>
		/// Dr Modus
		/// </summary>
		public readonly int Mode;

		/// <summary>
		/// Dr Palettäindex
		/// </summary>
		public readonly ushort PaletteIndex;

		/// <summary>
		/// Wiä lang's gaht bis mr zrugg zur Standard-Palettä wächslet (wenn 0 gar nid zrugg wächslä)
		/// </summary>
		public readonly uint Duration;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Mapping(BinaryReader reader)
		{
			Checksum = reader.ReadUInt32BE();
			Logger.Trace("  [{1}] [palette] Read checksum as {0}", Checksum, reader.BaseStream.Position);
			Mode = reader.ReadByte();
			Logger.Trace("  [{1}] [palette] Read mode as {0}", Mode, reader.BaseStream.Position);
			PaletteIndex = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [palette] Read index as {0}", PaletteIndex, reader.BaseStream.Position);
			Duration = reader.ReadUInt32BE();
			Logger.Trace("  [{1}] [palette] Read duration as {0}", Duration, reader.BaseStream.Position);
		}
	}
}
