using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Media;
using DmdExt.Common;
using LibDmd;
using LibDmd.Converter;
using LibDmd.Converter.Vni;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Input.FileSystem;
using LibDmd.Output;

namespace DmdExt.Play
{
	class PlayCommand : BaseCommand
	{
		private readonly IConfiguration _config;
		private readonly PlayOptions _options;
		private ColorizationLoader _colorizationLoader;
		private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

		public PlayCommand(IConfiguration config, PlayOptions options)
		{
			_config = config;
			_options = options;
			if (_options.SkipAnalytics) {
				Analytics.Instance.Disable();
			}
		}

		protected override void CreateRenderGraphs(RenderGraphCollection graphs, HashSet<string> reportingTags)
		{
			// define source
			object source;
			switch (Path.GetExtension(_options.FileName.ToLower())) {
				case ".png":
				case ".jpg":
					source = new ImageSourceBitmap(_options.FileName);
					break;

				case ".gif":
					source = new GifSource(_options.FileName);
					break;

				case ".bin":
					source = new RawSource(_options.FileName);
					break;

				case ".txt":
					source = new DumpSource(_options.FileName);
					break;

				default:
					throw new UnknownFormatException("Unknown format " + Path.GetExtension(_options.FileName.ToLower()) +
						". Known formats: png, jpg, gif, bin, txt.");
			}

			// define renderers
			var renderers = GetRenderers(_config, reportingTags);
			if (source is ISource frameSource) {
				// chain them up
				graphs.Add(new RenderGraph {
					Source = frameSource,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically,
				});

			} else {
				// not an ISource, so it must be a IRawSource.
				var rawSource = (IRawSource)source;
				IRawOutput rawOutput = null;
				foreach (var dest in renderers.OfType<IRawOutput>()) {
					if (rawOutput != null) {
						throw new MultipleRawSourcesException("Cannot use multiple destinations when using a raw source.");
					}
					rawOutput = dest;
				}
				if (rawOutput == null) {
					throw new NoRawDestinationException("No device supporting raw data available.");
				}
				graphs.Add(new RawRenderer(rawSource, rawOutput));
			}

			// if colorization enabled, subscribe to name changes to re-load colorizer.
			if (_config.Global.Colorize) {
				foreach (var graph in graphs.Graphs) {
					if (!(graph.Source is IGameNameSource gameNameSource)) {
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

				// print game names
				var graph = graphs.Graphs.FirstOrDefault();
				if (graph?.Source is IGameNameSource gameNameSource) {
					var nameSub = gameNameSource.GetGameName().Subscribe(name => {
						Logger.Info($"New game detected at {graph.Source.Name}: {name}");
					});
					_subscriptions.Add(nameSub);
				}
			}
		}

		private AbstractConverter SetupColorizer(string gameName)
		{
			// only setup if enabled and path is set
			if (!_config.Global.Colorize || _colorizationLoader == null || gameName == null) {
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

		public override void Dispose()
		{
			base.Dispose();
			_subscriptions.Dispose();
		}
	}

	public class UnknownFormatException : Exception
	{
		public UnknownFormatException(string message) : base(message)
		{
		}
	}

	public class NoRawDestinationException : Exception
	{
		public NoRawDestinationException(string message) : base(message)
		{
		}
	}

	public class MultipleRawSourcesException : Exception
	{
		public MultipleRawSourcesException(string message) : base(message)
		{
		}
	}
}
