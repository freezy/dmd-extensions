using System;
using System.Linq;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using Console.Mirror;
using Console.Play;
using Console.Test;

namespace Console.Common
{
	class Options
	{
		[VerbOption("mirror", HelpText = "Mirrors pixel data from the screen or memory to all available devices.")]
		public MirrorOptions Mirror { get; set; }

		[VerbOption("play", HelpText = "Plays any media on all available devices (currently only images).")]
		public PlayOptions Play { get; set; }

		[VerbOption("test", HelpText = "Displays a test image on all available devices.")]
		public TestOptions Test { get; set; }

		public Options()
		{
			Mirror = new MirrorOptions();
			Play = new PlayOptions();
			Test = new TestOptions();
		}

		[HelpVerbOption]
		public string GetUsage(string verb)
		{
			switch (verb) {
				case "mirror":
					return AutoBuild(Mirror, "dmdext mirror --source=<source> [--destination=<destination>]", Mirror.LastParserState);
				case "play":
					return AutoBuild(Play, "dmdext play --file=<image path> [--destination=<destination>]", Play.LastParserState);
				case "test":
					return AutoBuild(Test, "dmdext test [--destination=<destination>]", Test.LastParserState);
				default:
					return AutoBuild(this, "dmdext <command> [<options>]", null, false);
			}
		}

		public static HelpText AutoBuild(object options, string usage, IParserState parserState = null, bool addDashesToOption = true)
		{
			var title = (AssemblyTitleAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false).FirstOrDefault();
			var version = (AssemblyInformationalVersionAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).FirstOrDefault();
			var copyright = (AssemblyCopyrightAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false).FirstOrDefault();
			var license = (AssemblyLicenseAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyLicenseAttribute), false).FirstOrDefault();

			var help = new HelpText {
				AdditionalNewLineAfterOption = true,
				AddDashesToOption = addDashesToOption
			};

			help.AddPreOptionsLine($"{title?.Title} v{version?.InformationalVersion}");
			help.AddPreOptionsLine(copyright?.Copyright);
			help.AddPreOptionsLine(license?.Value);
			help.AddPreOptionsLine($"USAGE: {usage}");

			help.AddOptions(options);

			if (parserState != null && parserState.Errors.Any()) {
				var errors = help.RenderParsingErrorsText(options, 2); // indent with two spaces

				if (!string.IsNullOrEmpty(errors)) {
					help.AddPostOptionsLine("ERROR:\n");
					help.AddPostOptionsLine(errors);
				}
			}
			return help;
		}
	}

	public class InvalidOptionException : Exception
	{
		public InvalidOptionException(string message) : base(message)
		{
		}
	}
}
