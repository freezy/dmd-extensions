﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using LibDmd.Common;
using LibDmd.Converter;
using LibDmd.Converter.Colorize;
using LibDmd.Input.PinMame;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Output.Network;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Output.PinUp;
using LibDmd.Output.Virtual;
using LibDmd.Output.Virtual.AlphaNumeric;
using LibDmd.Output.Virtual.SkiaDmd;
using Microsoft.Win32;
using Mindscape.Raygun4Net;
using NLog;
using NLog.Config;
using NLog.Targets;

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

		private readonly Configuration _config;
		private readonly VpmGray2Source _vpmGray2Source = new VpmGray2Source();
		private readonly VpmGray4Source _vpmGray4Source = new VpmGray4Source();
		private readonly VpmRgb24Source _vpmRgb24Source = new VpmRgb24Source();
		private readonly VpmAlphaNumericSource _vpmAlphaNumericSource = new VpmAlphaNumericSource();
		private readonly RenderGraphCollection _graphs = new RenderGraphCollection();
		private SkiaDmdControl _dmd;

		// Ziigs vo VPM
		private string _gameName;
		private bool _colorize;
		private Color _color = RenderGraph.DefaultColor;

		// Iifärbigsziig
		private Color[] _palette;
		byte[] _upsizedFrame;
		private Gray2Colorizer _gray2Colorizer;
		private Gray4Colorizer _gray4Colorizer;
		private Coloring _coloring;
		private bool _isOpen = false;

		// Wärchziig
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static readonly string AssemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
		private static string _altcolorPath;
		private static readonly MemoryTarget MemLogger = new MemoryTarget {
			Name = "Raygun Logger",
			Layout = "${pad:padding=4:inner=[${threadid}]} ${date} ${pad:padding=5:inner=${level:uppercase=true}} | ${message} ${exception:format=ToString}"
		};

		public DmdDevice()
		{
			// setup logger
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
			var logConfigPath = Path.Combine(assemblyPath, "DmdDevice.log.config");
			if (File.Exists(logConfigPath)) {
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
			_config = new Configuration();
			_altcolorPath = GetColorPath();

			Logger.Info("Starting VPinMAME API through {0}.exe.", System.Diagnostics.Process.GetCurrentProcess().ProcessName);
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

			_gray2Colorizer = null;
			_gray4Colorizer = null;
			_coloring = null;

			SetupColorizer();

			if (_config.VirtualDmd.Enabled) {
				if (_dmd == null) {
					Logger.Info("Opening virtual DMD...");
					CreateVirtualDmd();

				} else {
					_dmd.Dispatcher.Invoke(() => {
						SetupGraphs();
						SetupVirtualDmd();
					});
				}
			} else {
				SetupGraphs();
			}
			_isOpen = true;
		}

		/// <summary>
		/// Wird uifgriäft wenn vom modifiziärtä ROM ibärä Sitäkanau ä Palettäwächsu
		/// ah gä wird.
		/// </summary>
		/// <param name="num">Weli Palettä muäss gladä wärdä</param>
		public void LoadPalette(uint num)
		{
			_gray4Colorizer?.LoadPalette(num);
		}

		private void SetupColorizer()
		{
			// only setup if enabled and path is set
			if (!_config.Global.Colorize || _altcolorPath == null || _gameName == null) {
				return;
			}

			// abort if already setup
			if (_gray2Colorizer != null || _gray4Colorizer != null) {
				return;
			}
			var palPath1 = Path.Combine(_altcolorPath, _gameName, _gameName + ".pal");
			var palPath2 = Path.Combine(_altcolorPath, _gameName, "pin2dmd.pal");
			var vniPath1 = Path.Combine(_altcolorPath, _gameName, _gameName + ".vni");
			var vniPath2 = Path.Combine(_altcolorPath, _gameName, "pin2dmd.vni");

			var palPath = File.Exists(palPath1) ? palPath1 : palPath2;
			var vniPath = File.Exists(vniPath1) ? vniPath1 : vniPath2;

			if (File.Exists(palPath)) {
				try {
					Logger.Info("Loading palette file at {0}...", palPath);
					_coloring = new Coloring(palPath);
					VniAnimationSet vni = null;
					if (File.Exists(vniPath)) {
						Logger.Info("Loading virtual animation file at {0}...", vniPath);
						vni = new VniAnimationSet(vniPath);
						Logger.Info("Loaded animation set {0}", vni);
					}
					_gray2Colorizer = new Gray2Colorizer(_coloring, vni);
					_gray4Colorizer = new Gray4Colorizer(_coloring, vni);
				} catch (Exception e) {
					Logger.Warn(e, "Error initializing colorizer: {0}", e.Message);
				}
			} else {
				Logger.Info("No palette file found at {0}.", palPath);
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
				_dmd = new SkiaDmdControl(new DmdStyleDefinition(), _config);
				//---_alphaNumericDisplay = new VirtualAlphaNumericDisplay();
				SetupGraphs();

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

				// When the window closes, shut down the dispatcher
				_dmd.Closed += (s, e) => _dmd.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
				_dmd.Dispatcher.Invoke(SetupVirtualDmd);

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

			var renderers = new List<IDestination>();
			if (_config.PinDmd1.Enabled) {
				var pinDmd1 = PinDmd1.GetInstance();
				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Logger.Info("Added PinDMDv1 renderer.");
				}
			}
			if (_config.PinDmd2.Enabled) {
				var pinDmd2 = PinDmd2.GetInstance();
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Logger.Info("Added PinDMDv2 renderer.");
				}
			}
			if (_config.PinDmd3.Enabled) {
				var pinDmd3 = PinDmd3.GetInstance(_config.PinDmd3.Port);
				if (pinDmd3.IsAvailable) {
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
				}
			}
			if (_config.Pin2Dmd.Enabled) {
				var pin2Dmd = Output.Pin2Dmd.Pin2Dmd.GetInstance(_config.Pin2Dmd.Delay);
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					if (_coloring != null) {
						pin2Dmd.PreloadPalettes(_coloring);
					}
					Logger.Info("Added PIN2DMD renderer.");
				}
			}
			if (_config.VirtualDmd.Enabled) {
				renderers.Add(_dmd);
				Logger.Info("Added VirtualDMD renderer.");
			}
			if (_config.VirtualAlphaNumericDisplay.Enabled) {
				var alphaNumeric = VirtualAlphanumericDestination.GetInstance(Dispatcher.CurrentDispatcher, _config.VirtualAlphaNumericDisplay.Style, _config);
				renderers.Add(alphaNumeric);
				Logger.Info("Added virtual alphanumeric renderer.");
			}
			if (_config.Video.Enabled) {

				var rootPath = "";
				if (_config.Video.Path.Length == 0 || !Path.IsPathRooted(_config.Video.Path)) {
					rootPath = AssemblyPath;
				}
				if (Directory.Exists(Path.Combine(rootPath, _config.Video.Path))) {
					renderers.Add(new VideoOutput(Path.Combine(rootPath, _config.Video.Path, _gameName + ".avi")));
					Logger.Info("Added video renderer.");

				} else if (Directory.Exists(Path.GetDirectoryName(Path.Combine(rootPath, _config.Video.Path))) && _config.Video.Path.Length > 4 && _config.Video.Path.EndsWith(".avi")) {
					renderers.Add(new VideoOutput(Path.Combine(rootPath, _config.Video.Path)));
					Logger.Info("Added video renderer.");

				} else {
					Logger.Warn("Ignoring video renderer for non-existing path \"{0}\"", _config.Video.Path);
				}
			}
			if (_config.PinUp.Enabled) {
				var pinupOutput = new PinUpOutput(_gameName);
				if (pinupOutput.IsAvailable) {
					renderers.Add(pinupOutput);
					Logger.Info("Added PinUP renderer.");
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

				} else {
					Logger.Warn("Ignoring animated GIF renderer for non-existing path \"{0}\"", Path.GetDirectoryName(path));
				}
			}
			if (_config.VpdbStream.Enabled) {
				renderers.Add(new VpdbStream { EndPoint = _config.VpdbStream.EndPoint });
			}
			if (_config.BrowserStream.Enabled) {
				renderers.Add(new BrowserStream(_config.BrowserStream.Port));
			}

			if (renderers.Count == 0) {
				Logger.Error("No renderers found, exiting.");
				return;
			}

			Logger.Info("Transformation options: Resize={0}, HFlip={1}, VFlip={2}", _config.Global.Resize, _config.Global.FlipHorizontally, _config.Global.FlipVertically);

			// 2-bit graph
			if (_colorize && _gray2Colorizer != null) {
				_graphs.Add(new RenderGraph {
					Name = "2-bit Colored VPM Graph",
					Source = _vpmGray2Source,
					Destinations = renderers,
					Converter = _gray2Colorizer,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically
				});

			} else {
				_graphs.Add(new RenderGraph {
					Name = "2-bit VPM Graph",
					Source = _vpmGray2Source,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically
				});
			}

			// 4-bit graph
			if (_colorize && _gray4Colorizer != null) {
				_graphs.Add(new RenderGraph {
					Name = "4-bit Colored VPM Graph",
					Source = _vpmGray4Source,
					Destinations = renderers,
					Converter = _gray4Colorizer,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically
				});

			} else {
				_graphs.Add(new RenderGraph {
					Name = "4-bit VPM Graph",
					Source = _vpmGray4Source,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically
				});
			}

			// rgb24 graph
			_graphs.Add(new RenderGraph {
				Name = "RGB24-bit VPM Graph",
				Source = _vpmRgb24Source,
				Destinations = renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically
			});
			
			// alphanumeric graph
			_graphs.Add(new RenderGraph {
				Name = "Alphanumeric VPM Graph",
				Source = _vpmAlphaNumericSource,
				Destinations = renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically
			});

			if (_colorize && (_gray2Colorizer != null || _gray4Colorizer != null)) {
				Logger.Info("Just clearing palette, colorization is done by converter.");
				_graphs.ClearColor();

			} else if (_colorize && _palette != null) {
				Logger.Info("Applying palette to render graphs.");
				_graphs.ClearColor();
				if (_coloring != null) {
					_graphs.SetPalette(_palette, _coloring.DefaultPaletteIndex);

				} else {
					_graphs.SetPalette(_palette, -1);
				}

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
			_dmd.IgnoreAspectRatio = _config.VirtualDmd.IgnoreAr;
			/*_dmd.AlwaysOnTop = _config.VirtualDmd.StayOnTop;
			_dmd.GripColor = _config.VirtualDmd.HideGrip ? Brushes.Transparent : Brushes.White;
			_dmd.DotSize = _config.VirtualDmd.DotSize;*/

			// find the game's dmd position in VPM's registry
			if (_config.VirtualDmd.UseRegistryPosition) {
				try {
					var regPath = @"Software\Freeware\Visual PinMame\" + _gameName;
					var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
					key = key.OpenSubKey(regPath);

					if (key == null) {
						// couldn't find the value in the 32-bit view so grab the 64-bit view
						key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
						key = key.OpenSubKey(regPath);
					}

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

			//_dmd.Dmd.Init();
			_dmd.Show();
		}

		/// <summary>
		/// Sets the position of the DMD as defined in the .ini file.
		/// </summary>
		private void SetVirtualDmdDefaultPosition(double x = -1d, double y = -1d, double width = -1d, double height = -1d)
		{
			var aspectRatio = _dmd.Width / _dmd.Height;
			_dmd.Left = _config.VirtualDmd.HasGameOverride("left") || x < 0 ? _config.VirtualDmd.Left : x;
			_dmd.Top = _config.VirtualDmd.HasGameOverride("top") || y < 0 ? _config.VirtualDmd.Top : y;
			_dmd.Width = _config.VirtualDmd.HasGameOverride("width") || width < 0 ? _config.VirtualDmd.Width : width;
			if (_config.VirtualDmd.IgnoreAr) {
				_dmd.Height = _config.VirtualDmd.HasGameOverride("height") || height < 0 ? _config.VirtualDmd.Height : height;
			} else {
				_dmd.Height = _dmd.Width / aspectRatio;
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
				_dmd?.Dispatcher.Invoke(() => {
					_dmd.Hide();
				});
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Could not hide DMD because task was already canceled.");
			}

			_color = RenderGraph.DefaultColor;
			_palette = null;
			_gray2Colorizer = null;
			_gray4Colorizer = null;
			_coloring = null;
			_isOpen = false;
		}

		public void SetGameName(string gameName)
		{
			Logger.Info("Setting game name: {0}", gameName);
			_gameName = gameName;
			_config.GameName = gameName;

			SetupColorizer();
		}

		public void SetColorize(bool colorize)
		{
			Logger.Info("{0} game colorization", colorize ? "Enabling" : "Disabling");
			_colorize = colorize;
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
		}

		public void RenderGray2(int width, int height, byte[] frame)
		{
			if (!_isOpen) {
				Init();
			}
			if (_gray2Colorizer != null && width == 128 && height == 16 && _gray2Colorizer.Has128x32Animation)
			{
				// Pin2DMD colorization may have 512 byte masks with a 128x16 source, 
				// indicating this should be upsized and treated as a centered 128x32 DMD.
				height *= 2;
				_gray2Colorizer.SetDimensions(width, height);
				if (_upsizedFrame == null)
					_upsizedFrame = new byte[width * height];
				Buffer.BlockCopy(frame, 0, _upsizedFrame, 8*width, frame.Length);
				_vpmGray2Source.NextFrame(width, height, _upsizedFrame);
			}
			else
			{
				_gray2Colorizer?.SetDimensions(width, height);
				_gray4Colorizer?.SetDimensions(width, height);
				_vpmGray2Source.NextFrame(width, height, frame);
			}
		}

		public void RenderGray4(int width, int height, byte[] frame)
		{
			if (!_isOpen) {
				Init();
			}
			_gray2Colorizer?.SetDimensions(width, height);
			_gray4Colorizer?.SetDimensions(width, height);
			_vpmGray4Source.NextFrame(width, height, frame);
		}

		public void RenderRgb24(int width, int height, byte[] frame)
		{
			if (!_isOpen) {
				Init();
			}
			_vpmRgb24Source.NextFrame(width, height, frame);
		}

		public void RenderAlphaNumeric(NumericalLayout layout, ushort[] segData, ushort[] segDataExtended)
		{
			if (!_isOpen) {
				Init();
			}
			_vpmAlphaNumericSource.NextFrame(new AlphaNumericFrame(layout, segData, segDataExtended));

			//Logger.Info("Alphanumeric: {0}", layout);
			switch (layout) {
				case NumericalLayout.__2x16Alpha:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x16Alpha(segData));
					break;
				case NumericalLayout.None:
				case NumericalLayout.__2x20Alpha:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x20Alpha(segData));
					break;
				case NumericalLayout.__2x7Alpha_2x7Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Alpha_2x7Num(segData));
					break;
				case NumericalLayout.__2x7Alpha_2x7Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Alpha_2x7Num_4x1Num(segData));
					break;
				case NumericalLayout.__2x7Num_2x7Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_2x7Num_4x1Num(segData));
					break;
				case NumericalLayout.__2x7Num_2x7Num_10x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_2x7Num_10x1Num(segData, segDataExtended));
					break;
				case NumericalLayout.__2x7Num_2x7Num_4x1Num_gen7:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_2x7Num_4x1Num_gen7(segData));
					break;
				case NumericalLayout.__2x7Num10_2x7Num10_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num10_2x7Num10_4x1Num(segData));
					break;
				case NumericalLayout.__2x6Num_2x6Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x6Num_2x6Num_4x1Num(segData));
					break;
				case NumericalLayout.__2x6Num10_2x6Num10_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x6Num10_2x6Num10_4x1Num(segData));
					break;
				case NumericalLayout.__4x7Num10:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render4x7Num10(segData));
					break;
				case NumericalLayout.__6x4Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render6x4Num_4x1Num(segData));
					break;
				case NumericalLayout.__2x7Num_4x1Num_1x16Alpha:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_4x1Num_1x16Alpha(segData));
					break;
				case NumericalLayout.__1x16Alpha_1x16Num_1x7Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render1x16Alpha_1x16Num_1x7Num(segData));
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
			Raygun.ApplicationVersion = LibDmd.Version.AssemblyInformationalVersionAttribute;
			Raygun.Send(ex, 
				new List<string> { System.Diagnostics.Process.GetCurrentProcess().ProcessName }, 
				new Dictionary<string, string> { {"log", string.Join("\n", MemLogger.Logs) } }
			);
#endif
		}

		private static string GetColorPath()
		{
			// first, try executing assembly.
			var altcolor = Path.Combine(AssemblyPath, "altcolor");
			if (Directory.Exists(altcolor)) {
				Logger.Info("Determined color path from assembly path: {0}", altcolor);
				return altcolor;
			}

			// then, try vpinmame location
			var vpmPath = GetDllPath("VPinMAME.dll");
			if (vpmPath == null) {
				return null;
			}
			altcolor = Path.Combine(Path.GetDirectoryName(vpmPath), "altcolor");
			if (Directory.Exists(altcolor)) {
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
