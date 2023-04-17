﻿using System;
using System.Collections.Generic;
using DmdExt.Common;
using LibDmd;
using LibDmd.Converter;
using LibDmd.DmdDevice;
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
		private RenderGraph _graph;
		private IDisposable _nameSubscription;
		private IDisposable _dmdColorSubscription;

		public MirrorCommand(IConfiguration config, MirrorOptions options)
		{
			_config = config;
			_options = options;
		}

		protected override void CreateRenderGraphs(RenderGraphCollection graphs, HashSet<string> reportingTags)
		{
			// create graph with renderers
			_graph = new RenderGraph {
				Destinations = GetRenderers(_config, reportingTags),
				Resize = _config.Global.Resize,
				FlipHorizontally = _config.Global.FlipHorizontally,
				FlipVertically = _config.Global.FlipVertically,
				IdleAfter = _options.IdleAfter,
				IdlePlay = _options.IdlePlay
			};
			_graph.SetColor(_config.Global.DmdColor);

			// setup source and additional processors
			switch (_options.Source) {

				case SourceType.PinballFX2: {
					_graph.Source = new PinballFX2Grabber { FramesPerSecond = _options.FramesPerSecond };
					reportingTags.Add("In:PinballFX2");
					break;
				}

				case SourceType.PinballFX3: {
					if (_options.Fx3GrabScreen) {
						_graph.Source = new PinballFX3Grabber { FramesPerSecond = _options.FramesPerSecond };
						reportingTags.Add("In:PinballFX3Legacy");
					} else {
						var memoryGrabber = new PinballFX3MemoryGrabber { FramesPerSecond = _options.FramesPerSecond };
						_graph.Source = memoryGrabber;

						var latest = new SwitchingConverter();
						_graph.Converter = latest;

						_dmdColorSubscription = memoryGrabber.DmdColor.Subscribe(color => { latest.DefaultColor = color; });

						if (_config.Global.Colorize) {
							_colorizationLoader = new ColorizationLoader();
							var nameGrabber = new PinballFX3GameNameMemoryGrabber();
							_nameSubscription = nameGrabber.GetFrames().Subscribe(name => { latest.Switch(LoadColorizer(name)); });
						}

						reportingTags.Add("In:PinballFX3");
					}
					break;
				}

				case SourceType.PinballArcade: {
					_graph.Source = new TPAGrabber { FramesPerSecond = _options.FramesPerSecond };
					reportingTags.Add("In:PinballArcade");
					break;
				}

				case SourceType.ProPinball: {
					_graph.Source = new ProPinballSlave(_options.ProPinballArgs);
					reportingTags.Add("In:ProPinball");
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

					_graph.Source = grabber;
					reportingTags.Add("In:ScreenGrab");
					break;

				case SourceType.FuturePinball:
					_graph.Source = new FutureDmdSink(_options.FramesPerSecond);
					reportingTags.Add("In:FutureDmdSink");
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
			graphs.Add(_graph);

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
