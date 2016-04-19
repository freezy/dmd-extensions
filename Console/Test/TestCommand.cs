using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using App;
using Console.Common;
using LibDmd;
using LibDmd.Input.PBFX2Grabber;
using LibDmd.Input.ScreenGrabber;
using LibDmd.Output;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Processor;
using NLog;

namespace Console.Test
{
	class TestCommand : ICommand
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly TestOptions _options;
		public TestCommand(TestOptions options)
		{
			_options = options;
		}

		public void Execute()
		{
			var virtualDmd = new VirtualDmd();

			// define renderers
			var renderers = new List<IFrameDestination> { virtualDmd.Dmd };
			Logger.Info("Added VirtualDMD renderer.");

			try {
				var pinDmd1 = PinDmd1.GetInstance();
				var pinDmd2 = PinDmd2.GetInstance();
				var pinDmd3 = PinDmd3.GetInstance();
				var pin2Dmd = Pin2Dmd.GetInstance();

				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Logger.Info("Added PinDMDv1 renderer.");
				}
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Logger.Info("Added PinDMDv2 renderer.");
				}
				if (pinDmd3.IsAvailable) {
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
				} 
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");
				}

			} catch (DllNotFoundException e) {
				Logger.Error(e, "Error loading DLL.");
				return;
			}

			// chain them up
			var graph = new RenderGraph {
				Destinations = renderers,
			};

			var bmp = new BitmapImage();
			bmp.BeginInit();
			bmp.UriSource = new Uri("pack://application:,,,/dmdext;component/Test/TestImage.png");
			bmp.EndInit();

			graph.Render(bmp);

			DmdExt.WinApp.Run(virtualDmd);
		}
	}
}
