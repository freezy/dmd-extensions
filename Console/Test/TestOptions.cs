using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using DmdExt.Common;
using LibDmd;

namespace DmdExt.Test
{
	class TestOptions : BaseOptions
	{

		[Option("format", HelpText = "Try to output in that frame format. One of: [ rgb24, gray2, gray4, coloredgray2, coloredgray4 ].")]
		public FrameFormat FrameFormat { get; set; } = FrameFormat.Bitmap;

		[ParserState]
		public IParserState LastParserState { get; set; }
	}

}
