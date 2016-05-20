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

		protected override RenderGraph CreateRenderGraph()
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
			return _graph;
		}
	}

	public class FileNotFoundException : Exception
	{
		public FileNotFoundException(string message) : base(message)
		{
		}
	}
}
