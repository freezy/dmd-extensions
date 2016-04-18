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
			var invokedVerb = "";
			object invokedVerbInstance;
			var options = new Options();
			if (!CommandLine.Parser.Default.ParseArgumentsStrict(args, options, (verb, subOptions) => {

				// if parsing succeeds the verb name and correct instance
				// will be passed to onVerbCommand delegate (string,object)
				invokedVerb = verb;
				invokedVerbInstance = subOptions;

				System.Console.WriteLine("Ok sub-parsing succeeded ({0})!", verb);

			})) {
				System.Console.WriteLine("Parsing failed, exiting.");
				//System.Console.WriteLine(options.GetUsage());

				Environment.Exit(CommandLine.Parser.DefaultExitCodeFail);
			}

			System.Console.WriteLine("Ok sub-parsing succeeded and we're back out: {0}", options.Mirror);
//			if (invokedVerb == "commit") {
//				var commitSubOptions = (CommitSubOptions)invokedVerbInstance;
//			}


		}
	}
}
