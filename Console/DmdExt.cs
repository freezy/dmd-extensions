using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using CommandLine;
using DmdExt.Common;
using DmdExt.Mirror;
using DmdExt.Play;
using DmdExt.Server;
using DmdExt.Test;
using LibDmd;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Input.FileSystem;
using LibDmd.Input.PinballFX;
using LibDmd.Input.ProPinball;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Output.PinUp;
using Microsoft.Win32;
using NLog;
using NLog.Config;

namespace DmdExt
{
	internal class DmdExt
	{
		public static Application WinApp { get; } = new Application();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
#if !DEBUG
		static readonly Mindscape.Raygun4Net.RaygunClient Raygun = new Mindscape.Raygun4Net.RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static readonly NLog.Targets.MemoryTarget MemLogger = new NLog.Targets.MemoryTarget {
			Name = "Raygun Logger",
			Layout = "${pad:padding=4:inner=[${threadid}]} ${date} ${pad:padding=5:inner=${level:uppercase=true}} | ${message} ${exception:format=ToString}"
		};
#endif
		private static BaseCommand _command;
		private static EventHandler _handler;
		private static string[] _commandLineArgs;
		private static string _version;
		private static string _sha;
		private static string _fullVersion;

		private static readonly HashSet<string> ReportingTags = new HashSet<string>();

		[STAThread]
		static void Main(string[] args)
		{
			var assembly = Assembly.GetExecutingAssembly();
			PathUtil.GetAssemblyVersion(out _fullVersion, out _sha);
			CultureUtil.NormalizeUICulture();
			_commandLineArgs = FixIniArg(args);
			ReportingTags.Add("Console");
#if PLATFORM_X86
			ReportingTags.Add("x86");
#elif PLATFORM_X64
			ReportingTags.Add("x64");
#endif

			// setup logger
			var assemblyPath = Path.GetDirectoryName(new Uri(assembly.CodeBase).LocalPath);
			var logConfigPath = Path.Combine(assemblyPath, "dmdext.log.config");
			if (File.Exists(logConfigPath)) {
				LogManager.ThrowConfigExceptions = true;
				LogManager.Configuration = new XmlLoggingConfiguration(logConfigPath);
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
			if (!Parser.Default.ParseArgumentsStrict(_commandLineArgs, options, (verb, subOptions) => {

				// if parsing succeeds the verb name and correct instance
				// will be passed to onVerbCommand delegate (string,object)
				invokedVerb = verb;
				invokedVerbInstance = subOptions;
			})) {
				Environment.Exit(Parser.DefaultExitCodeFail);
			}

			try {

				Logger.Info("Launching console tool v{0}", _fullVersion);
				options.Validate();
				var cmdLineOptions = (BaseOptions)invokedVerbInstance;
				var config = cmdLineOptions.DmdDeviceIni == null
					? (IConfiguration)cmdLineOptions
					: new Configuration(cmdLineOptions.DmdDeviceIni) { GameName = cmdLineOptions.GameName };

				try {
					Analytics.Instance.Init(_fullVersion, "EXE");
				} catch (Exception e) {
					ReportError(e);
					Analytics.Instance.Disable(false);
				}

				//BaseOptions baseOptions;
				switch (invokedVerb) {
					case "mirror":
						_command = new MirrorCommand(config, (MirrorOptions)cmdLineOptions);
						break;

					case "play":
						_command = new PlayCommand(config, (PlayOptions)cmdLineOptions);
						break;

					case "test":
						_command = new TestCommand(config, (TestOptions)cmdLineOptions);
						break;

					case "server":
						_command = new ServerCommand(config, (ServerOptions)cmdLineOptions);
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}

				var renderGraphs = _command.GetRenderGraphs(ReportingTags);

				if (config.Bitmap.Enabled) {
					renderGraphs.AddDestination(new BitmapOutput(config.Bitmap.Path));
				}

				if (config.PinUp.Enabled) {
					try {
						renderGraphs.AddDestination(new PinUpOutput(config.PinUp.GameName));

					} catch (Exception e) {
						Logger.Warn("Error opening PinUP output: {0}", e.Message);
					}
				}

				_command.Execute(ReportingTags, () => {
					if (config != null && config.Global.QuitWhenDone) {
						Logger.Info("Exiting.");
						_command?.Dispose();
						Environment.Exit(0);
					}
				}, ex => {
					Logger.Error("Error: {0}", ex.Message);
					_command?.Dispose();
					Environment.Exit(0);
				});

				if (config.Global.QuitAfter > -1) {
					Logger.Info("Quitting in {0}ms...", config.Global.QuitAfter);
					Observable
						.Return(Unit.Default)
						.Delay(TimeSpan.FromMilliseconds(config.Global.QuitAfter))
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

			} catch (IniNotFoundException e) {
				Logger.Error(e.Message);

			} catch (CropRectangleOutOfRangeException e) {
				Logger.Error(e.Message);
				Logger.Error("Are you running PinballFX2 in cabinet mode with the DMD at 1040x272?");

			} finally {
				Process.GetCurrentProcess().Kill();
			}
		}

		/// <summary>
		/// Checks if the --use-ini argument is present and if it is but without a path,
		/// checks the DMDDEVICE_CONFIG environment variable and adds it to the argument,
		/// if present.
		/// </summary>
		/// <param name="args">Command line args</param>
		/// <returns>Update command line args</returns>
		private static string[] FixIniArg(string[] args)
		{
			for (var i = 0; i < args.Length; i++) {
				if (args[i] != "--use-ini") {
					continue;
				}
				var envConfigPath = Configuration.GetEnvConfigPath();
				if (args.Length > i + 1) { // value argument following?
					if (!args[i + 1].ToLowerInvariant().Contains("dmddevice.ini")) {
						if (envConfigPath != null) {
							args[i] = $"--use-ini={envConfigPath}";
						}
					}
				} else {
					if (envConfigPath != null) {
						args[i] = $"--use-ini={envConfigPath}";
					}
				}
			}
			return args;
		}

		private static void AssertDotNetVersion()
		{
			using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\")) {
				var releaseKey = Convert.ToInt32(ndpKey?.GetValue("Release"));
				if (releaseKey < 461808) {
					Console.WriteLine("You need to install at least v4.7.2 of the .NET framework.");
					Console.WriteLine("Download from here: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472");
					Environment.Exit(Parser.DefaultExitCodeFail);
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

			ReportError(ex);
		}

		private static void ReportError(Exception ex)
		{
#if !DEBUG
			Raygun.ApplicationVersion = _fullVersion;
			Raygun.Send(ex, System.Linq.Enumerable.ToList(ReportingTags), new Dictionary<string, string> {
				{ "args", string.Join(" ", _commandLineArgs) },
				{ "log", string.Join("\n", MemLogger.Logs) },
				{ "sha", _sha }
			});
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
