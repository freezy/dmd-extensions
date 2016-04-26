using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Console.Common;
using LibDmd;
using LibDmd.Input;
using LibDmd.Input.PBFX2Grabber;
using LibDmd.Input.ScreenGrabber;
using LibDmd.Processor;

namespace Console.Mirror
{
	class MirrorCommand : BaseCommand
	{
		private readonly MirrorOptions _options;
		private RenderGraph _graph;

		public MirrorCommand(MirrorOptions options)
		{
			_options = options;
		}

		public override void Execute(Action onCompleted)
		{
			// create graph with renderers
			_graph = new RenderGraph {
				Destinations = GetRenderers(_options),
				RenderAsGray4 = _options.RenderAsGray4
			};

			// instantiate transformation processor
			var transformationProcessor = new TransformationProcessor {
				FlipVertically = _options.FlipVertically,
				FlipHorizontally = _options.FlipHorizontally
			};

			// setup source and additional processors
			switch (_options.Source) {

				case SourceType.PinballFX2:
					if (_options.GridSize.Length != 2) {
						throw new InvalidOptionException("Argument --grid-size must have two values: \"<Width> <Height>\".");
					}
					if (_options.DmdCrop.Length != 4) {
						throw new InvalidOptionException("Argument --dmd-crop must have four values: \"<Left> <Top> <Right> <Bottom>\".");
					}
					_graph.Source = new PBFX2Grabber {
						FramesPerSecond = _options.FramesPerSecond,
						CropLeft = _options.DmdCrop[0],
						CropTop = _options.DmdCrop[1],
						CropRight = _options.DmdCrop[2],
						CropBottom = _options.DmdCrop[3]
					};
					var gridProcessor = new GridProcessor {
						Spacing = _options.GridSpacing,
						Width = _options.GridSize[0],
						Height = _options.GridSize[1]
					};
					var shadeProcessor = new ShadeProcessor {
						Enabled = !_options.DisableShading,
						NumShades = _options.NumShades,
						Intensity = _options.ShadeIntensity,
						Brightness = _options.ShadeBrightness
					};
					_graph.Processors = new List<AbstractProcessor>() {
						gridProcessor,
						transformationProcessor,
						shadeProcessor
					};
					break;

				case SourceType.Screen:
					if (_options.Position.Length != 4) {
						throw new InvalidOptionException("Argument --position must have four values: \"<Left> <Top> <Width> <Height>\".");
					}
					_graph.Source = new ScreenGrabber {
						FramesPerSecond = _options.FramesPerSecond,
						Left = _options.Position[0],
						Top = _options.Position[1],
						Width = _options.Position[2],
						Height = _options.Position[3]
					};
					_graph.Processors = new List<AbstractProcessor>() { transformationProcessor };
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			// TODO add as option
			var monochromeProcessor = new MonochromeProcessor {
				Tint = Color.FromRgb(255, 155, 0)
			};

			// always transform to correct dimensions
			_graph.StartRendering(onCompleted);
		}

		public override void Dispose()
		{
			_graph?.Dispose();
		}
	}
}
