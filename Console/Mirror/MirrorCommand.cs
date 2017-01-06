using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using DmdExt.Common;
using LibDmd;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Input.PBFX2Grabber;
using LibDmd.Input.ProPinball;
using LibDmd.Input.ScreenGrabber;
using LibDmd.Input.TPAGrabber;
using LibDmd.Processor;

namespace DmdExt.Mirror
{
	class MirrorCommand : BaseCommand
	{
		private readonly MirrorOptions _options;
		private RenderGraph _graph;

		public MirrorCommand(MirrorOptions options)
		{
			_options = options;
		}

		protected override IRenderer CreateRenderGraph()
		{
			// create graph with renderers
			_graph = new RenderGraph {
				Destinations = GetRenderers(_options),
				Resize = _options.Resize,
				FlipHorizontally = _options.FlipHorizontally,
				FlipVertically = _options.FlipVertically
			};

			/*var transformationProcessor = new TransformationProcessor {
				FlipVertically = _options.FlipVertically,
				FlipHorizontally = _options.FlipHorizontally,
				Resize = _options.Resize
			};*/

			// setup source and additional processors
			switch (_options.Source) {

				case SourceType.PinballFX2: {

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
					/*
					_graph.Processors = new List<AbstractProcessor> {
						gridProcessor,
						//transformationProcessor,
						shadeProcessor
					};*/
					break;
				}

				case SourceType.PinballArcade: { 
					_graph.Source = new TPAGrabber { FramesPerSecond = _options.FramesPerSecond };
					var shadeProcessor = new ShadeProcessor {
						Enabled = !_options.DisableShading,
						NumShades = 4,
						Shades = new[]{ 0d, 0.22, 0.35, 0.55 },
						Intensity = 1.9,
						Brightness = 0
					};
					/*
					_graph.Processors = new List<AbstractProcessor>() {
						//transformationProcessor,
						shadeProcessor
					};*/
					break;
				}

				case SourceType.ProPinball: {
					
					_graph.Source = new ProPinballSlave(_options.ProPinballArgs);
					break;
				}

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
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
			return _graph;
		}
	}
}
