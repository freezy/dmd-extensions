using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Threading;
using DmdExt.Common;
using LibDmd;
using LibDmd.Output;
using static System.Windows.Threading.Dispatcher;

namespace PinMameDevice
{
	public class DmdExt
	{
		private readonly PinMameSource _source = new PinMameSource();
		public void Init()
		{
			ShowVirtualDmd();
		}

		public void RenderGray2(int width, int height, byte[] frame)
		{
			Console.WriteLine("[dmdext] Render_4_Shades()");
			_source.FramesGray2.OnNext(frame);

			/*for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x++) {
					Console.Write(frame[width * y + x].ToString("X1"));
				}
				Console.WriteLine();
			}*/
		}


		private void ShowVirtualDmd()
		{

			var thread = new Thread(() => {

				var dmd = new VirtualDmd();
				var graph = new RenderGraph {
					Source = _source,
					Destinations = new List<IFrameDestination> { dmd.Dmd },
					RenderAsGray4 = false,
					RenderAsGray2 = true,
				};
				graph.StartRendering();

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));

				// When the window closes, shut down the dispatcher
				dmd.Closed += (s, e) => CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
				dmd.Dispatcher.Invoke(() =>
				{
					dmd.Show();
				});

				// Start the Dispatcher Processing
				Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}
	}
}
