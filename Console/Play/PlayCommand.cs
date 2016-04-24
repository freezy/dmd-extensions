using System;
using System.Collections.Generic;
using Console.Common;
using LibDmd;
using LibDmd.Input.Media;
using LibDmd.Processor;

namespace Console.Play
{
	class PlayCommand : BaseCommand
	{
		private readonly PlayOptions _options;
		private RenderGraph _graph;

		public PlayCommand(PlayOptions options)
		{
			_options = options;
		}

		public override void Execute(Action onCompleted)
		{
			// define renderers
			var renderers = GetRenderers(_options);

			var transformationProcessor = new TransformationProcessor {
				FlipVertically = _options.FlipVertically,
				FlipHorizontally = _options.FlipHorizontally
			};

			// chain them up
			_graph = new RenderGraph {
				Source = new MediaPlayer { Filename = _options.File },
				Processors = new List<AbstractProcessor> { transformationProcessor },
				Destinations = renderers,
				RenderAsGray4 = _options.RenderAsGray4
			};

			_graph.StartRendering(onCompleted);
		}

		public override void Dispose()
		{
			_graph?.Dispose();
		}
	}
}
