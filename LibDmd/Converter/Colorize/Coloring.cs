using System.IO;
using System.Linq;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	/// <summary>
	/// Das ischd Haiptkonfig firs iifärbä vo Graistuifä-Aazeigä.
	/// 
	/// Biischpiu vom Fileformat hets hiä: http://vpuniverse.com/forums/files/category/84-pin2dmd-files/
	/// Doku übrs Format hiä: https://github.com/sker65/go-dmd-clock/blob/master/doc/README.md
	/// </summary>
	public class Coloring
	{
		public readonly string Filename;
		public readonly int Version;
		public readonly Palette[] Palettes;
		public readonly Mapping[] Mappings;
		public readonly byte[][] Masks;
		public readonly Palette DefaultPalette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// List di ganzi Konfig vom File inä.
		/// </summary>
		/// <param name="filename">Dr Pfad zum File</param>
		public Coloring(string filename)
		{
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);

			Filename = filename;
			Version = reader.ReadByte();
			Logger.Trace("[{1}] Read version as {0}", Version, reader.BaseStream.Position);

			var numPalettes = reader.ReadUInt16BE();
			Logger.Trace("[{1}] Read number of palettes as {0}", numPalettes, reader.BaseStream.Position);
			Palettes = new Palette[numPalettes];
			for (var i = 0; i < numPalettes; i++) {
				Palettes[i] = new Palette(reader);
				if (DefaultPalette == null && Palettes[i].IsDefault) {
					DefaultPalette = Palettes[i];
				}
			}
			if (DefaultPalette == null && Palettes.Length > 0) {
				DefaultPalette = Palettes[0];
			}

			if (reader.BaseStream.Position == reader.BaseStream.Length) {
				Mappings = new Mapping[0];
				Masks = new byte[0][];
				reader.Close();
				return;
			}

			var numMappings = reader.ReadUInt16BE();
			Logger.Trace("[{1}] Read number of mappings as {0}", numMappings, reader.BaseStream.Position);
			if (reader.BaseStream.Length - reader.BaseStream.Position < Mapping.Length * numMappings) {
				Logger.Warn("[{1}] Missing {0} bytes for {1} masks, ignoring.", Mapping.Length * numMappings - reader.BaseStream.Length + reader.BaseStream.Position, numMappings);
				Mappings = new Mapping[0];
				Masks = new byte[0][];
				reader.Close();
				return;
			}

			Mappings = new Mapping[numMappings];
			for (var i = 0; i < numMappings; i++) {
				Mappings[i] = new Mapping(reader);
			}

			if (numMappings == 0 || reader.BaseStream.Position == reader.BaseStream.Length) {
				if (reader.BaseStream.Position != reader.BaseStream.Length) {
					Logger.Warn("[{1}] No mappings found but there are still {0} bytes in the file!", reader.BaseStream.Length - reader.BaseStream.Position, reader.BaseStream.Position);
				}
				Masks = new byte[0][];
				reader.Close();
				return;
			}

			var numMasks = reader.ReadByte();
			Logger.Trace("[{1}] Read number of masks as {0}", numMasks, reader.BaseStream.Position);
			if (reader.BaseStream.Length - reader.BaseStream.Position < 512 * numMasks) {
				Logger.Warn("Missing {0} bytes for {1} masks, ignoring.", 512 * numMasks - reader.BaseStream.Length + reader.BaseStream.Position, numMasks);
				Mappings = new Mapping[0];
				Masks = new byte[0][];
				reader.Close();
				return;
			}
			Masks = new byte[numMasks][];
			for (var i = 0; i < numMasks; i++) {
				Masks[i] = reader.ReadBytesRequired(512);
				// Logger.Trace("[{1}] Read number of {0} bytes of mask", Masks[i].Length, reader.BaseStream.Position);
			}

			if (reader.BaseStream.Position != reader.BaseStream.Length) {
				throw new IOException("Read error, finished parsing but there are still " + (reader.BaseStream.Length - reader.BaseStream.Position) + " bytes to read.");
			}

			reader.Close();
		}

		public Palette GetPalette(uint index)
		{
			// TODO index bruichä
			return Palettes.FirstOrDefault(p => p.Index == index);
		}

		public Mapping FindMapping(uint checksum)
		{
			// TODO index bruichä
			return Mappings.FirstOrDefault(m => m.Checksum == checksum);
		}

		public override string ToString()
		{
			return $"{Path.GetFileName(Filename)}: v{Version}, {Palettes.Length} palette(s), {Mappings.Length} mapping(s), {Masks.Length} mask(s)";
		}

		
	}
}