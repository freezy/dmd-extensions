using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using DmdExt.Common;
using DmdExt.Mirror;
using DmdExt.Play;
using DmdExt.Test;
using LibDmd;
using LibDmd.Input.FileSystem;
using LibDmd.Input.PinballFX;
using LibDmd.Input.ProPinball;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Output.PinUp;
using Microsoft.Win32;
using Mindscape.Raygun4Net;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace DmdExt
{
	class DmdExt
	{
		public static Application WinApp { get; } = new Application();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static BaseCommand _command;
		private static EventHandler _handler;
		private static readonly MemoryTarget MemLogger = new MemoryTarget {
			Name = "Raygun Logger",
			Layout = "${pad:padding=4:inner=[${threadid}]} ${date} ${pad:padding=5:inner=${level:uppercase=true}} | ${message} ${exception:format=ToString}"
		};
		private static string[] _commandLineArgs;

		[STAThread]
		static void Main(string[] args)
		{
			_commandLineArgs = args;

			// setup logger
			var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
			var logConfigPath = Path.Combine(assemblyPath, "dmdext.log.config");
			if (File.Exists(logConfigPath)) {
				LogManager.Configuration = new XmlLoggingConfiguration(logConfigPath, true);
#if !DEBUG
				LogManager.Configuration.AddTarget("memory", MemLogger);
				LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, MemLogger));
				LogManager.ReconfigExistingLoggers();
#endif
			} 
#if !DEBUG			
			else {
				SimpleConfigurator.ConfigureForTargetLogging(MemLogger, LogLevel.Debug);
			}
#endif
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

			Logger.Info("Launching console tool.");
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

				if (baseOptions.PinUp != null) {
					(renderer as RenderGraph)?.Destinations.Add(new PinUpOutput(baseOptions.PinUp));
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

				if (baseOptions.QuitAfter > -1) {
					Logger.Info("Quitting in {0}ms...", baseOptions.QuitAfter);
					Observable
						.Return(Unit.Default)
						.Delay(TimeSpan.FromMilliseconds(baseOptions.QuitAfter))
						.Subscribe(_ => WinApp.Dispatcher.Invoke(() => WinApp.Shutdown()));

				} else {
					Logger.Info("Press CTRL+C to close.");
				}

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

			} catch (CropRectangleOutOfRangeException e) {
				Logger.Error(e.Message);
				Logger.Error("Are you running PinballFX2 in cabinet mode with the DMD at 1040x272?");

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
			var ex = e.ExceptionObject as Exception;
			if (ex != null) {
				Logger.Error(ex.Message);
				Logger.Error(ex.ToString());
			}
#if !DEBUG
			Raygun.ApplicationVersion = LibDmd.Version.AssemblyInformationalVersionAttribute;
			Raygun.Send(ex, null, new Dictionary<string, string> { {"args", string.Join(" ", _commandLineArgs) }, {"log", string.Join("\n", MemLogger.Logs) } });
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
