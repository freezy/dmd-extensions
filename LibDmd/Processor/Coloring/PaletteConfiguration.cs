using System;
using System.IO;

namespace LibDmd.Processor.Coloring
{
	public class PaletteConfiguration
	{
		public readonly string Filename;
		public readonly int Version;
		public readonly Palette[] Palettes;
		public readonly Mapping[] Mappings;
		public readonly byte[][] Masks;

		public PaletteConfiguration(string filename)
		{
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);

			Filename = filename;
			Version = reader.ReadByte();

			var numPalettes = reader.ReadUInt16BE();
			Palettes = new Palette[numPalettes];
			for (var i = 0; i < numPalettes; i++) {
				Palettes[i] = new Palette(reader);
			}

			if (reader.BaseStream.Position == reader.BaseStream.Length) {
				Mappings = new Mapping[0];
				Masks = new byte[0][];
				reader.Close();
				return;
			}

			var numMappings = reader.ReadUInt16BE();
			Mappings = new Mapping[numMappings];
			for (var i = 0; i < numMappings; i++) {
				Mappings[i] = new Mapping(reader);
			}

			if (reader.BaseStream.Position == reader.BaseStream.Length) {
				Masks = new byte[0][];
				reader.Close();
				return;
			}

			var numMasks = reader.ReadByte();
			Masks = new byte[numMasks][];
			for (var i = 0; i < numMappings; i++) {
				Masks[i] = reader.ReadBytes(512);
			}

			if (reader.BaseStream.Position != reader.BaseStream.Length) {
				throw new IOException("Read error, finished parsing but there are still " + (reader.BaseStream.Length - reader.BaseStream.Position) + " bytes to read.");
			}

			reader.Close();
		}

		public override string ToString()
		{
			return $"{Path.GetFileName(Filename)}: v{Version}, {Palettes.Length} palettes, {Mappings.Length} mappings, {Masks.Length} masks";
		}
	}
}