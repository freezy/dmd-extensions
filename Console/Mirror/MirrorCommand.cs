using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Media;
using DmdExt.Common;
using LibDmd;
using LibDmd.Converter;
using LibDmd.Converter.Vni;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Input.FutureDmd;
using LibDmd.Input.PinballFX;
using LibDmd.Input.ProPinball;
using LibDmd.Input.ScreenGrabber;
using LibDmd.Input.TPAGrabber;
using LibDmd.Output;
using LibDmd.Output.FileOutput;
using LibDmd.Processor;

namespace DmdExt.Mirror
{
	class MirrorCommand : BaseCommand
	{
		private readonly MirrorOptions _options;
		private readonly IConfiguration _config;
		private ColorizationLoader _colorizationLoader;
		private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
		private List<IDestination> _renderers;

		public MirrorCommand(IConfiguration config, MirrorOptions options)
		{
			_config = config;
			_options = options;
			if (_options.SkipAnalytics) {
				Analytics.Instance.Disable();
			}
		}

		private RenderGraph CreateGraph(ISource source, string name, HashSet<string> reportingTags)
		{
			if (_renderers == null) {
				_renderers = GetRenderers(_config, reportingTags);
			}
			var graph = new RenderGraph {
				Name = name,
				Source = source,
				Destinations = _renderers,
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically,
				IdleAfter = _options.IdleAfter,
				IdlePlay = _options.IdlePlay
			};
			graph.SetColor(_config.Global.DmdColor);

			return graph;
		}

		protected override void CreateRenderGraphs(RenderGraphCollection graphs, HashSet<string> reportingTags)
		{
			// setup source and additional processors
			switch (_options.Source) {

				case SourceType.PinballFX2: {
					reportingTags.Add("In:PinballFX2");
					Analytics.Instance.SetSource("Pinball FX2");
					graphs.Add(CreateGraph(new PinballFX2Grabber { FramesPerSecond = _options.FramesPerSecond }, "Pinball FX2 Render Graph", reportingTags));
					break;
				}

				case SourceType.PinballFX3: {
					if (_options.Fx3GrabScreen) {
						reportingTags.Add("In:PinballFX3Legacy");
						Analytics.Instance.SetSource("Pinball FX3 (legacy)");
						graphs.Add(CreateGraph(new PinballFX3Grabber { FramesPerSecond = _options.FramesPerSecond }, "Pinball FX3 (legacy) Render Graph", reportingTags));
					} else {
						reportingTags.Add("In:PinballFX3"); // analytics done when game name is known
						graphs.Add(CreateGraph(new PinballFX3MemoryGrabber { FramesPerSecond = _options.FramesPerSecond }, "Pinball FX3 Render Graph", reportingTags));
					}
					break;
				}

				case SourceType.PinballArcade: {
					reportingTags.Add("In:PinballArcade"); // analytics done when game name is known
					var tpaGrabber = new TPAGrabber { FramesPerSecond = _options.FramesPerSecond };
					graphs.Add(CreateGraph(tpaGrabber.Gray2Source, "Pinball Arcade (2-bit) Render Graph", reportingTags));
					graphs.Add(CreateGraph(tpaGrabber.Gray4Source, "Pinball Arcade (4-bit) Render Graph", reportingTags));
					break;
				}

				case SourceType.ProPinball: {
					reportingTags.Add("In:ProPinball");
					Analytics.Instance.SetSource("Pro Pinball", "Timeshock");
					graphs.Add(CreateGraph(new ProPinballSlave(_options.ProPinballArgs), "Pro Pinball Render Graph", reportingTags));
					break;
				}

				case SourceType.Screen:
					var grabber = new ScreenGrabber {
						FramesPerSecond = _options.FramesPerSecond,
						Left = _options.Position[0],
						Top = _options.Position[1],
						Width = _options.Position[2] - _options.Position[0],
						Height = _options.Position[3] - _options.Position[1],
						DestinationDimensions = new Dimensions(_options.ResizeTo[0], _options.ResizeTo[1])
					};
					if (_options.GridSpacing > 0) {
						grabber.Processors.Add(new GridProcessor {
							Width = _options.ResizeTo[0],
							Height = _options.ResizeTo[1],
							Spacing = _options.GridSpacing
						});
					}

					reportingTags.Add("In:ScreenGrab");
					Analytics.Instance.SetSource("Screen Grabber");
					graphs.Add(CreateGraph(grabber, "Screen Grabber Render Graph", reportingTags));
					break;

				case SourceType.FuturePinball:
					reportingTags.Add("In:FutureDmdSink");
					Analytics.Instance.SetSource("Future Pinball");
					graphs.Add(CreateGraph(new FutureDmdSink(_options.FramesPerSecond), "Future Pinball Render Graph", reportingTags));
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			// if colorization enabled, subscribe to name changes to re-load colorizer.
			if (_config.Global.Colorize) {
				foreach (var graph in graphs.Graphs) {
					if (!(graph.Source is IGameNameSource gameNameSource)) {
						Analytics.Instance.SetSource(graph.Source.Name);
						Analytics.Instance.StartGame(); // send now, since we won't get a game name
						continue;
					}

					if (_colorizationLoader == null) {
						_colorizationLoader = new ColorizationLoader();
						graphs.ClearColor();
					}
					
					var converter = new SwitchingConverter();
					graph.Converter = converter;

					_subscriptions.Add(gameNameSource.GetGameName().Subscribe(name => {
						converter.Switch(SetupColorizer(name));
					}));

					if (graph.Source is IDmdColorSource dmdColorSource) {
						dmdColorSource.GetDmdColor().Subscribe(color => {
							converter.SetColor(color);
						});
					}
				}
				
				// analytics
				var g = graphs.Graphs.FirstOrDefault();
				if (g != null) {
					if (g.Source is IGameNameSource s) {
						_subscriptions.Add(s.GetGameName().Subscribe(name => {
							Analytics.Instance.SetSource(g.Source.Name, name);
							Analytics.Instance.StartGame();
						}));
						
					} else {
						Analytics.Instance.SetSource(g.Source.Name);
						Analytics.Instance.StartGame(); // send now, since we won't get a game name
					}
				}
				
			} else {

				// When not colorizing, subscribe to DMD color changes to inform the graph.
				var colorSub = graphs.Graphs
					.Select(g => g.Source as IDmdColorSource)
					.FirstOrDefault(s => s != null)
					?.GetDmdColor()
					.Subscribe(graphs.SetColor);
				if (colorSub != null) {
					_subscriptions.Add(colorSub);
				}
				
				// print game names & analytics
				var graph = graphs.Graphs.FirstOrDefault();
				if (graph != null) {
					if (graph.Source is IGameNameSource s) {
						var nameSub = s.GetGameName().Subscribe(name => {
							Logger.Info($"New game detected at {graph.Source.Name}: {name}");
							Analytics.Instance.SetSource(graph.Source.Name, name);
							Analytics.Instance.StartGame();
						});
						_subscriptions.Add(nameSub);
					} else {
						Analytics.Instance.SetSource(graph.Source.Name);
						Analytics.Instance.StartGame(); // send now, since we won't get a game name
					}
				}
			}

			// raw output
			if (_config.RawOutput.Enabled) {
				var graph = graphs.Graphs.FirstOrDefault();
				if (graph?.Source is IGameNameSource s) {
					var rawOutput = graph.Destinations.OfType<RawOutput>().FirstOrDefault();
					if (rawOutput != null) {
						var nameSub = s.GetGameName().Subscribe(name => {
							rawOutput.SetGameName(name);
						});
						_subscriptions.Add(nameSub);

					} else {
						Logger.Warn("Cannot enable raw output due to missing RawOutput destination.");
					}
				}
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			_subscriptions.Dispose();
		}

		private AbstractConverter SetupColorizer(string gameName)
		{
			// only setup if enabled and path is set
			if (!_config.Global.Colorize || _colorizationLoader == null || gameName == null) {
				Analytics.Instance.ClearColorizer();
				return null;
			}

			// 1. check for serum
			var serumColorizer = _colorizationLoader.LoadSerum(gameName, _config.Global.ScalerMode);
			if (serumColorizer != null) {
				return serumColorizer;
			}

			// 2. check for plugins
			var pluginColorizer = _colorizationLoader.LoadPlugin(_config.Global.Plugins, true, gameName, Colors.OrangeRed, null);
			if (pluginColorizer != null) {
				return pluginColorizer;
			}

			// 3. check for native pin2color
			return _colorizationLoader.LoadVniColorizer(gameName, _config.Global.ScalerMode, _config.Global.VniKey);
		}
	}
}
