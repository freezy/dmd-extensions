using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Console
{
	class Program
	{
		static void Main(string[] args)
		{
			var options = new Options();
			if (CommandLine.Parser.Default.ParseArguments(args, options))
			{

				System.Console.WriteLine("Options okay!");
			}
		}
	}
}
