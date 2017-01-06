using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using DmdExt.Common;
using DmdExt.Mirror;
using DmdExt.Play;
using DmdExt.Test;
using LibDmd;
using LibDmd.Input.FileSystem;
using LibDmd.Input.ProPinball;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using Microsoft.Win32;
using Mindscape.Raygun4Net;
using NLog;

namespace DmdExt
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
			// setup log config
			var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
			var logConfigPath = Path.Combine(assemblyPath, "dmdext.log.config");
			if (File.Exists(logConfigPath)) {
				LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(logConfigPath, true);
			}

			AssertDotNetVersion();
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

			BaseOptions baseOptions;
			switch (invokedVerb) {
				case "mirror":
					baseOptions = (BaseOptions)invokedVerbInstance;
					_command = new MirrorCommand((MirrorOptions)baseOptions);
					break;

				case "play":
					baseOptions = (PlayOptions)invokedVerbInstance;
					_command = new PlayCommand((PlayOptions)baseOptions);
					break;

				case "test":
					baseOptions = (BaseOptions)invokedVerbInstance;
					_command = new TestCommand((TestOptions)baseOptions);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			try {
				var renderer = _command.GetRenderGraph();

				if (baseOptions.SaveToFile != null) {
					(renderer as RenderGraph)?.Destinations.Add(new BitmapOutput(baseOptions.SaveToFile));
				}

				_command.Execute(() => {
					if (baseOptions != null && baseOptions.QuitWhenDone) {
						Logger.Info("Exiting.");
						_command?.Dispose();
						Environment.Exit(0);
					}
				}, ex => {
					Logger.Error("Error: {0}", ex.Message);
					_command?.Dispose();
					Environment.Exit(0);
				});
				Logger.Info("Press CTRL+C to close.");
				WinApp.Run();

			} catch (DeviceNotAvailableException e) {
				Logger.Error("Device {0} is not available.", e.Message);

			} catch (NoRenderersAvailableException) {
				Logger.Error("No output devices available.");

			} catch (InvalidOptionException e) {
				Logger.Error("Invalid option: {0}", e.Message);

			} catch (FileNotFoundException e) {
				Logger.Error(e.Message);
				Logger.Info("Try installing the Visual C++ Redistributable for Visual Studio 2015 if you haven't so already:");
				Logger.Info("    https://www.microsoft.com/en-us/download/details.aspx?id=48145");

			} catch (UnknownFormatException e) {
				Logger.Error(e.Message);

			} catch (WrongFormatException e) {
				Logger.Error(e.Message);

			} catch (UnsupportedResolutionException e) {
				Logger.Error(e.Message);

			} catch (InvalidFolderException e) {
				Logger.Error(e.Message);

			} catch (RenderException e) {
				Logger.Error(e.Message);

			} catch (NoRawDestinationException e) {
				Logger.Error(e.Message);

			} catch (MultipleRawSourcesException e) {
				Logger.Error(e.Message);

			} catch (ProPinballSlaveException e) {
				Logger.Error(e.Message);

			} catch (IncompatibleRenderer e) {
				Logger.Error(e.Message);

			} catch (IncompatibleSourceException e) {
				Logger.Error(e.Message);

			} finally {
				Process.GetCurrentProcess().Kill();
			}
		}

		private static void AssertDotNetVersion()
		{
			using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\")) {
				var releaseKey = Convert.ToInt32(ndpKey?.GetValue("Release"));
				if (releaseKey < 379893) {
					System.Console.WriteLine("You need to install at least v4.5.2 of the .NET framework.");
					System.Console.WriteLine("Download from here: https://www.microsoft.com/en-us/download/details.aspx?id=42642");
					Environment.Exit(CommandLine.Parser.DefaultExitCodeFail);
				}
			}
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
#if !DEBUG
			Raygun.Send(e.ExceptionObject as Exception);
#endif
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
