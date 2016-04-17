using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace Console
{
	class Options
	{
		[Option('s', "source", HelpText = "The source you want to retrieve DMD data from.")]
		public SourceType Source { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }

		[HelpOption]
		public string GetUsage()
		{
			var help = new HelpText {
				Heading = new HeadingInfo("dmdext", "1.0"),
				Copyright = new CopyrightInfo("freezy", 2015),
				AdditionalNewLineAfterOption = true,
				AddDashesToOption = true
			};
			help.AddPreOptionsLine("Usage: dmdext --source=<source>");
			help.AddOptions(this);


			if (LastParserState.Errors.Any()) {
				var errors = help.RenderParsingErrorsText(this, 2); // indent with two spaces

				if (!string.IsNullOrEmpty(errors)) {
					help.AddPreOptionsLine(string.Concat(Environment.NewLine, "ERROR(S):"));
					help.AddPreOptionsLine(errors);
				}
			}
			return help;
		}

	}

	enum SourceType
	{
		PinballFX2,
		Screen
	}
}
