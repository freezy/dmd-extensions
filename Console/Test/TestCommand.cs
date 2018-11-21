using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DmdExt.Common;
using LibDmd;
using LibDmd.DmdDevice;
using LibDmd.Input.PinMame;
using LibDmd.Output;
using ImageSource = LibDmd.Input.FileSystem.ImageSource;

namespace DmdExt.Test
{
	class TestCommand : BaseCommand
	{
		private readonly TestOptions _options;
		private RenderGraph _graph;

		public TestCommand(TestOptions options)
		{
			_options = options;
		}

		protected override IRenderer CreateRenderGraph()
		{
			// define renderers
			var renderers = GetRenderers(_options);
			renderers.ForEach(renderer => {
				(renderer as IRgb24Destination)?.SetPalette(new[] {
					Color.FromRgb(0x0, 0x0, 0xff),
					Color.FromRgb(0xff, 0x0, 0x0),
				});
			});

			// retrieve image
			var bmp = new BitmapImage();
			bmp.BeginInit();
			bmp.UriSource = new Uri("pack://application:,,,/dmdext;component/Test/TestImage.png");
			bmp.EndInit();

			// chain them up
			if (_options.Destination == BaseOptions.DestinationType.AlphaNumeric) {
				var alphaNumericFrame = new AlphaNumericFrame(NumericalLayout.__2x20Alpha,
					new ushort[] {
						0, 10767, 2167, 8719, 0, 2109, 8713, 6259, 56, 2157, 0, 4957, 0, 8719, 62, 8719, 121, 2157, 0,
						0, 0, 0, 5120, 8704, 16640, 0, 0, 0, 0, 2112, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
						0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
					});
				_graph = new RenderGraph {
					Source = new VpmAlphaNumericSource(alphaNumericFrame),
					Destinations = renderers,
					Resize = _options.Resize,
					FlipHorizontally = _options.FlipHorizontally,
					FlipVertically = _options.FlipVertically
				};
			} else {
				_graph = new RenderGraph {
					Source = new ImageSource(bmp),
					Destinations = renderers,
					Resize = _options.Resize,
					FlipHorizontally = _options.FlipHorizontally,
					FlipVertically = _options.FlipVertically
				};
			}

			return _graph;
		}
	}
}
