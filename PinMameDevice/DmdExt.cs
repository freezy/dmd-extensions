using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using DmdExt.Common;
using LibDmd;
using LibDmd.Common;
using LibDmd.Converter;
using LibDmd.Converter.Colorize;
using LibDmd.Input;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using Mindscape.Raygun4Net;
using NLog;
using static System.Windows.Threading.Dispatcher;

namespace PinMameDevice
{
	/// <summary>
	/// Hiä isch d Haiptlogik fir d <c>DmdDevice.dll</c> fir VPinMAME.
	/// </summary>
	/// <seealso cref="DmdDevice">Vo det chemid d Datä übr VPinMAME</seealso>
	public class DmdExt
	{
		private static readonly int Width = 128;
		private static readonly int Height = 32;
		private static readonly Color DefaultColor = Colors.OrangeRed;

		private readonly Configuration _config = new Configuration();
		private readonly PassthroughSource _source = new PassthroughSource();
		private readonly List<RenderGraph> _graphs = new List<RenderGraph>();
		private readonly List<IDisposable> _renderers = new List<IDisposable>();
		private VirtualDmd _dmd;

		// Ziigs vo VPM
		private string _gameName;
		private bool _colorize;
		private Color _color = DefaultColor;

		// Iifärbigsziig
		private Color[] _palette;
		private Gray2Colorizer _gray2Colorizer;
		private Gray4Colorizer _gray4Colorizer;

		// Wärchziig
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");
		private static readonly string AssemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

		public DmdExt()
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			var logConfigPath = Path.Combine(AssemblyPath, "DmdDevice.log.config");
			if (File.Exists(logConfigPath)) {
				LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(logConfigPath, true);
			}
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
			var palPath = Path.Combine(AssemblyPath, "altcolor", _gameName, "pin2dmd.pal");
			var fsqPath = Path.Combine(AssemblyPath, "altcolor", _gameName, "pin2dmd.fsq");
			_gray2Colorizer = null;
			_gray4Colorizer = null;

			if (File.Exists(palPath)) {
				try {
					Logger.Info("Loading palette file at {0}...", palPath);
					var coloring = new Coloring(palPath);
					Animation[] animations = null;

					if (File.Exists(fsqPath)) {
						Logger.Info("Loading animation file at {0}...", fsqPath);
						animations = Animation.ReadFrameSequence(fsqPath, Width, Height);
					}
					_gray2Colorizer = new Gray2Colorizer(Width, Height, coloring, animations);
					_gray4Colorizer = new Gray4Colorizer(Width, Height, coloring, animations);

				} catch (Exception e) {
					Logger.Warn("Error initializing colorizer: {0}", e.Message);
				}
			} else {
				Logger.Debug("No palette file found at {0}.", palPath);
			}

			if (_config.VirtualDmd.Enabled) {
				if (_dmd == null) {
					Logger.Info("Opening virtual DMD...");
					CreateVirtualDmd();

				} else {
					_dmd.Dispatcher.Invoke(() => {
						SetupGraphs();
						_dmd.Dmd.Init();
						_dmd.Show();
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
				_dmd.Dispatcher.Invoke(() => {
					_dmd.Left = _config.VirtualDmd.Left;
					_dmd.Top = _config.VirtualDmd.Top;
					_dmd.Width = _config.VirtualDmd.Width;
					_dmd.Height = _config.VirtualDmd.Height;
					_dmd.AlwaysOnTop = _config.VirtualDmd.StayOnTop;
					_dmd.GripColor = _config.VirtualDmd.HideGrip ? Brushes.Transparent : Brushes.White;
					_dmd.Dmd.Init();
					_dmd.Show();
				});

				_dmd.PositionChanged
					.Throttle(TimeSpan.FromSeconds(1))
					.Subscribe(pos => _config.VirtualDmd.SetPosition(pos.Left, pos.Top, pos.Width, pos.Height));

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

			// miär bruichid äi Render-Graph fir jedi Bitlängi
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = renderers,
				Converter = _gray2Colorizer,
				RenderAs = RenderBitLength.Gray2,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically =  _config.Global.FlipVertically
			});
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = renderers,
				Converter = _gray4Colorizer,
				RenderAs = RenderBitLength.Gray4,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically =  _config.Global.FlipVertically
			});
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = renderers,
				RenderAs = RenderBitLength.Rgb24,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically =  _config.Global.FlipVertically
			});

			// ReSharper disable once ForCanBeConvertedToForeach
			for (var i = 0; i < _renderers.Count; i++) {
				var rgb24Renderer = _renderers[i] as IRgb24Destination;
				if (rgb24Renderer == null) {
					continue;
				}
				if (_colorize && (_gray2Colorizer != null || _gray4Colorizer != null)) {
					Logger.Info("Just clearing palette, colorization is done by converter.");
					rgb24Renderer.ClearColor();

				} else if (_colorize && _palette != null) {
					Logger.Info("Applying palette to DMD...");
					rgb24Renderer.ClearColor();
					rgb24Renderer.SetPalette(_palette);

				} else {
					Logger.Info("Applying color to DMD...");
					rgb24Renderer.ClearPalette();
					rgb24Renderer.SetColor(_color);	
				}
			}

			_graphs.ForEach(graph => _renderers.Add(graph.StartRendering()));
		}

		/// <summary>
		/// Tuät aui Renderer ahautä unds virtueua DMD vrschteckä.
		/// </summary>
		public void Close()
		{
			Logger.Info("Closing up.");
			_renderers.ForEach(r => r.Dispose());
			_renderers.Clear();
			_graphs.ForEach(graph => graph.Dispose());
			_graphs.Clear();
			_dmd?.Dispatcher.Invoke(() => {
				_dmd.Hide();
			});

			_color = DefaultColor;
			_palette = null;
			_gray4Colorizer = null;
		}

		public void SetGameName(string gameName)
		{
			Logger.Info("Setting game name: {0}", gameName);
			_gameName = gameName;
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
			_source.SetDimensions(width, height);
			_source.FramesGray2.OnNext(frame);
		}

		public void RenderGray4(int width, int height, byte[] frame)
		{
			_source.SetDimensions(width, height);
			_source.FramesGray4.OnNext(frame);
		}

		public void RenderRgb24(int width, int height, byte[] frame)
		{
			_source.SetDimensions(width, height);
			_source.FramesRgb24.OnNext(frame);
		}

		public void RenderAlphaNumeric(DmdDevice.NumericalLayout layout, byte[] segData, byte[] segDataExtended)
		{
			Logger.Info("Alphanumeric: {0}", layout);
			switch(layout)
			{
				case DmdDevice.NumericalLayout.None:
					break;
				case DmdDevice.NumericalLayout.__2x16Alpha:
					break;
				case DmdDevice.NumericalLayout.__2x20Alpha:
					break;
				case DmdDevice.NumericalLayout.__2x7Alpha_2x7Num:
					break;
				case DmdDevice.NumericalLayout.__2x7Alpha_2x7Num_4x1Num:
					break;
				case DmdDevice.NumericalLayout.__2x7Num_2x7Num_4x1Num:
					break;
				case DmdDevice.NumericalLayout.__2x7Num_2x7Num_10x1Num:
					break;
				case DmdDevice.NumericalLayout.__2x7Num_2x7Num_4x1Num_gen7:
					break;
				case DmdDevice.NumericalLayout.__2x7Num10_2x7Num10_4x1Num:
					var planes = FrameUtil.Copy(128, 32, AlphaNumeric._2x7Num10_2x7Num10_4x1Num(segData), 2, 4);
					_source.FramesGray2.OnNext(FrameUtil.Join(128, 32, planes));
					break;
				case DmdDevice.NumericalLayout.__2x6Num_2x6Num_4x1Num:
					break;
				case DmdDevice.NumericalLayout.__2x6Num10_2x6Num10_4x1Num:
					break;
				case DmdDevice.NumericalLayout.__4x7Num10:
					break;
				case DmdDevice.NumericalLayout.__6x4Num_4x1Num:
					break;
				case DmdDevice.NumericalLayout.__2x7Num_4x1Num_1x16Alpha:
					break;
				case DmdDevice.NumericalLayout.__1x16Alpha_1x16Num_1x7Num:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layout), layout, null);
			}
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var ex = e.ExceptionObject as Exception;
			if (ex != null)
			{
				Logger.Error(ex.Message);
				Logger.Error(ex.StackTrace);
			}
			Raygun.Send(ex);
		}
	}
}
