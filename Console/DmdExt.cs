using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Console.Common;
using Console.Mirror;
using Console.Test;
using Mindscape.Raygun4Net;
using NLog;

namespace Console
{
	class DmdExt
	{
		public static Application WinApp { get; } = new Application();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static BaseCommand _command;
		private static EventHandler _handler;

		[STAThread]
		static void Main(string[] args)
		{

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			// enable exit handler
			_handler += ExitHandler;
			SetConsoleCtrlHandler(_handler, true);

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

			switch (invokedVerb) {
				case "mirror":
					_command = new MirrorCommand((MirrorOptions)invokedVerbInstance);
					break;

				case "test":
					_command = new TestCommand((TestOptions)invokedVerbInstance);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			try {
				_command.Execute();
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

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Raygun.Send(e.ExceptionObject as Exception);
		}

		#region Exit Handling

		[DllImport("Kernel32")]
		private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

		private delegate bool EventHandler(CtrlType sig);

		private static bool ExitHandler(CtrlType sig)
		{
			switch (sig) {
				case CtrlType.CTRL_C_EVENT:
				case CtrlType.CTRL_LOGOFF_EVENT:
				case CtrlType.CTRL_SHUTDOWN_EVENT:
				case CtrlType.CTRL_CLOSE_EVENT:
				default:
					_command?.Dispose();
					return false;
			}
		}

		enum CtrlType
		{
			CTRL_C_EVENT = 0,
			CTRL_BREAK_EVENT = 1,
			CTRL_CLOSE_EVENT = 2,
			CTRL_LOGOFF_EVENT = 5,
			CTRL_SHUTDOWN_EVENT = 6
		}

		#endregion

	}
}
