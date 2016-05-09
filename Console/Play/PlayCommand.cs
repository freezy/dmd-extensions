using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Console.Common;
using Console.Test;
using LibDmd;
using LibDmd.Input.Media;
using LibDmd.Processor;

namespace Console.Play
{
	class PlayCommand : BaseCommand
	{
		private readonly PlayOptions _options;
		private RenderGraph _graph;
		private IDisposable _renderer;

		public PlayCommand(PlayOptions options)
		{
			_options = options;
		}

		public override void Execute(Action onCompleted, Action<Exception> onError)
		{
			// define source
			var source = new ImageSource(_options.FileName);

			// define renderers
			var renderers = GetRenderers(_options);

			// define processors
			var transformationProcessor = new TransformationProcessor {
				FlipVertically = _options.FlipVertically,
				FlipHorizontally = _options.FlipHorizontally,
				Resize = _options.Resize
			};

			// chain them up
			_graph = new RenderGraph {
				Source = source,
				Processors = new List<AbstractProcessor> { transformationProcessor },
				Destinations = renderers,
				RenderAsGray4 = _options.RenderAsGray4, 
			};

			// render image
			_renderer = _graph.StartRendering(onCompleted, onError);
		}

		public override void Dispose()
		{
			_renderer?.Dispose();
			_graph?.Dispose();
		}
	}

	public class FileNotFoundException : Exception
	{
		public FileNotFoundException(string message) : base(message)
		{
		}
	}
}
