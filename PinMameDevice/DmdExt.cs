using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using DmdExt.Common;
using LibDmd;
using LibDmd.Output;
using Mindscape.Raygun4Net;
using static System.Windows.Threading.Dispatcher;

namespace PinMameDevice
{
	public class DmdExt
	{
		private readonly PinMameSource _source = new PinMameSource();
		private readonly List<RenderGraph> _graphs = new List<RenderGraph>();
		private readonly List<IDisposable> _renderers = new List<IDisposable>();
		private VirtualDmd _dmd;
		static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");

		public void Open()
		{
			ShowVirtualDmd();
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		}

		public void Close()
		{
			_renderers.ForEach(r => r.Dispose());
			_graphs.ForEach(graph => graph.Dispose());
			_graphs.RemoveAll(g => true);
			_dmd?.Dispatcher.Invoke(() => {
				_dmd.Close();
			});
		}

		public void SetPalette(Color[] colors) {
			_dmd.Dmd.SetPalette(colors);
		}

		public void RenderGray2(int width, int height, byte[] frame)
		{
			_source.FramesGray2.OnNext(frame);
		}

		public void RenderGray4(int width, int height, byte[] frame)
		{
			_source.FramesGray4.OnNext(frame);
		}

		public void RenderRgb24(int width, int height, byte[] frame)
		{
			_source.FramesRgb24.OnNext(frame);
		}


		private void ShowVirtualDmd()
		{
			var thread = new Thread(() => {

				_dmd = new VirtualDmd();
				var dest = new List<IFrameDestination> { _dmd.Dmd };
				
				// create a graph for each bit length.
				_graphs.Add(new RenderGraph {
					Source = _source,
					Destinations = dest,
					RenderAs = RenderBitLength.Gray2
				});
				_graphs.Add(new RenderGraph {
					Source = _source,
					Destinations = dest,
					RenderAs = RenderBitLength.Gray4
				});
				_graphs.Add(new RenderGraph {
					Source = _source,
					Destinations = dest,
					RenderAs = RenderBitLength.Rgb24
				});

				_graphs.ForEach(graph => _renderers.Add(graph.StartRendering()));

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));

				// When the window closes, shut down the dispatcher
				_dmd.Closed += (s, e) => CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
				_dmd.Dispatcher.Invoke(() => {
					_dmd.Show();
				});

				// Start the Dispatcher Processing
				Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Raygun.Send(e.ExceptionObject as Exception);
		}
	}
}
