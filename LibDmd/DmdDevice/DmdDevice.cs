using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using LibDmd.Common;
using LibDmd.Input.PinMame;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Output.Network;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Output.ZeDMD;
using LibDmd.Output.PinUp;
using LibDmd.Output.Pixelcade;
using LibDmd.Output.Virtual.AlphaNumeric;
using Microsoft.Win32;
using Mindscape.Raygun4Net;
using NLog;
using NLog.Config;
using NLog.Targets;
using LibDmd.Converter.Serum;
using LibDmd.Converter.Pin2Color;

namespace LibDmd.DmdDevice
{
	/// <summary>
	/// Hiä isch d Haiptlogik fir d <c>DmdDevice.dll</c> fir VPinMAME.
	/// </summary>
	/// <seealso cref="LibDmd.DmdDevice">Vo det chemid d Datä übr VPinMAME</seealso>
	public class DmdDevice : IDmdDevice
	{
		private const int Width = 128;
		private const int Height = 32;
		private int aniWidth = 128;
		private int aniHeight = 32;

		private readonly Configuration _config;
		private readonly VpmGray2Source _vpmGray2Source;
		private readonly VpmGray4Source _vpmGray4Source;
		private readonly VpmRgb24Source _vpmRgb24Source;
		private readonly VpmColoredGraySource _vpmColoredGraySource;
		private readonly VpmAlphaNumericSource _vpmAlphaNumericSource;
		private readonly BehaviorSubject<FrameFormat> _currentFrameFormat;
		private readonly RenderGraphCollection _graphs = new RenderGraphCollection();
		private static string _version = "";
		private static string _sha = "";
		private static string _fullVersion = "";
		private VirtualDmd _virtualDmd;
		private VirtualAlphanumericDestination _alphaNumericDest;

		// Ziigs vo VPM
		private string _gameName;
		private bool _colorize;
		private Color _color = RenderGraph.DefaultColor;
		private readonly DMDFrame _dmdFrame = new DMDFrame();
		private readonly DMDFrame _coloredDmdFrame = new DMDFrame();

		// Iifärbigsziig
		private Color[] _palette;
		private bool _isOpen;

		private bool _isColored;
		
		private Pin2Color.ColorizerMode _pin2ColorizerMode;

		// Wärchziig
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static readonly string AssemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
		private static string _altcolorPath;
		private static readonly MemoryTarget MemLogger = new MemoryTarget {
			Name = "Raygun Logger",
			Layout = "${pad:padding=4:inner=[${threadid}]} ${date} ${pad:padding=5:inner=${level:uppercase=true}} | ${message} ${exception:format=ToString}"
		};
		private static readonly HashSet<string> ReportingTags = new HashSet<string>();
		private Serum _serum;

		public DmdDevice()
		{
			_currentFrameFormat = new BehaviorSubject<FrameFormat>(FrameFormat.Rgb24);
			_vpmGray2Source = new VpmGray2Source(_currentFrameFormat);
			_vpmGray4Source = new VpmGray4Source(_currentFrameFormat);
			_vpmRgb24Source = new VpmRgb24Source(_currentFrameFormat);
			_vpmColoredGraySource = new VpmColoredGraySource(_currentFrameFormat);
			_vpmAlphaNumericSource = new VpmAlphaNumericSource(_currentFrameFormat);

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			// setup logger
			var assembly = Assembly.GetCallingAssembly();
			var assemblyPath = Path.GetDirectoryName(new Uri(assembly.CodeBase).LocalPath);
			var logConfigPath = Path.Combine(assemblyPath, "DmdDevice.log.config");
			if (File.Exists(logConfigPath))
			{
				LogManager.Configuration = new XmlLoggingConfiguration(logConfigPath, true);
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
			CultureUtil.NormalizeUICulture();
			_config = new Configuration();
			_altcolorPath = GetColorPath();

			// read versions from assembly
			var attr = assembly.GetCustomAttributes(typeof(AssemblyConfigurationAttribute), false);
			var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
			_version = fvi.ProductVersion;
			if (attr.Length > 0)
			{
				var aca = (AssemblyConfigurationAttribute)attr[0];
				_sha = aca.Configuration;
				if (string.IsNullOrEmpty(_sha))
				{
					_fullVersion = _version;

				}
				else
				{
					_fullVersion = $"{_version} ({_sha})";
				}

			}
			else
			{
				_fullVersion = fvi.ProductVersion;
				_sha = "";
			}

			Logger.Info("Starting VPinMAME API {0} through {1}.exe.", _fullVersion,
				Process.GetCurrentProcess().ProcessName);
			Logger.Info("Assembly located at {0}", assembly.Location);
			Logger.Info("Running in {0}", Directory.GetCurrentDirectory());
		}

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

			SetupColorizer();

			if (Pin2Color._colorizerIsOpen)
			{
				_pin2ColorizerMode = (Pin2Color.ColorizerMode) Pin2Color.Init(_colorize, _gameName, _color.R, _color.G, _color.B);
				if (_pin2ColorizerMode >= 0)
					_isColored = true;
				else
					_isColored = false;
			}

			if (_config.VirtualDmd.Enabled || _config.VirtualAlphaNumericDisplay.Enabled) {
				if (_virtualDmd == null && _alphaNumericDest == null) {
					Logger.Info("Opening virtual display...");
					CreateVirtualDmd();

				} else if (_config.VirtualDmd.Enabled) {
					try {
						_virtualDmd?.Dispatcher.Invoke(() => {
							SetupGraphs();
							SetupVirtualDmd();
						});

					} catch (TaskCanceledException e) {
						Logger.Error(e, "Main thread seems already destroyed, aborting.");
					}
				}
			} else {
				SetupGraphs();
			}
			_isOpen = true;
		}

		private void SetupColorizer()
		{
			// only setup if enabled and path is set
			if (!_config.Global.Colorize || _altcolorPath == null || _gameName == null || !_colorize)
			{
				return;
			}

			var serumPath = Path.Combine(_altcolorPath, _gameName, _gameName + ".cRZ");
			if (File.Exists(serumPath))
			{
				try
				{
					_serum = new Serum(_altcolorPath, _gameName);
					if (_serum.IsLoaded)
					{
						Logger.Info($"Serum colorizer v{Serum.GetVersion()} initialized.");
						Logger.Info($"Loading colorization at {serumPath}...");
						_serum.ScalerMode = _config.Global.ScalerMode;
						aniWidth = _serum.FrameWidth;
						aniHeight = _serum.FrameHeight;

					}
					else
					{
						Logger.Warn($"Found Serum coloring file at {serumPath}, but could not load colorizer.");
						_serum = null;
					}

				}
				catch (Exception e)
				{
					Logger.Warn(e, "Error initializing colorizer: {0}", e.Message);
					_serum = null;
				}
			}

			if (_config.Global.ScaleToHd)
			{
				Logger.Info("ScaleToHd = True, ScalerMode = " + _config.Global.ScalerMode.ToString());
			}
			else
			{
				Logger.Info("ScaleToHd = False");
			}
		}


		/// <summary>
		/// Tuät ä nii Inschantz vom virtueuä DMD kreiärä und tuät drnah d
		/// Render-Graphä drabindä.
		/// </summary>
		private void CreateVirtualDmd()
		{
			// set up an event object to synchronize with the thread startup
			var ev = new EventWaitHandle(false, EventResetMode.AutoReset);

			// launch a thrtead for the virtual DMD window event handler
			var thread = new Thread(() => {

				// create the virtual DMD window and create the render grahps
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
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();

			// wait until the virtual DMD window is fully set up, to avoid any
			// race conditions with the UI thread
			ev.WaitOne();
			ev.Dispose();
		}

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
				}
			}
			if (_config.PinDmd2.Enabled) {
				var pinDmd2 = PinDmd2.GetInstance();
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Logger.Info("Added PinDMDv2 renderer.");
					ReportingTags.Add("Out:PinDMDv2");
				}
			}
			if (_config.PinDmd3.Enabled)
			{
				var pinDmd3 = PinDmd3.GetInstance(_config.PinDmd3.Port);
				if (pinDmd3.IsAvailable)
				{
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
					ReportingTags.Add("Out:PinDMDv3");
				}
			}
			if (_config.ZeDMD.Enabled)
			{
				var zeDmd = ZeDMD.GetInstance();
				if (zeDmd.IsAvailable)
				{
					renderers.Add(zeDmd);
					Logger.Info("Added ZeDMD renderer.");
					ReportingTags.Add("Out:ZeDMD");
					zeDmd.SetOriginalDimensions(aniWidth, aniHeight);
				}
			}
			if (_config.Pin2Dmd.Enabled) {
				var pin2Dmd = Pin2Dmd.GetInstance(_config.Pin2Dmd.Delay);
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");
					ReportingTags.Add("Out:PIN2DMD");
				}

				var pin2DmdXl = Pin2DmdXl.GetInstance(_config.Pin2Dmd.Delay);
				if (pin2DmdXl.IsAvailable) {
					renderers.Add(pin2DmdXl);
					Logger.Info("Added PIN2DMD XL renderer.");
					ReportingTags.Add("Out:PIN2DMDXL");
				}

				var pin2DmdHd = Pin2DmdHd.GetInstance(_config.Pin2Dmd.Delay);
				if (pin2DmdHd.IsAvailable) {
					renderers.Add(pin2DmdHd);
					Logger.Info("Added PIN2DMD HD renderer.");
					ReportingTags.Add("Out:PIN2DMDHD");
				}
			}
			if (_config.Pixelcade.Enabled) {
				var pixelcade = Pixelcade.GetInstance(_config.Pixelcade.Port, _config.Pixelcade.ColorMatrix);
				if (pixelcade.IsAvailable) {
					renderers.Add(pixelcade);
					Logger.Info("Added Pixelcade renderer.");
					ReportingTags.Add("Out:Pixelcade");
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
					renderers.Add(new VideoOutput(Path.Combine(rootPath, _config.Video.Path, _gameName + ".avi"), _config.Global.ScaleToHd));
					Logger.Info("Added video renderer.");
					ReportingTags.Add("Out:Video");

				} else if (Directory.Exists(Path.GetDirectoryName(Path.Combine(rootPath, _config.Video.Path))) && _config.Video.Path.Length > 4 && _config.Video.Path.EndsWith(".avi")) {
					renderers.Add(new VideoOutput(Path.Combine(rootPath, _config.Video.Path), _config.Global.ScaleToHd));
					Logger.Info("Added video renderer.");
					ReportingTags.Add("Out:Video");

				} else {
					Logger.Warn("Ignoring video renderer for non-existing path \"{0}\"", _config.Video.Path);
				}
			}
			if (_config.PinUp.Enabled) {
				try {
					var pinupOutput = new PinUpOutput(_gameName);
					if (pinupOutput.IsAvailable) {
						if ((_serum != null) && (pinupOutput.isPuPTrigger)) _serum.SetPinupInstance(pinupOutput);
						if (Pin2Color._colorizerIsOpen && (pinupOutput.isPuPTrigger)) Pin2Color.SetPinUpOutput(pinupOutput);
						renderers.Add(pinupOutput);
						Logger.Info("Added PinUP renderer.");
						ReportingTags.Add("Out:PinUP");
					}

				} catch (Exception e) {
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
					renderers.Add(new GifOutput(path));
					Logger.Info("Added animated GIF renderer, saving to {0}", path);
					ReportingTags.Add("Out:GIF");

				} else {
					Logger.Warn("Ignoring animated GIF renderer for non-existing path \"{0}\"", Path.GetDirectoryName(path));
				}
			}
			if (_config.VpdbStream.Enabled) {
				renderers.Add(new VpdbStream { EndPoint = _config.VpdbStream.EndPoint });
				Logger.Info("Added VPDB stream renderer.");
				ReportingTags.Add("Out:VpdbStream");
			}
			if (_config.BrowserStream.Enabled) {
				renderers.Add(new BrowserStream(_config.BrowserStream.Port, _gameName));
				Logger.Info("Added browser stream renderer.");
				ReportingTags.Add("Out:BrowserStream");
			}
			if (_config.NetworkStream.Enabled) {
				renderers.Add(NetworkStream.GetInstance(_config.NetworkStream, _gameName));
				Logger.Info("Added network stream renderer.");
				ReportingTags.Add("Out:NetworkStream");
			}

			if (renderers.Count == 0) {
				Logger.Error("No renderers found, exiting.");
				return;
			}

			Logger.Info("Transformation options: Resize={0}, HFlip={1}, VFlip={2}", _config.Global.Resize, _config.Global.FlipHorizontally, _config.Global.FlipVertically);

			if (_serum != null)
			{
				if (_serum.NumColors == 16)
				{
					_graphs.Add(new RenderGraph
					{
						Name = "4-bit Colored VPM Graph",
						Source = _vpmGray4Source,
						Destinations = renderers,
						Converter = _serum,
						Resize = _config.Global.Resize,
						FlipHorizontally = _config.Global.FlipHorizontally,
						FlipVertically = _config.Global.FlipVertically,
						ScalerMode = _config.Global.ScalerMode
					});
				}
				else
				{
					_graphs.Add(new RenderGraph
					{
						Name = "2-bit Colored VPM Graph",
						Source = _vpmGray2Source,
						Destinations = renderers,
						Converter = _serum,
						Resize = _config.Global.Resize,
						FlipHorizontally = _config.Global.FlipHorizontally,
						FlipVertically = _config.Global.FlipVertically,
						ScalerMode = _config.Global.ScalerMode
					});
				}
			}
			else
			{
				// 2-bit graph
				_graphs.Add(new RenderGraph
				{
					Name = "2-bit VPM Graph",
					Source = _vpmGray2Source,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode,
					Colored = _isColored
				});

				// 4-bit graph
				_graphs.Add(new RenderGraph
				{
					Name = "4-bit VPM Graph",
					Source = _vpmGray4Source,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
					ScalerMode = _config.Global.ScalerMode,
					Colored = _isColored
				});
			}

			// rgb24 graph
			_graphs.Add(new RenderGraph {
				Name = "RGB24-bit VPM Graph",
				Source = _vpmRgb24Source,
				Destinations = renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically,
				ScalerMode = _config.Global.ScalerMode,
				Colored = _isColored
			});

			// colored rgb24 graph
			_graphs.Add(new RenderGraph
			{
				Name = "colored RGB24-bit VPM Graph",
				Source = _vpmColoredGraySource,
				Destinations = renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically,
				ScalerMode = _config.Global.ScalerMode,
				Colored = _isColored
			});

			// alphanumeric graph
			_graphs.Add(new RenderGraph {
				Name = "Alphanumeric VPM Graph",
				Source = _vpmAlphaNumericSource,
				Destinations = renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically,
				ScalerMode = _config.Global.ScalerMode,
				Colored = _isColored
			});

			if (_serum != null)
			{
				Logger.Info("Just clearing palette, colorization is done by converter.");
				_graphs.ClearColor();

			}
			else if (_colorize && _palette != null) {
				Logger.Info("Applying palette to render graphs.");
				_graphs.ClearColor();
				_graphs.SetPalette(_palette, -1);

			} else {
				Logger.Info("Applying default color to render graphs ({0}).", _color);
				_graphs.ClearPalette();
				_graphs.SetColor(_color);
			}

			_graphs.Init().StartRendering();
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
						} else {
							Logger.Warn("Ignoring VPM registry for DMD position because not all values were found at HKEY_CURRENT_USER\\{0}. Found keys: [ {1} ]", regPath, string.Join(", ", values));
							SetVirtualDmdDefaultPosition();
						}
					} else {
						Logger.Warn("Ignoring VPM registry for DMD position because key was not found at HKEY_CURRENT_USER\\{0}", regPath);
						SetVirtualDmdDefaultPosition();
					}
					key?.Dispose();

				} catch (Exception ex) {
					Logger.Warn(ex, "Could not retrieve registry values for DMD position for game \"" + _gameName + "\".");
					SetVirtualDmdDefaultPosition();
				}
			} else {
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
			} else {
				_virtualDmd.Height = _virtualDmd.Width / aspectRatio;
			}
		}

		/// <summary>
		/// Tuät aui Renderer ahautä unds virtueua DMD vrschteckä.
		/// </summary>
		public void Close()
		{
			Logger.Info("Closing up.");
			_graphs.ClearDisplay();
			_graphs.Dispose();
			try {
				_virtualDmd?.Dispatcher?.Invoke(() => _virtualDmd?.Close());
				_virtualDmd = null;
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Could not hide DMD because task was already canceled.");
			}

			_alphaNumericDest = null;
			_color = RenderGraph.DefaultColor;
			_palette = null;
			if (_serum != null) _serum.Dispose();
			_serum = null;
			_isOpen = false;

			if (Pin2Color._colorizerIsOpen)
			{
				Pin2Color.Close();
				Pin2Color._colorizerIsOpen = false;
			}
		}

		public void SetGameName(string gameName)
		{
			if (_gameName != null) { // only reload if game name is set (i.e. we didn't just load because we just started)
				_config.Reload();
			}

			Logger.Info("Setting game name: {0}", gameName);
			_gameName = gameName;
			_config.GameName = gameName;
		}

		public void SetColorize(bool colorize)
		{
			Logger.Info("{0} game colorization", colorize ? "Enabling" : "Disabling");
			_colorize = colorize;

			if (_config.Global.Colorize && !Pin2Color._colorizerIsLoaded)
			{
				Pin2Color._colorizerIsLoaded = Pin2Color.SetColorize();
			} 

			if (Pin2Color._colorizerIsLoaded)
			{
				if (!Pin2Color._colorizerIsOpen)
					Pin2Color._colorizerIsOpen = Pin2Color.Open();

				if (_palette != null && Pin2Color._colorizerIsOpen)
					SetPalette(_palette);
			}

		}

		public void ConsoleData(byte data)
		{
			if (Pin2Color._colorizerIsOpen)
				Pin2Color.ConsoleData(data);
		}

		public void SetColor(Color color)
		{
			Logger.Info("Setting color: {0}", color);
			_color = color;
		}
		public void SetPalette(Color[] colors)
		{
			Logger.Info("Setting palette to {0} colors...", colors.Length);
			_palette = colors;
			if (Pin2Color._colorizerIsOpen)
			{
				var _pal = new byte[_palette.Length * 3];
				for (int i = 0; i < _palette.Length; i++)
				{
					_pal[i*3] = colors[i].R;
					_pal[(i*3)+1] = colors[i].G;
					_pal[(i*3)+2] = colors[i].B;
				}
				if (_palette.Length == 4)
				{
					Pin2Color.Set_4_Colors(_pal);
				}
				else if (_palette.Length == 16)
				{
					Pin2Color.Set_16_Colors(_pal);
				}

			}
		}
		public int GetAniHeight()
		{
			return aniHeight;
		}
		public int GetAniWidth()
		{
			return aniWidth;
		}
		public void RenderGray2(DMDFrame frame)
		{
			if (!_isOpen) {
				Init();
			}

			int width = frame.width;
			int height = frame.height;

			if (_serum != null)
			{
				if (_config.Global.ScaleToHd)
				{
					if (width == 128 && height == 32)
					{
						width *= 2;
						height *= 2;
						frame.Update(width, height, frame.Data);
					}
				}
				_serum.SetDimensions(frame.width, frame.height);
				_serum.Convert(frame);
				_vpmGray2Source.NextFrame(frame);
			}
			else
			{

				if (Pin2Color._colorizerIsOpen && _pin2ColorizerMode >= 0)
				{
					if (_pin2ColorizerMode == Pin2Color.ColorizerMode.Advanced128x32 && ((frame.width == 128 && frame.height == 32) || (frame.width == 128 && frame.height == 16)))
					{
						width = 128;
						height = 32;
					}
					else if (_pin2ColorizerMode == Pin2Color.ColorizerMode.Advanced192x64 && frame.width == 192 && frame.height == 64)
					{
						width = 192;
						height = 64;
					}
					else if (_pin2ColorizerMode == Pin2Color.ColorizerMode.Advanced256x64 && ((frame.width == 128 && frame.height == 32) || (frame.width == 256 && frame.height == 64)))
					{
						width = 256;
						height = 64;
					}
					else
					{
						_pin2ColorizerMode = Pin2Color.ColorizerMode.SimplePalette;
					}
				}

				var frameSize = width * height * 3;
				var coloredFrame = new byte[frameSize];

				if (Pin2Color._colorizerIsOpen)
				{
					if (frame is RawDMDFrame vd && vd.RawPlanes.Length > 0)
					{
						var RawBuffer = new byte[vd.RawPlanes.Length * vd.RawPlanes[0].Length];
						for (int i = 0; i < vd.RawPlanes.Length; i++)
						{
							vd.RawPlanes[i].CopyTo(RawBuffer, i * vd.RawPlanes[0].Length);
						}
						var Rgb24Buffer = Pin2Color.Render2GrayWithRaw((ushort)frame.width, (ushort)frame.height, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					}
					else
					{
						var Rgb24Buffer = Pin2Color.Render2Gray((ushort)frame.width, (ushort)frame.height, frame.Data);
						if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None)
						{
							if (_pin2ColorizerMode == Pin2Color.ColorizerMode.SimplePalette && width == 128 && height == 16)
							{
								coloredFrame = new byte[frameSize * 2];
								Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize * 2);
								height = 32;
							}
							else
								Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
						}
					}

					if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None)
					{
						if (_config.Global.ScaleToHd)
						{
							if (width == 128 && height == 32)
							{
								width *= 2;
								height *= 2;
							}
						}

						_coloredDmdFrame.Update(width, height, coloredFrame);
						_vpmColoredGraySource.NextFrame(_coloredDmdFrame);
					}
				}

				if (Pin2Color._colorizerIsOpen && _pin2ColorizerMode != Pin2Color.ColorizerMode.None && frame.width == 128 && frame.height == 16)
				{
					frame.Update(width, height, frame.Data);
				}
				else
				{
					if (_config.Global.ScaleToHd)
					{
						if (width == 128 && height == 32)
						{
							width *= 2;
							height *= 2;
						}
					}

					frame.Update(width, height, frame.Data);
				}

				_vpmGray2Source.NextFrame(frame);
			}
		}

		public void RenderGray4(DMDFrame frame)
		{
			if (!_isOpen)
			{
				Init();
			}

			int width = frame.width;
			int height = frame.height;

			if (_serum != null)
			{
				if (_config.Global.ScaleToHd)
				{
					if (width == 128 && height == 32)
					{
						width *= 2;
						height *= 2;
						frame.Update(width, height, frame.Data);
					}
				}
				_serum.SetDimensions(frame.width, frame.height);
				_serum.Convert(frame);
				_vpmGray4Source.NextFrame(frame);
			}
			else
			{

				if (Pin2Color._colorizerIsOpen && _pin2ColorizerMode >= 0)
				{
					if (_pin2ColorizerMode == Pin2Color.ColorizerMode.Advanced128x32 && ((frame.width == 128 && frame.height == 32) || (frame.width == 128 && frame.height == 16)))
					{
						width = 128;
						height = 32;
					}
					else if (_pin2ColorizerMode == Pin2Color.ColorizerMode.Advanced192x64 && frame.width == 192 && frame.height == 64)
					{
						width = 192;
						height = 64;
					}
					else if (_pin2ColorizerMode == Pin2Color.ColorizerMode.Advanced256x64 && ((frame.width == 128 && frame.height == 32) || (frame.width == 256 && frame.height == 64)))
					{
						width = 256;
						height = 64;
					}
					else
					{
						_pin2ColorizerMode = Pin2Color.ColorizerMode.SimplePalette;
					}
				}

				var frameSize = width * height * 3;
				var coloredFrame = new byte[frameSize];

				if (Pin2Color._colorizerIsOpen)
				{
					if (frame is RawDMDFrame vd && vd.RawPlanes.Length > 0)
					{
						var RawBuffer = new byte[vd.RawPlanes.Length * vd.RawPlanes[0].Length];
						for (int i = 0; i < vd.RawPlanes.Length; i++)
						{
							vd.RawPlanes[i].CopyTo(RawBuffer, i * vd.RawPlanes[0].Length);
						}
						var Rgb24Buffer = Pin2Color.Render4GrayWithRaw((ushort)frame.width, (ushort)frame.height, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					}
					else
					{
						var Rgb24Buffer = Pin2Color.Render4Gray((ushort)frame.width, (ushort)frame.height, frame.Data);
						if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					}

					if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None)
					{
						if (_config.Global.ScaleToHd)
						{
							if (width == 128 && height == 32)
							{
								width *= 2;
								height *= 2;
							}
						}
						_coloredDmdFrame.Update(width, height, coloredFrame);
						_vpmColoredGraySource.NextFrame(_coloredDmdFrame);
					}
				}

				if (_config.Global.ScaleToHd)
				{
					if (width == 128 && height == 32)
					{
						width *= 2;
						height *= 2;

					}
				}
				frame.Update(width, height, frame.Data);
				_vpmGray4Source.NextFrame(frame);
			}
		}

		public void RenderRgb24(DMDFrame frame)
		{
			if (!_isOpen) {
				Init();
			}

			int width = frame.width;
			int height = frame.height;

			if (Pin2Color._colorizerIsOpen)
			{
				Pin2Color.RenderRGB24((ushort)frame.width, (ushort)frame.height, frame.Data);
			}

			if (_config.Global.ScaleToHd)
			{
				if (width == 128 && height == 32)
				{
					width *= 2;
					height *= 2;
					frame.Update(width, height, frame.Data);
				}
			}
			_vpmRgb24Source.NextFrame(frame);
		}

		public void RenderAlphaNumeric(NumericalLayout layout, ushort[] segData, ushort[] segDataExtended)
		{
			if (_gameName.StartsWith("spagb_")) {
				// ignore GB frames, looks like a bug from SPA side
				return;
			}

			if (!_isOpen) {
				Init();
			}

			var frameSize = Width * Height * 3;
			var coloredFrame = new byte[frameSize];

			if (Pin2Color._colorizerIsOpen)
			{
				var Rgb24Buffer = Pin2Color.RenderAlphaNumeric(layout, segData, segDataExtended);
				if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None)
				{
					Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					_coloredDmdFrame.Update(Width, Height, coloredFrame);
					_vpmColoredGraySource.NextFrame(_coloredDmdFrame);
				}
			}

			_vpmAlphaNumericSource.NextFrame(new AlphaNumericFrame(layout, segData, segDataExtended));
			_dmdFrame.width = Width;
			_dmdFrame.height = Height;

			if (_gameName.StartsWith("rvrbt_")) layout = NumericalLayout.__1x16Alpha_1x16Num_1x7Num_1x4Num;
			if (_gameName.StartsWith("polic_")) layout = NumericalLayout.__1x7Num_1x16Alpha_1x16Num;

			//Logger.Info("Alphanumeric: {0}", layout);
			switch (layout) {
				case NumericalLayout.__2x16Alpha:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x16Alpha(segData)));
					break;
				case NumericalLayout.None:
				case NumericalLayout.__2x20Alpha:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x20Alpha(segData)));
					break;
				case NumericalLayout.__2x7Alpha_2x7Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x7Alpha_2x7Num(segData)));
					break;
				case NumericalLayout.__2x7Alpha_2x7Num_4x1Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x7Alpha_2x7Num_4x1Num(segData)));
					break;
				case NumericalLayout.__2x7Num_2x7Num_4x1Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x7Num_2x7Num_4x1Num(segData)));
					break;
				case NumericalLayout.__2x7Num_2x7Num_10x1Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x7Num_2x7Num_10x1Num(segData, segDataExtended)));
					break;
				case NumericalLayout.__2x7Num_2x7Num_4x1Num_gen7:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x7Num_2x7Num_4x1Num_gen7(segData)));
					break;
				case NumericalLayout.__2x7Num10_2x7Num10_4x1Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x7Num10_2x7Num10_4x1Num(segData)));
					break;
				case NumericalLayout.__2x6Num_2x6Num_4x1Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x6Num_2x6Num_4x1Num(segData)));
					break;
				case NumericalLayout.__2x6Num10_2x6Num10_4x1Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x6Num10_2x6Num10_4x1Num(segData)));
					break;
				case NumericalLayout.__4x7Num10:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render4x7Num10(segData)));
					break;
				case NumericalLayout.__6x4Num_4x1Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render6x4Num_4x1Num(segData)));
					break;
				case NumericalLayout.__2x7Num_4x1Num_1x16Alpha:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render2x7Num_4x1Num_1x16Alpha(segData)));
					break;
				case NumericalLayout.__1x16Alpha_1x16Num_1x7Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render1x16Alpha_1x16Num_1x7Num(segData)));
					break;
				case NumericalLayout.__1x7Num_1x16Alpha_1x16Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render1x7Num_1x16Alpha_1x16Num(segData)));
					break;
				case NumericalLayout.__1x16Alpha_1x16Num_1x7Num_1x4Num:
					_vpmGray2Source.NextFrame(_dmdFrame.Update(AlphaNumeric.Render1x16Alpha_1x16Num_1x7Num_1x4Num(segData)));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layout), layout, null);
			}
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var ex = e.ExceptionObject as Exception;
			if (ex != null) {
				Logger.Error(ex.ToString());
			}

#if !DEBUG
			Raygun.ApplicationVersion = _fullVersion;
			Raygun.Send(ex,
				ReportingTags.ToList(), // tags
				new Dictionary<string, string> {
					{ "log", string.Join("\n", MemLogger.Logs) },
					{ "sha", _sha }
				}
			);
#endif
		}

		private static string GetColorPath()
		{
			// first, try executing assembly.
			var altcolor = Path.Combine(AssemblyPath, "altcolor");
			if (Directory.Exists(altcolor))
			{
				Logger.Info("Determined color path from assembly path: {0}", altcolor);
				return altcolor;
			}

			// then, try vpinmame location
			var vpmPath = GetDllPath("VPinMAME.dll");
			if (vpmPath == null)
			{
				return null;
			}
			altcolor = Path.Combine(Path.GetDirectoryName(vpmPath), "altcolor");
			if (Directory.Exists(altcolor))
			{
				Logger.Info("Determined color path from VPinMAME.dll location: {0}", altcolor);
				return altcolor;
			}
			Logger.Info("No altcolor folder found, ignoring palettes.");
			return null;
		}

		private static string GetDllPath(string name)
		{
			const int maxPath = 260;
			var builder = new StringBuilder(maxPath);
			var hModule = GetModuleHandle(name);
			if (hModule == IntPtr.Zero) {
				return null;
			}
			var size = GetModuleFileName(hModule, builder, builder.Capacity);
			return size <= 0 ? null : builder.ToString();
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);

		[DllImport("kernel32.dll", SetLastError = true)]
		[PreserveSig]
		public static extern uint GetModuleFileName
		(
			[In] IntPtr hModule,
			[Out] StringBuilder lpFilename,
			[In][MarshalAs(UnmanagedType.U4)] int nSize
		);
	}
}
