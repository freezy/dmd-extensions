using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace Console
{
	class Options
	{
		[VerbOption("mirror", HelpText = "Mirrors pixel data from the screen to one or more other destinations.")]
		public MirrorOptions Mirror { get; set; }

		public Options()
		{
			Mirror = new MirrorOptions();
		}


		[HelpVerbOption]
		public string GetUsage(string verb)
		{
			switch (verb) {
				case "mirror":
					return AutoBuild(Mirror, "dmdext mirror --source=<source> [--destination=<destination>]", Mirror.LastParserState);
				default:
					return AutoBuild(this, "dmdext <command> [<options>]");
			}
		}

		public static HelpText AutoBuild(object options, string usage, IParserState parserState = null)
		{
			var title = (AssemblyTitleAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false).FirstOrDefault();
			var version = (AssemblyInformationalVersionAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).FirstOrDefault();
			var copyright = (AssemblyCopyrightAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false).FirstOrDefault();
			var license = (AssemblyLicenseAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyLicenseAttribute), false).FirstOrDefault();

			var help = new HelpText {
				AdditionalNewLineAfterOption = true,
				AddDashesToOption = true,
			};

			help.AddPreOptionsLine($"{title?.Title} v{version?.InformationalVersion}");
			help.AddPreOptionsLine(copyright?.Copyright);
			help.AddPreOptionsLine(license?.Value);
			help.AddPreOptionsLine($"USAGE: {usage}");

			help.AddOptions(options);

			if (parserState != null && parserState.Errors.Any()) {
				var errors = help.RenderParsingErrorsText(options, 2); // indent with two spaces

				if (!string.IsNullOrEmpty(errors)) {
					help.AddPostOptionsLine("ERROR:");
					help.AddPostOptionsLine(errors);
				}
			}
			return help;
		}
	}

	class MirrorOptions
	{
		[Option('s', "source", Required = true, HelpText = "The source you want to retrieve DMD data from. One of: [ pinballfx2, screen ].")]
		public SourceType Source { get; set; }

		[Option('d', "destination", HelpText = "The destination where the DMD data is sent to. One of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd ]. Default: \"auto\".")]
		public DestinationType Destination { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }
	}

	enum SourceType
	{
		PinballFX2,
		Screen
	}

	enum DestinationType
	{
		Auto, PinDMDv1, PinDMDv2, PinDMDv3, PIN2DMD
	}
}
