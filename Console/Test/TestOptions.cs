using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Console.Common;

namespace Console.Test
{
	class TestOptions : BaseOptions
	{
		[ParserState]
		public IParserState LastParserState { get; set; }
	}
}
