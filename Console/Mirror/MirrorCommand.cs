using System;
using System.Collections.Generic;
using DmdExt.Common;
using LibDmd;
using LibDmd.Converter;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Input.FutureDmd;
using LibDmd.Input.PinballFX;
using LibDmd.Input.ProPinball;
using LibDmd.Input.ScreenGrabber;
using LibDmd.Input.TPAGrabber;
using LibDmd.Processor;

namespace DmdExt.Mirror
{
	class MirrorCommand : BaseCommand
	{
		private readonly MirrorOptions _options;
		private readonly IConfiguration _config;
		private ColorizationLoader _colorizationLoader;
		private IDisposable _nameSubscription;
		private IDisposable _dmdColorSubscription;

		public MirrorCommand(IConfiguration config, MirrorOptions options)
		{
			_config = config;
			_options = options;
		}

		private RenderGraph CreateGraph(ISource source, HashSet<string> reportingTags)
		{
			var graph = new RenderGraph {
				Source = source,
				Destinations = GetRenderers(_config, reportingTags),
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
					graphs.Add(CreateGraph(new PinballFX2Grabber { FramesPerSecond = _options.FramesPerSecond }, reportingTags));
					break;
				}

				case SourceType.PinballFX3: {
					if (_options.Fx3GrabScreen) {
						reportingTags.Add("In:PinballFX3Legacy");
						graphs.Add(CreateGraph(new PinballFX3Grabber { FramesPerSecond = _options.FramesPerSecond }, reportingTags));
						
					} else {
						reportingTags.Add("In:PinballFX3");
						var memoryGrabber = new PinballFX3MemoryGrabber { FramesPerSecond = _options.FramesPerSecond };
						var graph = CreateGraph(memoryGrabber, reportingTags);

						var latest = new SwitchingConverter();
						graph.Converter = latest;

						_dmdColorSubscription = memoryGrabber.DmdColor.Subscribe(color => { latest.DefaultColor = color; });

						if (_config.Global.Colorize) {
							_colorizationLoader = new ColorizationLoader();
							var nameGrabber = new PinballFX3GameNameMemoryGrabber();
							_nameSubscription = nameGrabber.GetFrames().Subscribe(name => { latest.Switch(LoadColorizer(name)); });
						}
						graphs.Add(graph);
					}
					break;
				}

				case SourceType.PinballArcade: {
					reportingTags.Add("In:PinballArcade");
					graphs.Add(CreateGraph(new TPAGrabber { FramesPerSecond = _options.FramesPerSecond }, reportingTags));
					break;
				}

				case SourceType.ProPinball: {
					reportingTags.Add("In:ProPinball");
					graphs.Add(CreateGraph(new ProPinballSlave(_options.ProPinballArgs), reportingTags));
					break;
				}

				case SourceType.Screen:

					var grabber = new ScreenGrabber {
						FramesPerSecond = _options.FramesPerSecond,
						Left = _options.Position[0],
						Top = _options.Position[1],
						Width = _options.Position[2] - _options.Position[0],
						Height = _options.Position[3] - _options.Position[1],
						DestinationWidth = _options.ResizeTo[0],
						DestinationHeight = _options.ResizeTo[1]
					};
					if (_options.GridSpacing > 0) {
						grabber.Processors.Add(new GridProcessor {
							Width = _options.ResizeTo[0],
							Height = _options.ResizeTo[1],
							Spacing = _options.GridSpacing
						});
					}

					reportingTags.Add("In:ScreenGrab");
					graphs.Add(CreateGraph(grabber, reportingTags));
					break;

				case SourceType.FuturePinball:
					reportingTags.Add("In:FutureDmdSink");
					graphs.Add(CreateGraph(new FutureDmdSink(_options.FramesPerSecond), reportingTags));
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			foreach (var graph in graphs.Graphs)
			{
				if (!_config.Global.Colorize || !(graph.Source is IGameNameSource gameNameSource)) {
					continue;
				}

				var latest = new SwitchingConverter();
				graph.Converter = latest;
				_colorizationLoader = new ColorizationLoader();
				_nameSubscription = gameNameSource.GetGameName().Subscribe(name => {
					if (name != null) {
						latest.Switch(LoadColorizer(name));
					}
				});
			}
			
			if (_colorizationLoader!= null) {
				graphs.ClearColor();
			}
			graphs.SetDimensions(new LibDmd.Input.Dimensions(_options.ResizeTo[0], _options.ResizeTo[1]));
		}

		public override void Dispose()
		{
			base.Dispose();
			_dmdColorSubscription?.Dispose();
			_nameSubscription?.Dispose();
		}

		private IConverter LoadColorizer(string gameName)
		{
			var serumColorizer = _colorizationLoader.LoadSerum(gameName, _config.Global.ScalerMode);
			if (serumColorizer != null) {
				return serumColorizer;
			}
			return _colorizationLoader.LoadColorizer(gameName, _config.Global.ScalerMode)?.gray2;
		}
	}
}
