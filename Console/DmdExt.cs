using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Console.Common;
using Console.Mirror;
using Console.Test;

namespace Console
{
	class DmdExt
	{
		static void Main(string[] args)
		{
			var invokedVerb = "";
			object invokedVerbInstance = null;
			var options = new Options();
			if (!CommandLine.Parser.Default.ParseArgumentsStrict(args, options, (verb, subOptions) => {

				// if parsing succeeds the verb name and correct instance
				// will be passed to onVerbCommand delegate (string,object)
				invokedVerb = verb;
				invokedVerbInstance = subOptions;
			})) {
				Environment.Exit(CommandLine.Parser.DefaultExitCodeFail);
			}

			ICommand command;
			switch (invokedVerb) {
				case "mirror":
					command = new MirrorCommand((MirrorOptions)invokedVerbInstance);
					break;
				default:
					command = new TestCommand((TestOptions) invokedVerbInstance);
					break;

			}
			command.Execute();
		}
	}
}
