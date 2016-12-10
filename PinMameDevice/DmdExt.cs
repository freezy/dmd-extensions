using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using DmdExt.Common;
using LibDmd;
using LibDmd.Output;
using LibDmd.Processor.Coloring;
using Mindscape.Raygun4Net;
using NLog;
using static System.Windows.Threading.Dispatcher;

namespace PinMameDevice
{
	/// <summary>
	/// Hiä isch d Haiptlogik fir d <c>DmdDevice.dll</c> vom VPinMAME.
	/// </summary>
	/// <remarks>
	/// Im Momänt untrstitzts numä s virtueuä DMD, wobi schpätr ai diä
	/// ächtä drzuä chemid.
	/// </remarks>
	/// <seealso cref="DmdDevice">Vo det chemid d Datä übr VPinMAME</seealso>
	public class DmdExt
	{
		private static readonly Color DefaultColor = Colors.OrangeRed;

		private readonly PinMameSource _source = new PinMameSource();
		private readonly List<RenderGraph> _graphs = new List<RenderGraph>();
		private readonly List<IDisposable> _renderers = new List<IDisposable>();
		private VirtualDmd _dmd;

		// Ziigs vo VPM
		private string _gameName;
		private bool _colorize;
		private Color _color = DefaultColor;
		private Color[] _palette;
		private Coloring _paletteConfig;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");

		public DmdExt()
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
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
			var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
			var palettePath = Path.Combine(assemblyPath, "altcolor", _gameName, "pin2dmd.pal");

			if (File.Exists(palettePath)) {
				Logger.Info("Loading palette file at {0}...", palettePath);
				try {
					_paletteConfig = new Coloring(palettePath);
					if (_paletteConfig.Palettes.Length == 1) {
						Logger.Info("Only one palette found, applying...");
						_palette = _paletteConfig.Palettes[0].Colors;
					}
				} catch (Exception e) {
					Logger.Warn("Error loading palette: {0}", e.Message);
				}
			} else {
				Logger.Debug("No palette file found at {0}.", palettePath);
			}

			if (_dmd == null) {
				Logger.Info("Opening virtual DMD...");
				CreateVirtualDmd();

			} else {
				_dmd.Dispatcher.Invoke(() => {
					SetupGraphs();
					_dmd.Show();
				});
			}
		}

		/// <summary>
		/// Wird uifgriäft wenn vom modifiziärtä ROM ibärä Sitäkanau ä Palettäwächsu
		/// ah gä wird.
		/// </summary>
		/// <param name="num">Weli Palettä muäss gladä wärdä</param>
		public void LoadPalette(uint num)
		{

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
					_dmd.Show();
				});

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
			var dest = new List<IFrameDestination> { _dmd.Dmd };

			// miär bruichid äi Render-Graph fir jedi Bitlängi
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = dest,
				RenderAs = RenderBitLength.Gray2
			});
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = dest,
				RenderAs = RenderBitLength.Gray4
			});
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = dest,
				RenderAs = RenderBitLength.Rgb24
			});
			
			if (_colorize && _palette != null) {
				Logger.Info("Applying palette to DMD...");
				_dmd.Dmd.ClearColor();
				_dmd.Dmd.SetPalette(_palette);
			} else {
				Logger.Info("Applying color to DMD...");
				_dmd.Dmd.ClearPalette();
				_dmd.Dmd.SetColor(_color);	
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
			_source.FramesGray2.OnNext(frame);
		}

		public void RenderGray4(int width, int height, byte[] frame)
		{
			_source.FramesGray4.OnNext(frame);
		}

		public void RenderRgb24(int width, int height, byte[] frame)
		{
			_source.FramesRgb24.OnNext(frame);
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var ex = e.ExceptionObject as Exception;
			if (ex != null) {
				Logger.Error(ex.Message);
				Logger.Error(ex.StackTrace);
			}
			Raygun.Send(ex);
		}
	}
}
