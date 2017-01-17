using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using DmdExt.Common;
using LibDmd;
using LibDmd.Converter;
using LibDmd.Converter.Colorize;
using LibDmd.Input.PinMame;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using Mindscape.Raygun4Net;
using NLog;
using NLog.Targets;
using static System.Windows.Threading.Dispatcher;

namespace PinMameDevice
{
	/// <summary>
	/// Hiä isch d Haiptlogik fir d <c>DmdDevice.dll</c> fir VPinMAME.
	/// </summary>
	/// <seealso cref="DmdDevice">Vo det chemid d Datä übr VPinMAME</seealso>
	public class DmdExt
	{
		private const int Width = 128;
		private const int Height = 32;

		private readonly Configuration _config = new Configuration();
		private readonly VpmGray2Source _vpmGray2Source = new VpmGray2Source();
		private readonly VpmGray4Source _vpmGray4Source = new VpmGray4Source();
		private readonly VpmRgb24Source _vpmRgb24Source = new VpmRgb24Source();
		private readonly RenderGraphCollection _graphs = new RenderGraphCollection();
		private VirtualDmd _dmd;

		// Ziigs vo VPM
		private string _gameName;
		private bool _colorize;
		private Color _color = RenderGraph.DefaultColor;

		// Iifärbigsziig
		private Color[] _palette;
		private Gray2Colorizer _gray2Colorizer;
		private Gray4Colorizer _gray4Colorizer;

		// Wärchziig
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static readonly string AssemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
		private static string _altcolorPath;
		private static readonly MemoryTarget MemLogger = new MemoryTarget();

		public DmdExt()
		{
			MemLogger.Layout = "${pad:padding=4:inner=[${threadid}]} ${date} ${pad:padding=5:inner=${level:uppercase=true}} | ${message} ${exception:format=ToString}";
			MemLogger.Name = "Raygun Logger";

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			var logConfigPath = Path.Combine(AssemblyPath, "DmdDevice.log.config");
			if (File.Exists(logConfigPath)) {
				LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(logConfigPath, true);
				LogManager.Configuration.AddTarget(MemLogger);

			} else {
				NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(MemLogger, LogLevel.Debug);

			}
			_altcolorPath = GetColorPath();
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
			_gray2Colorizer = null;
			_gray4Colorizer = null;

			if (_config.Global.Colorize && _altcolorPath != null) {
				var palPath1 = Path.Combine(_altcolorPath, _gameName, _gameName + ".pal");
				var palPath2 = Path.Combine(_altcolorPath, _gameName, "pin2dmd.pal");
				var fsqPath1 = Path.Combine(_altcolorPath, _gameName, _gameName + ".fsq");
				var fsqPath2 = Path.Combine(_altcolorPath, _gameName, "pin2dmd.fsq");
				
				var palPath = File.Exists(palPath1) ? palPath1 : palPath2;
				var fsqPath = File.Exists(fsqPath1) ? fsqPath1 : fsqPath2;

				if (File.Exists(palPath)) {
					try {
						Logger.Info("Loading palette file at {0}...", palPath);
						var coloring = new Coloring(palPath);
						Animation[] animations = null;

						if (File.Exists(fsqPath)) {
							Logger.Info("Loading animation file at {0}...", fsqPath);
							animations = Animation.ReadFrameSequence(fsqPath, Width, Height);
						}
						_gray2Colorizer = new Gray2Colorizer(coloring, animations);
						_gray4Colorizer = new Gray4Colorizer(coloring, animations);

					} catch (Exception e) {
						Logger.Warn(e, "Error initializing colorizer: {0}", e.Message);
					}
				} else {
					Logger.Debug("No palette file found at {0}.", palPath);
				}
			} else {
				Logger.Info("Bit-convertion disabled.");
			}

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

		/// <summary>
		/// Tuät ä nii Inschantz vom virtueuä DMD kreiärä und tuät drnah d 
		/// Render-Graphä drabindä.
		/// </summary>
		private void CreateVirtualDmd()
		{
			var thread = new Thread(() => {

				_dmd = new VirtualDmd();
				SetupGraphs();

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));

				// When the window closes, shut down the dispatcher
				_dmd.Closed += (s, e) => CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
				_dmd.Dispatcher.Invoke(SetupVirtualDmd);

				// Start the Dispatcher Processing
				Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		/// <summary>
		/// Kreiärt ä Render-Graph fir jedä Input-Tip und bindet si as 
		/// virtueuä DMD.
		/// </summary>
		private void SetupGraphs()
		{
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
				var pin2Dmd = Pin2Dmd.GetInstance();
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");					
				}
			}
			if (_config.VirtualDmd.Enabled) {
				renderers.Add(_dmd.Dmd);
				Logger.Info("Added VirtualDMD renderer.");
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

			if (_colorize && (_gray2Colorizer != null || _gray4Colorizer != null)) {
				Logger.Info("Just clearing palette, colorization is done by converter.");
				_graphs.ClearColor();

			} else if (_colorize && _palette != null) {
				Logger.Info("Applying palette to render graphs.");
				_graphs.ClearColor();
				_graphs.SetPalette(_palette);

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
			_dmd.Left = _config.VirtualDmd.Left;
			_dmd.Top = _config.VirtualDmd.Top;
			_dmd.Width = _config.VirtualDmd.Width;
			_dmd.Height = _config.VirtualDmd.Height;
			_dmd.AlwaysOnTop = _config.VirtualDmd.StayOnTop;
			_dmd.GripColor = _config.VirtualDmd.HideGrip ? Brushes.Transparent : Brushes.White;
			_dmd.Dmd.Init();
			_dmd.Show();
		}

		/// <summary>
		/// Tuät aui Renderer ahautä unds virtueua DMD vrschteckä.
		/// </summary>
		public void Close()
		{
			Logger.Info("Closing up.");
			_graphs.Dispose();
			_dmd?.Dispatcher.Invoke(() => {
				_dmd.Hide();
			});

			_color = RenderGraph.DefaultColor;
			_palette = null;
			_gray2Colorizer = null;
			_gray4Colorizer = null;
		}

		public void SetGameName(string gameName)
		{
			Logger.Info("Setting game name: {0}", gameName);
			_gameName = gameName;
			_config.GameName = gameName;
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
		public void SetPalette(Color[] colors) {
			Logger.Info("Setting palette to {0} colors...", colors.Length);
			_palette = colors;
		}

		public void RenderGray2(int width, int height, byte[] frame)
		{
			_gray2Colorizer?.SetDimensions(width, height);
			_gray4Colorizer?.SetDimensions(width, height);
			_vpmGray2Source.NextFrame(width, height, frame);
		}

		public void RenderGray4(int width, int height, byte[] frame)
		{
			_gray2Colorizer?.SetDimensions(width, height);
			_gray4Colorizer?.SetDimensions(width, height);
			_vpmGray4Source.NextFrame(width, height, frame);
		}

		public void RenderRgb24(int width, int height, byte[] frame)
		{
			_vpmRgb24Source.NextFrame(width, height, frame);
		}

		public void RenderAlphaNumeric(DmdDevice.NumericalLayout layout, ushort[] segData, ushort[] segDataExtended)
		{
			//Logger.Info("Alphanumeric: {0}", layout);
			switch(layout)
			{
				case DmdDevice.NumericalLayout.None:
					break;
				case DmdDevice.NumericalLayout.__2x16Alpha:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x16Alpha(segData));
					break;
				case DmdDevice.NumericalLayout.__2x20Alpha:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x20Alpha(segData));
					break;
				case DmdDevice.NumericalLayout.__2x7Alpha_2x7Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Alpha_2x7Num(segData));
					break;
				case DmdDevice.NumericalLayout.__2x7Alpha_2x7Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Alpha_2x7Num_4x1Num(segData));
					break;
				case DmdDevice.NumericalLayout.__2x7Num_2x7Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_2x7Num_4x1Num(segData));
					break;
				case DmdDevice.NumericalLayout.__2x7Num_2x7Num_10x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_2x7Num_10x1Num(segData, segDataExtended));
					break;
				case DmdDevice.NumericalLayout.__2x7Num_2x7Num_4x1Num_gen7:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_2x7Num_4x1Num_gen7(segData));
					break;
				case DmdDevice.NumericalLayout.__2x7Num10_2x7Num10_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num10_2x7Num10_4x1Num(segData));
					break;
				case DmdDevice.NumericalLayout.__2x6Num_2x6Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x6Num_2x6Num_4x1Num(segData));
					break;
				case DmdDevice.NumericalLayout.__2x6Num10_2x6Num10_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x6Num10_2x6Num10_4x1Num(segData));
					break;
				case DmdDevice.NumericalLayout.__4x7Num10:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render4x7Num10(segData));
					break;
				case DmdDevice.NumericalLayout.__6x4Num_4x1Num:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render6x4Num_4x1Num(segData));
					break;
				case DmdDevice.NumericalLayout.__2x7Num_4x1Num_1x16Alpha:
					_vpmGray2Source.NextFrame(Width, Height, AlphaNumeric.Render2x7Num_4x1Num_1x16Alpha(segData));
					break;
				case DmdDevice.NumericalLayout.__1x16Alpha_1x16Num_1x7Num:
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
			Raygun.Send(ex, null, new Dictionary<string, string> { {"log", string.Join("\n", MemLogger.Logs) } });
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
