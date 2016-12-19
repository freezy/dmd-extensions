using System.IO;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	/// <summary>
	/// S Mäpping definiärt Häsches wo Sachä uisleesid wiä ä Palettäwächsu odr
	/// än Animazion.
	/// </summary>
	public class Mapping
	{
		/// <summary>
		/// D Checkum vom Biud
		/// </summary>
		public readonly uint Checksum;

		/// <summary>
		/// Dr Modus. 
		/// 
		/// Vo dem gits drii vrschidini: 
		/// 
		///  - `0`: Nii Palettä muäss gladä wärdä. Drbii isch dr `PaletteIndex`
		///    d Numärä vo dr Palettä uism Palettä-Feil. Wiä lang d Palettä
		///    gladä wird chunnt vo dr `Duration`.
		///  - `1`: Än Animazion uism FSQ-Feil wird abgschpiut. Weli genai
		///    definiärt d `Duration`. Drbii wird wiä im Modus 0 ai d Palettä gladä.
		///  - `2`: Aui Biudr wo chemid wärdid mit dä Zweibit-Datä uism FSQ-Feil
		///    erwiitered. D Idee isch dass uis Zwäibit-Datä Viärbit-Datä wärdid.
		///    D Palettä wird wiä obä ai gladä.
		/// </summary>
		public readonly int Mode;

		/// <summary>
		/// Dr Palettäindex
		/// </summary>
		public readonly ushort PaletteIndex;

		/// <summary>
		/// Im Modus 0 ischs wiä lang's gaht bis mr zrugg zur Standard-Palettä wächslet (wenn 0 gar nid zrugg wächslä).
		/// Im Modus eis odr zwäi ischs d Byte-Position vodr Animazion im FSQ-Feil.
		/// </summary>
		public readonly uint Duration;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Mapping(BinaryReader reader)
		{
			Checksum = reader.ReadUInt32BE();
			//Logger.Trace("  [{1}] [palette] Read checksum as {0}", Checksum, reader.BaseStream.Position);
			Mode = reader.ReadByte();
			//Logger.Trace("  [{1}] [palette] Read mode as {0}", Mode, reader.BaseStream.Position);
			PaletteIndex = reader.ReadUInt16BE();
			//Logger.Trace("  [{1}] [palette] Read index as {0}", PaletteIndex, reader.BaseStream.Position);
			Duration = reader.ReadUInt32BE();
			//Logger.Trace("  [{1}] [palette] Read duration as {0}", Duration, reader.BaseStream.Position);
		}
	}
}
