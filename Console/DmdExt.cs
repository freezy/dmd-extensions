using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Console.Common;
using Console.Mirror;
using Console.Test;
using NLog;

namespace Console
{
	class DmdExt
	{
		public static Application WinApp { get; } = new Application();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[STAThread]
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

			BaseCommand command;
			switch (invokedVerb) {
				case "mirror":
					command = new MirrorCommand((MirrorOptions)invokedVerbInstance);
					break;

				case "test":
					command = new TestCommand((TestOptions)invokedVerbInstance);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			try {
				command.Execute();
				Logger.Info("Press CTRL+C to close.");
				WinApp.Run();

			} catch (DeviceNotAvailableException e) {
				Logger.Error("Device {0} is not available.", e.Message);

			} catch (NoRenderersAvailableException) {
				Logger.Error("No output devices available.");

			} catch (InvalidOptionException e) {
				Logger.Error("Invalid option: {0}", e.Message);

			} finally {
				Environment.Exit(CommandLine.Parser.DefaultExitCodeFail);
			}

		}
	}
}
