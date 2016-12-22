using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DmdExt.Common;
using LibDmd;
using LibDmd.Converter.Colorize;
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
			_graph = new RenderGraph {
				Source = new ImageSource(bmp),
				Destinations = renderers,
				RenderAs = _options.RenderAs,
				Resize = _options.Resize,
				FlipHorizontally = _options.FlipHorizontally,
				FlipVertically = _options.FlipVertically
			};

			return _graph;
		}
	}
}
