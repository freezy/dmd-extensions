using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace Console.Test
{
	class TestOptions
	{
		[ParserState]
		public IParserState LastParserState { get; set; }
	}
}
