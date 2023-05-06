using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Vni
{
	/// <summary>
	/// Reads the PAL file format.
	/// 
	/// Example here: http://vpuniverse.com/forums/files/category/84-pin2dmd-files/
	/// Documentation here: https://github.com/sker65/go-dmd-clock/blob/master/doc/README.md
	/// </summary>
	public class PalFile
	{
		public Palette[] Palettes;
		public Dictionary<uint, Mapping> Mappings;
		public byte[][] Masks;
		public Palette DefaultPalette;
		public ushort DefaultPaletteIndex;

		private string _filename;

		/// <summary>
		/// File version. 1 = FSQ, 2 = VNI (but we don't really care, we fetch what we get)
		/// </summary>
		private int _version;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Opens and parses the .vni file.
		/// </summary>
		/// <param name="filename">Path to the file</param>
		public PalFile(string filename)
		{
			using (var fs = new FileStream(filename, FileMode.Open))
			using (var reader = new BinaryReader(fs)) {
				Load(reader, filename);
			}
		}

		public PalFile(byte[] palData, string filename)
		{
			using (var memoryStream = new MemoryStream(palData))
			using (var reader = new BinaryReader(memoryStream)) {
				Load(reader, filename);
			}
		}

		private void Load(BinaryReader reader, string filename)
		{
			Mappings = null;
			Masks = null;
			_filename = filename;
			_version = reader.ReadByte();
			Logger.Trace("[vni] PAL[{1}] Read version as {0}", _version, reader.BaseStream.Position);

			int numPalettes = reader.ReadUInt16BE();
			Logger.Trace("[vni] PAL[{1}] Read number of palettes as {0}", numPalettes, reader.BaseStream.Position);
			Palettes = new Palette[numPalettes];
			for (var i = 0; i < numPalettes; i++) {
				Palettes[i] = new Palette(reader);
				if (DefaultPalette == null && Palettes[i].IsDefault) {
					DefaultPalette = Palettes[i];
					DefaultPaletteIndex = (ushort)i;
				}
			}
			if (DefaultPalette == null && Palettes.Length > 0) {
				DefaultPalette = Palettes[0];
			}

			if (reader.BaseStream.Position == reader.BaseStream.Length) {
				reader.Close();
				return;
			}

			var numMappings = reader.ReadUInt16BE();
			Logger.Trace("[vni] PAL[{1}] Read number of mappings as {0}", numMappings, reader.BaseStream.Position);
			if (reader.BaseStream.Length - reader.BaseStream.Position < Mapping.Length * numMappings) {
				Logger.Warn("[vni] [{1}] Missing {0} bytes for {1} masks, ignoring.", Mapping.Length * numMappings - reader.BaseStream.Length + reader.BaseStream.Position, numMappings);
				reader.Close();
				return;
			}

			if (numMappings > 0) {
				Mappings = new Dictionary<uint, Mapping>();
				for (var i = 0; i < numMappings; i++) {
					var mapping = new Mapping(reader);
					Mappings.Add(mapping.Checksum, mapping);
				}
			} else if (reader.BaseStream.Position == reader.BaseStream.Length) {
				if (reader.BaseStream.Position != reader.BaseStream.Length) {
					Logger.Warn("[vni] PAL[{1}] No mappings found but there are still {0} bytes in the file!", reader.BaseStream.Length - reader.BaseStream.Position, reader.BaseStream.Position);
				}
				reader.Close();
				return;
			}

			var numMasks = reader.ReadByte();
			Logger.Trace("[vni] PAL[{1}] Read number of masks as {0}", numMasks, reader.BaseStream.Position);
			if (numMasks > 0) {
				int maskBytes = (int)(reader.BaseStream.Length - reader.BaseStream.Position) / numMasks;

				if (maskBytes != 256 && maskBytes != 512 && maskBytes != 1536) {
					Logger.Warn("[vni] {0} bytes remaining per {1} masks.  Unknown size, ignoring.", maskBytes, numMasks);
					reader.Close();
					return;
				}
				Masks = new byte[numMasks][];
				for (var i = 0; i < numMasks; i++) {
					Masks[i] = reader.ReadBytesRequired(maskBytes);
					// Logger.Trace("[{1}] Read number of {0} bytes of mask", Masks[i].Length, reader.BaseStream.Position);
				}
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
			Mappings.TryGetValue(checksum, out var mapping);
			return mapping;
		}

		public override string ToString()
		{
			return $"{Path.GetFileName(_filename)}: v{_version}, {Palettes.Length} palette(s), {Mappings.Count} mapping(s), {Masks.Length} mask(s)";
		}
	}
}
