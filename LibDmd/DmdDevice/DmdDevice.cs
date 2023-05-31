// ReSharper disable CommentTypo

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using LibDmd.Common;
using LibDmd.Converter.Pin2Color;
using LibDmd.Converter.Plugin;
using LibDmd.Converter.Serum;
using LibDmd.Frame;
using LibDmd.Input.Passthrough;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Output.Network;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Output.PinUp;
using LibDmd.Output.Pixelcade;
using LibDmd.Output.Virtual.AlphaNumeric;
using LibDmd.Output.ZeDMD;
using Microsoft.Win32;
using NLog;
using NLog.Config;

namespace LibDmd.DmdDevice
{
	/// <summary>
	/// The main logic of <c>DmdDevice.dll</c>.
	/// </summary>
	/// <seealso cref="LibDmd.DmdDevice">The DLL wrapper where the data comes from.</seealso>
	public class DmdDevice : IDmdDevice
	{
		// generic stuff
		private readonly IConfiguration _config;
		private readonly RenderGraphCollection _graphs = new RenderGraphCollection();
		private bool _isOpen;
		private Thread _dmdThread;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// sources
		private readonly PassthroughGray2Source _passthroughGray2Source;
		private readonly PassthroughGray4Source _passthroughGray4Source;
		private readonly PassthroughRgb24Source _passthroughRgb24Source;
		private readonly PassthroughAlphaNumericSource _passthroughAlphaNumericSource;

		// destinations
		private VirtualDmd _virtualDmd;
		private VirtualAlphanumericDestination _alphaNumericDest;

		// versioning
		private static string _sha = "";
		private static string _fullVersion = "";

		// data coming from the DLL
		private string _gameName;
		private bool _colorize;
		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;
		private readonly DmdFrame _alphanumFrame = new DmdFrame(new Dimensions(128, 32), 2);

		// colorizers
		private readonly ColorizationLoader _colorizationLoader;
		private Serum _serum;
		private ColorizationPlugin _colorizationPlugin;
		private Pin2ColorResult _pin2ColorResult;

		// error reporting
#if !DEBUG
		static readonly Mindscape.Raygun4Net.RaygunClient Raygun = new Mindscape.Raygun4Net.RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static readonly NLog.Targets.MemoryTarget MemLogger = new NLog.Targets.MemoryTarget {
			Name = "Raygun Logger",
			Layout = "${pad:padding=4:inner=[${threadid}]} ${date} ${pad:padding=5:inner=${level:uppercase=true}} | ${message} ${exception:format=ToString}"
		};
#endif
		private static readonly string AssemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
		private static readonly HashSet<string> ReportingTags = new HashSet<string>();

		public DmdDevice(IConfiguration config = null)
		{
			var currentFrameFormat = new BehaviorSubject<FrameFormat>(FrameFormat.Rgb24);
			_passthroughGray2Source = new PassthroughGray2Source(currentFrameFormat, "DmdDevice 2-bit Source");
			_passthroughGray4Source = new PassthroughGray4Source(currentFrameFormat, "DmdDevice 4-bit Source");
			_passthroughRgb24Source = new PassthroughRgb24Source(currentFrameFormat, "DmdDevice RGB24 Source");
			_passthroughAlphaNumericSource = new PassthroughAlphaNumericSource(currentFrameFormat);

			_config = config ?? new Configuration();
			_colorizationLoader = new ColorizationLoader();

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			#region Logger
			// setup logger
			var assembly = Assembly.GetCallingAssembly();
			var assemblyPath = Path.GetDirectoryName(new Uri(assembly.CodeBase).LocalPath);
			if (assemblyPath != null) {
				var logConfigPath = Path.Combine(assemblyPath, "DmdDevice.log.config");
				if (File.Exists(logConfigPath)) {
					LogManager.Configuration = new XmlLoggingConfiguration(logConfigPath);
					LogManager.ThrowExceptions = false;
#if !DEBUG
					LogManager.Configuration.AddTarget("memory", MemLogger);
					LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, MemLogger));
					LogManager.ReconfigExistingLoggers();
#endif
				}
#if !DEBUG
				else {
					SimpleConfigurator.ConfigureForTargetLogging(MemLogger, LogLevel.Debug);
				}
#endif
			}
			CultureUtil.NormalizeUICulture();

			// read versions from assembly
			var attr = assembly.GetCustomAttributes(typeof(AssemblyConfigurationAttribute), false);
			var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
			var version = fvi.ProductVersion;
			if (attr.Length > 0) {
				var aca = (AssemblyConfigurationAttribute)attr[0];
				_sha = aca.Configuration;
				_fullVersion = string.IsNullOrEmpty(_sha) ? version : $"{version} ({_sha})";

			} else {
				_fullVersion = fvi.ProductVersion;
				_sha = "";
			}
			#endregion

			if (_config.Global.SkipAnalytics) {
				Analytics.Instance.Disable();
			}

			try {
				Analytics.Instance.Init(_fullVersion, "DLL");
				
			} catch (Exception e) {
				ReportError(e);
				Analytics.Instance.Disable(false);
			}

			Logger.Info("Starting VPinMAME API {0} through {1}.exe.", _fullVersion, Process.GetCurrentProcess().ProcessName);
			Logger.Info("Assembly located at {0}", assembly.Location);
			Logger.Info("Running in {0}", Directory.GetCurrentDirectory());
		}

		#region DmdDevice.dll API

		/// <summary>
		/// Wird uisgfiärt wemmr aui Parametr hend.
		/// </summary>
		/// <remarks>
		/// Es cha si dass das meh as einisch uisgfiärt wird, wobih's DMD numä
		/// einisch ersteuht wird.
		/// </remarks>
		public void Init()
		{
			if (_isOpen) {
				return;
			}

			_serum = null;
			_pin2ColorResult = null;
			_colorizationPlugin = null;

			SetupColorizer();

			if (_config.VirtualDmd.Enabled || _config.VirtualAlphaNumericDisplay.Enabled) {
				if (_virtualDmd == null && _alphaNumericDest == null) {
					Logger.Info("Opening virtual display...");
					CreateVirtualDmd();
				}
				else if (_config.VirtualDmd.Enabled) {
					try {
						_virtualDmd?.Dispatcher.Invoke(() => {
							SetupGraphs();
							SetupVirtualDmd();
						});

					}
					catch (TaskCanceledException e) {
						Logger.Error(e, "Main thread seems already destroyed, aborting.");
					}
				}
			} else {
				SetupGraphs();
			}
			_isOpen = true;
		}

		/// <summary>
		/// Called if a modified ROM emits a palette change.
		/// </summary>
		/// <param name="num">Index of the new palette.</param>
		public void LoadPalette(uint num)
		{
			_pin2ColorResult?.Gray4Colorizer?.LoadPalette(num);
		}

		/// <summary>
		/// Pass console data through to plugin
		/// </summary>
		/// <param name="data"></param>
		public void ConsoleData(byte data)
		{
			_colorizationPlugin?.ConsoleData(data);
		}

		public void SetGameName(string gameName)
		{
			AnalyticsClear();

			if (_gameName != null) { // only reload if game name is set (i.e. we didn't just load because we just started)
				_config.Reload();
			}

			Logger.Info("Setting game name: {0}", gameName);
			_gameName = gameName;
			_config.SetGameName(gameName);
			Analytics.Instance.SetSource(Process.GetCurrentProcess().ProcessName, gameName);
		}

		/// <summary>
		/// Triggers frame colorization through the DLL.
		/// </summary>
		/// <param name="colorize">True if colorization enable in the ROM, false otherwise.</param>
		public void SetColorize(bool colorize)
		{
			Logger.Info("{0} game colorization", colorize ? "Enabling" : "Disabling");
			_colorize = colorize;
		}

		/// <summary>
		/// Sets the color through the DLL.
		/// </summary>
		/// <param name="color">New color</param>
		public void SetColor(Color color)
		{
			Logger.Info("Setting color: {0}", color);
			_color = color;
		}

		/// <summary>
		/// Sets the palette through the DLL.
		/// </summary>
		/// <param name="colors">New palette</param>
		public void SetPalette(Color[] colors)
		{
			Logger.Info("Setting palette to {0} colors...", colors.Length);
			_palette = colors;
		}

		/// <summary>
		/// A new gray2 frame coming in from the DLL.
		/// </summary>
		/// <param name="frame">New gray frame</param>
		public void RenderGray2(DmdFrame frame)
		{
			AnalyticsSetDmd();
			if (!_isOpen) {
				Init();
			}
			_passthroughGray2Source.NextFrame(frame);
		}

		/// <summary>
		/// A new gray4 frame coming in from the DLL.
		/// </summary>
		/// <param name="frame">New gray4 frame</param>
		public void RenderGray4(DmdFrame frame)
		{
			AnalyticsSetDmd();
			if (!_isOpen) {
				Init();
			}
			_passthroughGray4Source.NextFrame(frame);
		}

		/// <summary>
		/// A new RGB24 frame coming in from the DLL.
		/// </summary>
		/// <param name="frame">New RGB24 frame</param>
		public void RenderRgb24(DmdFrame frame)
		{
			AnalyticsSetDmd();
			if (!_isOpen) {
				Init();
			}
			_passthroughRgb24Source.NextFrame(frame);
		}

		/// <summary>
		/// A new segdisplay frame coming in from the DLL.
		/// </summary>
		/// <param name="layout">Segment layout</param>
		/// <param name="segData">Segment data</param>
		/// <param name="segDataExtended">Additional segment data</param>
		/// <exception cref="ArgumentOutOfRangeException">On unknown segment layout</exception>
		public void RenderAlphaNumeric(NumericalLayout layout, ushort[] segData, ushort[] segDataExtended)
		{
			AnalyticsSetSegmentDisplay();
			if (_gameName.StartsWith("spagb_")) {
				// ignore GB frames, looks like a bug from SPA side
				return;
			}

			if (!_isOpen) {
				Init();
			}

			if (_gameName.StartsWith("rvrbt_")) {
				layout = NumericalLayout.__1x16Alpha_1x16Num_1x7Num_1x4Num;
			}
			if (_gameName.StartsWith("polic_")) {
				layout = NumericalLayout.__1x7Num_1x16Alpha_1x16Num;
			}

			_passthroughAlphaNumericSource.NextFrame(new AlphaNumericFrame(layout, segData, segDataExtended));

			//Logger.Info("Alphanumeric: {0}", layout);
			switch (layout) {
				case NumericalLayout.__2x16Alpha:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x16Alpha(segData), 2));
					break;
				case NumericalLayout.None:
				case NumericalLayout.__2x20Alpha:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x20Alpha(segData), 2));
					break;
				case NumericalLayout.__2x7Alpha_2x7Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x7Alpha_2x7Num(segData), 2));
					break;
				case NumericalLayout.__2x7Alpha_2x7Num_4x1Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x7Alpha_2x7Num_4x1Num(segData), 2));
					break;
				case NumericalLayout.__2x7Num_2x7Num_4x1Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x7Num_2x7Num_4x1Num(segData), 2));
					break;
				case NumericalLayout.__2x7Num_2x7Num_10x1Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x7Num_2x7Num_10x1Num(segData, segDataExtended), 2));
					break;
				case NumericalLayout.__2x7Num_2x7Num_4x1Num_gen7:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x7Num_2x7Num_4x1Num_gen7(segData), 2));
					break;
				case NumericalLayout.__2x7Num10_2x7Num10_4x1Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x7Num10_2x7Num10_4x1Num(segData), 2));
					break;
				case NumericalLayout.__2x6Num_2x6Num_4x1Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x6Num_2x6Num_4x1Num(segData), 2));
					break;
				case NumericalLayout.__2x6Num10_2x6Num10_4x1Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x6Num10_2x6Num10_4x1Num(segData), 2));
					break;
				case NumericalLayout.__4x7Num10:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render4x7Num10(segData), 2));
					break;
				case NumericalLayout.__6x4Num_4x1Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render6x4Num_4x1Num(segData), 2));
					break;
				case NumericalLayout.__2x7Num_4x1Num_1x16Alpha:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render2x7Num_4x1Num_1x16Alpha(segData), 2));
					break;
				case NumericalLayout.__1x16Alpha_1x16Num_1x7Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render1x16Alpha_1x16Num_1x7Num(segData), 2));
					break;
				case NumericalLayout.__1x7Num_1x16Alpha_1x16Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render1x7Num_1x16Alpha_1x16Num(segData), 2));
					break;
				case NumericalLayout.__1x16Alpha_1x16Num_1x7Num_1x4Num:
					_passthroughGray2Source.NextFrame(_alphanumFrame.Update(AlphaNumeric.Render1x16Alpha_1x16Num_1x7Num_1x4Num(segData), 2));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layout), layout, null);
			}
		}

		/// <summary>
		/// Game ended, hide the displays.
		/// </summary>
		public void Close()
		{
			Logger.Info("Closing up.");
			try {
				Analytics.Instance.EndGame();
			} catch (Exception e) {
				Logger.Warn(e, "Could not end game.");
			}
			_graphs.ClearDisplay();
			_graphs.Dispose();
			try {
				_virtualDmd?.Dispatcher?.Invoke(() => _virtualDmd?.Close());
				_virtualDmd = null;

				_dmdThread.Abort();

			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Could not hide DMD because task was already canceled.");

			} catch (ThreadAbortException) {
				// this is expected
			}

			_alphaNumericDest = null;
			_color = RenderGraph.DefaultColor;
			_palette = null;
			_serum?.Dispose();
			_serum = null;
			_colorizationPlugin?.Dispose();
			_colorizationPlugin = null;
			_pin2ColorResult = null;
			_isOpen = false;
		}

		#endregion

		/// <summary>
		/// Kreiärt ä Render-Graph fir jedä Input-Tip und bindet si as
		/// virtueuä DMD.
		/// </summary>
		private void SetupGraphs()
		{
			_graphs.Dispose();

			ReportingTags.Clear();
			ReportingTags.Add(Process.GetCurrentProcess().ProcessName);
#if PLATFORM_X86
			ReportingTags.Add("x86");
#elif PLATFORM_X64
			ReportingTags.Add("x64");
#endif
			ReportingTags.Add("In:DmdDevice");
			ReportingTags.Add("Game:" + _gameName);

			var renderers = new List<IDestination>();
			if (_config.PinDmd1.Enabled) {
				var pinDmd1 = PinDmd1.GetInstance();
				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Logger.Info("Added PinDMDv1 renderer.");
					ReportingTags.Add("Out:PinDMDv1");
					Analytics.Instance.AddDestination(pinDmd1);
				}
			}
			if (_config.PinDmd2.Enabled) {
				var pinDmd2 = PinDmd2.GetInstance();
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Logger.Info("Added PinDMDv2 renderer.");
					ReportingTags.Add("Out:PinDMDv2");
					Analytics.Instance.AddDestination(pinDmd2);
				}
			}
			if (_config.PinDmd3.Enabled) {
				var pinDmd3 = PinDmd3.GetInstance(_config.PinDmd3.Port);
				if (pinDmd3.IsAvailable) {
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
					ReportingTags.Add("Out:PinDMDv3");
					Analytics.Instance.AddDestination(pinDmd3);
				}
			}
			if (_config.ZeDMD.Enabled) {
				var zeDmd = ZeDMD.GetInstance();
				if (zeDmd.IsAvailable) {
					renderers.Add(zeDmd);
					Logger.Info("Added ZeDMD renderer.");
					ReportingTags.Add("Out:ZeDMD");
					Analytics.Instance.AddDestination(zeDmd);
				}
			}
			if (_config.Pin2Dmd.Enabled) {
				var pin2Dmd = Pin2Dmd.GetInstance(_config.Pin2Dmd.Delay);
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");
					ReportingTags.Add("Out:PIN2DMD");
					Analytics.Instance.AddDestination(pin2Dmd);
				}

				var pin2DmdXl = Pin2DmdXl.GetInstance(_config.Pin2Dmd.Delay);
				if (pin2DmdXl.IsAvailable) {
					renderers.Add(pin2DmdXl);
					Logger.Info("Added PIN2DMD XL renderer.");
					ReportingTags.Add("Out:PIN2DMDXL");
					Analytics.Instance.AddDestination(pin2DmdXl);
				}

				var pin2DmdHd = Pin2DmdHd.GetInstance(_config.Pin2Dmd.Delay);
				if (pin2DmdHd.IsAvailable) {
					renderers.Add(pin2DmdHd);
					Logger.Info("Added PIN2DMD HD renderer.");
					ReportingTags.Add("Out:PIN2DMDHD");
					Analytics.Instance.AddDestination(pin2DmdHd);
				}
			}
			if (_config.Pixelcade.Enabled) {
				var pixelcade = Pixelcade.GetInstance(_config.Pixelcade.Port, _config.Pixelcade.ColorMatrix);
				if (pixelcade.IsAvailable) {
					renderers.Add(pixelcade);
					Logger.Info("Added Pixelcade renderer.");
					ReportingTags.Add("Out:Pixelcade");
					Analytics.Instance.AddDestination(pixelcade);
				}
			}
			if (_config.VirtualDmd.Enabled) {
				renderers.Add(_virtualDmd.Dmd);
				Logger.Info("Added VirtualDMD renderer.");
				ReportingTags.Add("Out:VirtualDMD");
			}
			if (_config.VirtualAlphaNumericDisplay.Enabled) {
				_alphaNumericDest = VirtualAlphanumericDestination.GetInstance(Dispatcher.CurrentDispatcher, _config.VirtualAlphaNumericDisplay.Style, _config);
				renderers.Add(_alphaNumericDest);
				Logger.Info("Added virtual alphanumeric renderer.");
				ReportingTags.Add("Out:VirtualAlphaNum");
			}
			if (_config.Video.Enabled) {
				var rootPath = "";
				if (_config.Video.Path.Length == 0 || !Path.IsPathRooted(_config.Video.Path)) {
					rootPath = AssemblyPath;
				}
				if (Directory.Exists(Path.Combine(rootPath, _config.Video.Path))) {
					var video = new VideoOutput(Path.Combine(rootPath, _config.Video.Path, _gameName + ".avi"));
					renderers.Add(video);
					Logger.Info("Added video renderer.");
					ReportingTags.Add("Out:Video");
					Analytics.Instance.AddDestination(video);
				}
				else if (Directory.Exists(Path.GetDirectoryName(Path.Combine(rootPath, _config.Video.Path))) && _config.Video.Path.Length > 4 && _config.Video.Path.EndsWith(".avi")) {
					var video = new VideoOutput(Path.Combine(rootPath, _config.Video.Path));
					renderers.Add(video);
					Logger.Info("Added video renderer.");
					ReportingTags.Add("Out:Video");
					Analytics.Instance.AddDestination(video);
				}
				else {
					Logger.Warn("Ignoring video renderer for non-existing path \"{0}\"", _config.Video.Path);
				}
			}
			if (_config.PinUp.Enabled) {
				try {
					var pinupOutput = new PinUpOutput(_gameName);
					if (pinupOutput.IsAvailable)
					{
						if (_serum != null)
						{
							_serum.SetPinupInstance(pinupOutput);
						}
						renderers.Add(pinupOutput);
						Logger.Info("Added PinUP renderer.");
						ReportingTags.Add("Out:PinUP");
						Analytics.Instance.AddDestination(pinupOutput);
					}
				}
				catch (Exception e) {
					Logger.Warn("Error opening PinUP output: {0}", e.Message);
				}
			}
			if (_config.Gif.Enabled) {
				var rootPath = "";
				var dirPath = Path.GetDirectoryName(_config.Gif.Path);
				if (string.IsNullOrEmpty(dirPath) || !Path.IsPathRooted(_config.Video.Path)) {
					rootPath = AssemblyPath;
				}
				var path = Path.Combine(rootPath, _config.Gif.Path);
				if (Directory.Exists(Path.GetDirectoryName(path))) {
					var gifOutput = new GifOutput(path);
					renderers.Add(gifOutput);
					Logger.Info("Added animated GIF renderer, saving to {0}", path);
					ReportingTags.Add("Out:GIF");
					Analytics.Instance.AddDestination(gifOutput);
				}
				else {
					Logger.Warn("Ignoring animated GIF renderer for non-existing path \"{0}\"", Path.GetDirectoryName(path));
				}
			}
			if (_config.VpdbStream.Enabled) {
				var vpdbStream = new VpdbStream { EndPoint = _config.VpdbStream.EndPoint };
				renderers.Add(vpdbStream);
				Logger.Info("Added VPDB stream renderer.");
				ReportingTags.Add("Out:VpdbStream");
				Analytics.Instance.AddDestination(vpdbStream);
			}
			if (_config.BrowserStream.Enabled) {
				var browserStream = new BrowserStream(_config.BrowserStream.Port, _gameName);
				renderers.Add(browserStream);
				Logger.Info("Added browser stream renderer.");
				ReportingTags.Add("Out:BrowserStream");
				Analytics.Instance.AddDestination(browserStream);
			}
			if (_config.NetworkStream.Enabled) {
				var networkStream = NetworkStream.GetInstance(_config.NetworkStream, _gameName);
				renderers.Add(networkStream);
				Logger.Info("Added network stream renderer.");
				ReportingTags.Add("Out:NetworkStream");
				Analytics.Instance.AddDestination(networkStream);
			}

			if (renderers.Count == 0) {
				Logger.Error("No renderers found, exiting.");
				return;
			}

			Logger.Info("Transformation options: Resize={0}, HFlip={1}, VFlip={2}", _config.Global.Resize, _config.Global.FlipHorizontally, _config.Global.FlipVertically);

			// === SERUM ===
			if (_colorize && _serum != null) {

				// 4-bit graph
				if (_serum.NumColors == 16) {
					_graphs.Add(new RenderGraph {
						Name = "4-bit Serum Graph",
						Source = _passthroughGray4Source,
						Destinations = renderers,
						Converter = _serum,
						Resize = _config.Global.Resize,
						FlipHorizontally = _config.Global.FlipHorizontally,
						FlipVertically = _config.Global.FlipVertically,
						ScalerMode = _config.Global.ScalerMode
					});
				}
				// 2-bit graph
				else {
					_graphs.Add(new RenderGraph {
						Name = "2-bit Serum Graph",
						Source = _passthroughGray2Source,
						Destinations = renderers,
						Converter = _serum,
						Resize = _config.Global.Resize,
						FlipHorizontally = _config.Global.FlipHorizontally,
						FlipVertically = _config.Global.FlipVertically,
						ScalerMode = _config.Global.ScalerMode
					});
				}
			}

			// === COLORIZATION PLUGIN ===
			else if (_colorize && _colorizationPlugin != null) {

				// 2-bit graph
				_graphs.Add(new RenderGraph {
					Name = "2-bit Colorization Plugin Graph",
					Source = _passthroughGray2Source,
					Destinations = renderers,
					Converter = _colorizationPlugin,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode,
				});
				ReportingTags.Add("Color:Gray2");

				// 4-bit graph
				_graphs.Add(new RenderGraph {
					Name = "4-bit Colorization Plugin Graph",
					Source = _passthroughGray4Source,
					Destinations = renderers,
					Converter = _colorizationPlugin,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode,
				});
				ReportingTags.Add("Color:Gray4");

				// alphanum graph
				_graphs.Add(new RenderGraph {
					Name = "Alphanumeric Colorization Plugin Graph",
					Source = _passthroughAlphaNumericSource,
					Destinations = renderers,
					Converter = _colorizationPlugin,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode,
				});
				ReportingTags.Add("Color:Gray4");
			}

			// === NATIVE VNI 2-bit ===
			else if (_colorize && _pin2ColorResult != null) {

				// 2-bit graph
				_graphs.Add(new RenderGraph {
					Name = "2-bit Pin2Color Graph",
					Source = _passthroughGray2Source,
					Destinations = renderers,
					Converter = _pin2ColorResult.Gray2Colorizer,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode
				});
				ReportingTags.Add("Color:Gray2");

				// 4-bit graph
				_graphs.Add(new RenderGraph {
					Name = "4-bit Pin2Color Graph",
					Source = _passthroughGray4Source,
					Destinations = renderers,
					Converter = _pin2ColorResult.Gray4Colorizer,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode
				});
				ReportingTags.Add("Color:Gray4");
			}

			// === NO COLORIZATION ===
			else {
				_graphs.Add(new RenderGraph {
					Name = "2-bit Passthrough Graph",
					Source = _passthroughGray2Source,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode
				});

				_graphs.Add(new RenderGraph {
					Name = "4-bit Passthrough Graph",
					Source = _passthroughGray4Source,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode
				});
			}

			// rgb24 graph
			_graphs.Add(new RenderGraph {
				Name = "RGB24 Passthrough Graph",
				Source = _passthroughRgb24Source,
				Destinations = renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically,
				ScalerMode = _config.Global.ScalerMode
			});

			// alphanumeric graph
			_graphs.Add(new RenderGraph {
				Name = "Alphanumeric Passthrough Graph",
				Source = _passthroughAlphaNumericSource,
				Destinations = renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically,
				ScalerMode = _config.Global.ScalerMode
			});

			// if colorization enabled and frame-by-frame colorization enabled, just clear the color.
			if (_colorize && (_serum != null || _colorizationPlugin != null || _pin2ColorResult != null)) {
				Logger.Info("Just clearing palette, colorization is done by converter.");
				_graphs.ClearColor();

			} else if (_colorize && _palette != null) {

				// if colorization enabled and palette is set, apply palette.
				Logger.Info("Applying palette to render graphs.");
				_graphs.ClearColor();

				// todo retrieve palette from .pal
				// if (_vniColoring != null) {
				// 	_graphs.SetPalette(_palette, _vniColoring.DefaultPaletteIndex);

				if (_palette != null) {
					_graphs.SetPalette(_palette, -1);
				}

			} else {
				// if colorization disabled, apply default color.
				Logger.Info("Applying default color to render graphs ({0}).", _color);
				_graphs.ClearPalette();
				_graphs.SetColor(_color);
			}

			_graphs.Init().StartRendering();
		}

		/// <summary>
		/// Tuät ä nii Inschantz vom virtueuä DMD kreiärä und tuät drnah d
		/// Render-Graphä drabindä.
		/// </summary>
		private void CreateVirtualDmd()
		{
			// set up an event object to synchronize with the thread startup
			var ev = new EventWaitHandle(false, EventResetMode.AutoReset);

			// launch a thread for the virtual DMD window event handler
			_dmdThread = new Thread(() => {

				// create the virtual DMD window and create the render graphs
				if (_config.VirtualDmd.Enabled) {
					_virtualDmd = new VirtualDmd();
					_virtualDmd.Setup(_config, _gameName);
				}

				SetupGraphs();

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

				// When the window closes, shut down the dispatcher
				if (_config.VirtualDmd.Enabled) {
					_virtualDmd.Closed += (s, e) => _virtualDmd.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
					_virtualDmd.Dispatcher.Invoke(SetupVirtualDmd);
				}

				// we're done with the setup - let the calling thread proceed
				ev.Set();

				// Start the Dispatcher Processing
				Dispatcher.Run();
			});
			_dmdThread.SetApartmentState(ApartmentState.STA);
			_dmdThread.Start();

			// wait until the virtual DMD window is fully set up, to avoid any
			// race conditions with the UI thread
			ev.WaitOne();
			ev.Dispose();
		}

		/// <summary>
		/// Sets the virtual DMD's parameters, initializes it and shows it.
		/// </summary>
		private void SetupVirtualDmd()
		{
			_virtualDmd.Setup(_config, _gameName);
			if (_config.VirtualDmd.UseRegistryPosition) {
				try {
					var regPath = @"Software\Freeware\Visual PinMame\" + (_gameName.Length > 0 ? _gameName : "default");
					var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
					key = key.OpenSubKey(regPath);

					if (key == null) {
						// couldn't find the value in the 32-bit view so grab the 64-bit view
						key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
						key = key.OpenSubKey(regPath);
					}
					if (key != null) {
						var values = key.GetValueNames();
						if (!values.Contains("dmd_pos_x") && values.Contains("dmd_pos_y") && values.Contains("dmd_width") && values.Contains("dmd_height")) {
							Logger.Warn("Not all values were found at HKEY_CURRENT_USER\\{0}. Trying default.", regPath);
							key?.Dispose();
							regPath = @"Software\Freeware\Visual PinMame\default";
							key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
							key = key.OpenSubKey(regPath);
						}
					}
					// still null?
					if (key != null) {
						var values = key.GetValueNames();
						if (values.Contains("dmd_pos_x") && values.Contains("dmd_pos_y") && values.Contains("dmd_width") && values.Contains("dmd_height")) {
							SetVirtualDmdDefaultPosition(
								Convert.ToInt64(key.GetValue("dmd_pos_x").ToString()),
								Convert.ToInt64(key.GetValue("dmd_pos_y").ToString()),
								 Convert.ToInt64(key.GetValue("dmd_width").ToString()),
								Convert.ToInt64(key.GetValue("dmd_height").ToString())
							);
						}
						else {
							Logger.Warn("Ignoring VPM registry for DMD position because not all values were found at HKEY_CURRENT_USER\\{0}. Found keys: [ {1} ]", regPath, string.Join(", ", values));
							SetVirtualDmdDefaultPosition();
						}
					}
					else {
						Logger.Warn("Ignoring VPM registry for DMD position because key was not found at HKEY_CURRENT_USER\\{0}", regPath);
						SetVirtualDmdDefaultPosition();
					}
					key?.Dispose();

				}
				catch (Exception ex) {
					Logger.Warn(ex, "Could not retrieve registry values for DMD position for game \"" + _gameName + "\".");
					SetVirtualDmdDefaultPosition();
				}
			}
			else {
				Logger.Debug("DMD position: No registry because it's ignored.");
				SetVirtualDmdDefaultPosition();
			}

			_virtualDmd.Dmd.Init();
			_virtualDmd.Show();
		}

		/// <summary>
		/// Sets the position of the DMD as defined in the .ini file.
		/// </summary>
		private void SetVirtualDmdDefaultPosition(double x = -1d, double y = -1d, double width = -1d, double height = -1d)
		{
			var aspectRatio = _virtualDmd.Width / _virtualDmd.Height;
			_virtualDmd.Left = _config.VirtualDmd.HasGameOverride("left") || x < 0 ? _config.VirtualDmd.Left : x;
			_virtualDmd.Top = _config.VirtualDmd.HasGameOverride("top") || y < 0 ? _config.VirtualDmd.Top : y;
			_virtualDmd.Width = _config.VirtualDmd.HasGameOverride("width") || width < 0 ? _config.VirtualDmd.Width : width;
			if (_config.VirtualDmd.IgnoreAr) {
				_virtualDmd.Height = _config.VirtualDmd.HasGameOverride("height") || height < 0 ? _config.VirtualDmd.Height : height;
			}
			else {
				_virtualDmd.Height = _virtualDmd.Width / aspectRatio;
			}
		}

		/// <summary>
		/// Goes through all the colorizers until the first is loaded.
		/// </summary>
		private void SetupColorizer()
		{
			// only setup if enabled and path is set
			if (!_config.Global.Colorize || _colorizationLoader == null || _gameName == null || !_colorize) {
				Analytics.Instance.ClearColorizer();
				return;
			}

			// 1. check for serum
			_serum = _colorizationLoader.LoadSerum(_gameName, _config.Global.ScalerMode);

			// 2. check for plugins
			if (_serum == null) {
				_colorizationPlugin = _colorizationLoader.LoadPlugin(_config.Global.Plugins, _colorize, _gameName, _color, _palette, _config.Global.ScalerMode);
			}

			// 3. check for native pin2color
			if (_serum == null && _colorizationPlugin == null) {
				_pin2ColorResult = _colorizationLoader.LoadPin2Color(_gameName, _config.Global.ScalerMode);
			}
		}

		#region Analytics

		private bool _analyticsVirtualDmdEnabled;

		private void AnalyticsSetDmd()
		{
			if (!_config.VirtualDmd.Enabled || _analyticsVirtualDmdEnabled || _virtualDmd == null) {
				return;
			}
			_analyticsVirtualDmdEnabled = true;
			Analytics.Instance.ClearVirtualDestinations();
			Analytics.Instance.AddDestination(_virtualDmd.Dmd);
			Analytics.Instance.StartGame();
		}
		
		private void AnalyticsSetSegmentDisplay()
		{
			if (!_config.VirtualAlphaNumericDisplay.Enabled || _analyticsVirtualDmdEnabled || _alphaNumericDest == null) {
				return;
			}
			_analyticsVirtualDmdEnabled = true;
			Analytics.Instance.ClearVirtualDestinations();
			Analytics.Instance.AddDestination(_alphaNumericDest);
			Analytics.Instance.StartGame();
		}

		private void AnalyticsClear()
		{
			_analyticsVirtualDmdEnabled = false;
		}
		
		#endregion

		#region Error Reporting

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (!(e.ExceptionObject is Exception ex)) {
				return;
			}

			Logger.Error(ex.ToString());
			ReportError(ex);
		}

		private static void ReportError(Exception ex)
		{
#if !DEBUG
			Raygun.ApplicationVersion = _fullVersion;
			Raygun.Send(ex,
				Enumerable.ToList(ReportingTags),
				new Dictionary<string, string> {
					{ "log", string.Join("\n", MemLogger.Logs) },
					{ "sha", _sha }
				}
			);
#endif
		}

		#endregion
	}
}
