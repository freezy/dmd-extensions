using CommandLine;
using DmdExt.Common;

namespace DmdExt.Play
{
	class PlayOptions : BaseOptions
	{
		[Option('f', "file", Required = true, HelpText = "Path to the file to play. Currently supported file types: PNG, JPG, BIN (raw).")]
		public string FileName { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }
	}
}