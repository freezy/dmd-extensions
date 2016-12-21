using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DmdExt.Common;
using LibDmd;
using LibDmd.Input;
using LibDmd.Input.FileSystem;
using LibDmd.Output;
using LibDmd.Processor;

namespace DmdExt.Play
{
	class PlayCommand : BaseCommand
	{
		private readonly PlayOptions _options;

		public PlayCommand(PlayOptions options)
		{
			_options = options;
		}

		protected override IRenderer CreateRenderGraph()
		{
			// define source
			object source;
			switch (Path.GetExtension(_options.FileName.ToLower()))
			{
				case ".png":
				case ".jpg":
					source = new ImageSource(_options.FileName);
					break;

				case ".bin":
					source = new RawSource(_options.FileName);
					break;

				default:
					throw new UnknownFormatException("Unknown format " + Path.GetExtension(_options.FileName.ToLower()) + 
						". Known formats: png, jpg, bin.");
			}

			// define renderers
			var renderers = GetRenderers(_options);

			// define processors
			var transformationProcessor = new TransformationProcessor {
				FlipVertically = _options.FlipVertically,
				FlipHorizontally = _options.FlipHorizontally,
				Resize = _options.Resize
			};

			var frameSource = source as ISource;
			if (frameSource != null) {
				// chain them up
				return new RenderGraph {
					Source = frameSource,
					Processors = new List<AbstractProcessor> { transformationProcessor },
					Destinations = renderers,
					RenderAs = _options.RenderAs,
				};
			}

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
			return new RawRenderer(rawSource, rawOutput);
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
