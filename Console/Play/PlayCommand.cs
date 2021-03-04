using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DmdExt.Common;
using LibDmd;
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

		public PlayCommand(IConfiguration config, PlayOptions options)
		{
			_config = config;
			_options = options;
		}

		protected override void CreateRenderGraphs(RenderGraphCollection graphs, HashSet<string> reportingTags)
		{
			// define source
			object source;
			switch (Path.GetExtension(_options.FileName.ToLower()))
			{
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

				default:
					throw new UnknownFormatException("Unknown format " + Path.GetExtension(_options.FileName.ToLower()) +
						". Known formats: png, jpg, gif, bin.");
			}

			// define renderers
			var renderers = GetRenderers(_config, reportingTags);
			var frameSource = source as ISource;
			if (frameSource != null) {
				// chain them up
				graphs.Add(new RenderGraph {
					Source = frameSource,
					Destinations = renderers,
					Resize = _config.Global.Resize,
					FlipHorizontally = _config.Global.FlipHorizontally,
					FlipVertically = _config.Global.FlipVertically
				});
				return;
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
			graphs.Add(new RawRenderer(rawSource, rawOutput));
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
