using System;
using System.Windows.Media.Imaging;
using DmdExt.Common;
using LibDmd;
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

			// retrieve image
			var bmp = new BitmapImage();
			bmp.BeginInit();
			bmp.UriSource = new Uri("pack://application:,,,/dmdext;component/Test/TestImage.png");
			bmp.EndInit();

			// chain them up
			_graph = new RenderGraph {
				Source = new ImageSource(bmp),
				Destinations = renderers,
				RenderAsGray4 = _options.RenderAsGray4
			};

			return _graph;
		}
	}
}
