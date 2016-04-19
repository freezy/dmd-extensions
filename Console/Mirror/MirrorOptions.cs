using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace Console.Mirror
{
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
		Auto, PinDMDv1, PinDMDv2, PinDMDv3, PIN2DMD, VirtualDmd
	}
}
