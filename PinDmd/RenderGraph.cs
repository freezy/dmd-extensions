using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using NLog;
using PinDmd.Input;
using PinDmd.Output;
using PinDmd.Processor;

namespace PinDmd
{
	/// <summary>
	/// A primitive render pipeline which consists of one source and any number
	/// of processors and destinations.
	/// 
	/// Every frame produced by the source goes through all processors and is then
	/// dispatched to all destinations.
	/// </summary>
	/// 
	/// <remarks>
	/// Sources, processors and destinations can be re-used in other graphs. It 
	/// should even be possible to have them running at the same time, e.g. a 
	/// graph withe same source and different processors to different outputs.
	/// </remarks>
	public class RenderGraph : IDisposable
	{
		/// <summary>
		/// A source is something that produces frames at an arbitrary resolution with
		/// an arbitrary framerate.
		/// </summary>
		public IFrameSource Source { get; set; }

		/// <summary>
		/// A processor is something that receives a frame, does some processing
		/// on it, and returns the processed frame.
		/// 
		/// All frames from the source are passed through all processors before
		/// the reach their destinations.
		/// 
		/// Examples of processors are convert to grey scale or resize.
		/// </summary>
		public List<AbstractProcessor> Processors { get; set; }

		/// <summary>
		/// Destinations are output devices that can render frames.
		/// 
		/// All destinations in the graph are getting the same frames.
		/// 
		/// Examples of destinations is a virtual DMD that renders frames
		/// on the computer screen, PinDMD and PIN2DMD integrations.
		/// </summary>
		public List<IFrameDestination> Destinations { get; set; }

		/// <summary>
		/// True of the graph is currently active, i.e. if the source is
		/// producing frames.
		/// </summary>
		public bool IsRendering { get; set; }

		/// <summary>
		/// Produces frames before they get send through the processors.
		/// 
		/// Useful for displaying them for debug purposes.
		/// </summary>
		public IObservable<BitmapSource> BeforeProcessed => _beforeProcessed;

		private readonly List<IDisposable> _activeSources = new List<IDisposable>();
		private readonly Subject<BitmapSource> _beforeProcessed = new Subject<BitmapSource>();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Starts the rendering.
		/// </summary>
		public void StartRendering()
		{
			if (_activeSources.Count > 0) {
				throw new RendersAlreadyActiveException("Renders already active, please stop before re-launching.");
			}
			IsRendering = true;
			var enabledProcessors = Processors.Where(processor => processor.Enabled);

			foreach (var dest in Destinations) {
				var frames = Source.GetFrames();
				_activeSources.Add(frames.Subscribe(bmp => {

					_beforeProcessed.OnNext(bmp);

					if (Processors != null) {
						bmp = enabledProcessors.Aggregate(bmp,
							(current, processor) => processor.Process(current));
					}
					dest.Render(bmp);
				}));
			}
		}

		public void StopRendering()
		{
			foreach (var source in _activeSources) {
				source.Dispose();
			}
			Logger.Info("Source for {0} renderer(s) stopped.", _activeSources.Count);
			_activeSources.Clear();
			IsRendering = false;
		}

		public void Render(BitmapSource bmp)
		{
			foreach (var dest in Destinations) {
				dest.Render(bmp);
			}
		}

		public void Dispose()
		{
			if (IsRendering) {
				StopRendering();
			}
			foreach (var dest in Destinations) {
				dest.Dispose();
			}
		}
	}

	/// <summary>
	/// Thrown when trying to start rendering when it's already started.
	/// </summary>
	public class RendersAlreadyActiveException : Exception
	{
		public RendersAlreadyActiveException(string message) : base(message)
		{
		}
	}
}
