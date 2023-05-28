using System.IO;
using System.Linq;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Pin2Color
{
	/// <summary>
	/// Das ischd Haiptkonfig firs iifärbä vo Graistuifä-Aazeigä.
	/// 
	/// Biischpiu vom Fileformat hets hiä: http://vpuniverse.com/forums/files/category/84-pin2dmd-files/
	/// Doku übrs Format hiä: https://github.com/sker65/go-dmd-clock/blob/master/doc/README.md
	/// </summary>
	public class VniColoring
	{
		public readonly string Filename;
		/// <summary>
		/// File version. 1 = FSQ, 2 = VNI (but we don't really care, we fetch what we get)
		/// </summary>
		public readonly int Version;
		public readonly Palette[] Palettes;
		public readonly System.Collections.Generic.Dictionary<uint, Mapping> Mappings;
		public readonly byte[][] Masks;
		public readonly Palette DefaultPalette;
		public readonly ushort DefaultPaletteIndex;
		public readonly int NumPalettes;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// List di ganzi Konfig vom File inä.
		/// </summary>
		/// <param name="filename">Dr Pfad zum File</param>
		public VniColoring(string filename)
		{
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);

			Mappings = null;
			Masks = null;
			Filename = filename;
			Version = reader.ReadByte();
			Logger.Trace("[pin2color] PAL[{1}] Read version as {0}", Version, reader.BaseStream.Position);

			NumPalettes = reader.ReadUInt16BE();
			Logger.Trace("[pin2color] PAL[{1}] Read number of palettes as {0}", NumPalettes, reader.BaseStream.Position);
			Palettes = new Palette[NumPalettes];
			for (var i = 0; i < NumPalettes; i++) {
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
			Logger.Trace("[pin2color] PAL[{1}] Read number of mappings as {0}", numMappings, reader.BaseStream.Position);
			if (reader.BaseStream.Length - reader.BaseStream.Position < Mapping.Length * numMappings) {
				Logger.Warn("[pin2color] [{1}] Missing {0} bytes for {1} masks, ignoring.", Mapping.Length * numMappings - reader.BaseStream.Length + reader.BaseStream.Position, numMappings);
				reader.Close();
				return;
			}

			if (numMappings > 0) {
				Mappings = new System.Collections.Generic.Dictionary<uint, Mapping>();
				for (var i = 0; i < numMappings; i++) {
					var mapping = new Mapping(reader);
					Mappings.Add(mapping.Checksum, mapping);
				}
			} else if (numMappings == 0 || reader.BaseStream.Position == reader.BaseStream.Length) {
				if (reader.BaseStream.Position != reader.BaseStream.Length) {
					Logger.Warn("[pin2color] PAL[{1}] No mappings found but there are still {0} bytes in the file!", reader.BaseStream.Length - reader.BaseStream.Position, reader.BaseStream.Position);
				}
				reader.Close();
				return;
			}

			var numMasks = reader.ReadByte();
			Logger.Trace("[pin2color] PAL[{1}] Read number of masks as {0}", numMasks, reader.BaseStream.Position);
			if (numMasks > 0) {
				int maskBytes = (int)(reader.BaseStream.Length - reader.BaseStream.Position) / numMasks;

				if (maskBytes != 256 && maskBytes != 512 && maskBytes != 1536) {
					Logger.Warn("[pin2color] {0} bytes remaining per {1} masks.  Unknown size, ignoring.", maskBytes, numMasks);
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
			return $"{Path.GetFileName(Filename)}: v{Version}, {Palettes.Length} palette(s), {Mappings.Count} mapping(s), {Masks.Length} mask(s)";
		}
	}
}
