using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using NLog;
using LibDmd.Input;
using LibDmd.Output;
using LibDmd.Processor;

namespace LibDmd
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
		/// Examples of processors are convert to gray scale or resize.
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

		/// <summary>
		/// If true, send 4-byte grayscale image to renderers which support it.
		/// </summary>
		public bool RenderAsGray4 { get; set; }

		private readonly List<IDisposable> _activeSources = new List<IDisposable>();
		private readonly Subject<BitmapSource> _beforeProcessed = new Subject<BitmapSource>();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Renders a single bitmap on all destinations.
		/// </summary>
		/// <param name="bmp">Bitmap to render</param>
		/// <param name="onCompleted">If set, this action is executed once the bitmap is displayed.</param>
		public void Render(BitmapSource bmp, Action onCompleted = null)
		{
			foreach (var dest in Destinations) {
				var destGray4 = dest as IGray4;
				if (RenderAsGray4 && destGray4 != null) {
					Logger.Info("Enabling 4-bit grayscale rendering for {0}", dest.Name);
					destGray4.RenderGray4(bmp);
				} else {
					dest.Render(bmp);
				}
				onCompleted?.Invoke();
			}
		}

		/// <summary>
		/// Subscribes to the source and hence starts receiving and processing frames.
		/// </summary>
		public void StartRendering(Action onCompleted = null)
		{
			if (_activeSources.Count > 0) {
				throw new RendersAlreadyActiveException("Renders already active, please stop before re-launching.");
			}
			IsRendering = true;
			var enabledProcessors = Processors?.Where(processor => processor.Enabled) ?? new List<AbstractProcessor>();

			foreach (var dest in Destinations) {
				var frames = Source.GetFrames();
				var canRenderGray4 = false;
				var destGray4 = dest as IGray4;
				if (destGray4 != null) {
					canRenderGray4 = true;
					if (RenderAsGray4) {
						Logger.Info("Enabling 4-bit grayscale rendering for {0}", dest.Name);
					}
				} 
				_activeSources.Add(frames.Subscribe(bmp => {

					_beforeProcessed.OnNext(bmp);

					if (Processors != null) {
						// TODO don't process non-greyscale compatible processors when gray4 is enabled
						bmp = enabledProcessors
							.Where(processor => dest.IsRgb || processor.IsGrayscaleCompatible)
							.Aggregate(bmp, (currentBmp, processor) => processor.Process(currentBmp));
					}
					if (RenderAsGray4 && canRenderGray4) {
						destGray4?.RenderGray4(bmp);
					} else {
						dest.Render(bmp);
					}

				}, () => {
					Logger.Info("Source {0} has finished.", dest.Name);
					StopRendering();
					onCompleted?.Invoke();
				}));
			}
		}

		/// <summary>
		/// Unsubscribes all destinations from the source and hence stops rendering
		/// </summary>
		/// <remarks>
		/// Note that destinations are still active, i.e. not yet disponsed and can
		/// be re-subscribe if necssary.
		/// </remarks>
		public void StopRendering()
		{
			foreach (var source in _activeSources) {
				source.Dispose();
			}
			Logger.Info("Source for {0} renderer(s) stopped.", _activeSources.Count);
			_activeSources.Clear();
			IsRendering = false;
		}

		/// <summary>
		/// Disposes the graph and all elements.
		/// </summary>
		/// <remarks>
		/// Run this before exiting the application.
		/// </remarks>
		public void Dispose()
		{
			Logger.Debug("Disposing render graph.");
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
