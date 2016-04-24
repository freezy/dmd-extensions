using System;

using Console.Common;
using LibDmd;
using LibDmd.Input.Media;

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

			// chain them up
			_graph = new RenderGraph {
				Source = new MediaPlayer { Filename = _options.File },
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
