using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DmdExt.Common;
using LibDmd;
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
				(renderer as IRgb24)?.SetPalette(new[] {
					Color.FromRgb(0xff, 0x0, 0x0),
					Color.FromRgb(0x0, 0x0, 0xff)
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
				RenderAs = _options.RenderAs
			};

			return _graph;
		}
	}
}
