using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using NLog;
using LibDmd.Input;
using LibDmd.Input.PBFX2Grabber;
using LibDmd.Input.TPAGrabber;
using LibDmd.Output;
using LibDmd.Processor;
using NLog.LayoutRenderers;

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
	public class RenderGraph : IRenderer
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
		
		/// <summary>
		/// If true, send 2-byte grayscale image to renderers which support it.
		/// </summary>
		public bool RenderAsGray2 { get; set; }

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
				var destGray2 = dest as IGray2;
				var destGray4 = dest as IGray4;
				if (RenderAsGray2 && destGray2 != null) {
					Logger.Info("Enabling 2-bit grayscale rendering for {0}", dest.Name);
					destGray2.RenderGray2(bmp);

				} else if (RenderAsGray4 && destGray4 != null) {
					Logger.Info("Enabling 4-bit grayscale rendering for {0}", dest.Name);
					destGray4.RenderGray4(bmp);

				} else {
					dest.Render(bmp);
				}
				onCompleted?.Invoke();
			}
		}

		/// <summary>
		/// Subscribes to the source and hence starts receiving and processing frames
		/// as soon as the source produces them.
		/// </summary>
		/// <remarks>
		/// Note that unexpected errors crash the app so we get a log and can debug.
		/// Expected errors end up in the provided error callback.
		/// </remarks>
		/// <param name="onError">When a known error occurs.</param>
		/// <returns>An IDisposable that stops rendering when disposed.</returns>
		public IDisposable StartRendering(Action<Exception> onError = null)
		{
			return StartRendering(null, onError);
		}

		/// <summary>
		/// Subscribes to the source and hence starts receiving and processing frames
		/// as soon as the source produces them.
		/// </summary>
		/// <remarks>
		/// Note that unexpected errors crash the app so we get a log and can debug.
		/// Expected errors end up in the provided error callback.
		/// </remarks>
		/// <param name="onCompleted">When the source stopped producing frames.</param>
		///  <param name="onError">When a known error occurs.</param>
		/// <returns>An IDisposable that stops rendering when disposed.</returns>
		public IDisposable StartRendering(Action onCompleted, Action<Exception> onError = null)
		{
			if (_activeSources.Count > 0) {
				throw new RendersAlreadyActiveException("Renders already active, please stop before re-launching.");
			}
			IsRendering = true;
			var enabledProcessors = Processors?.Where(processor => processor.Enabled) ?? new List<AbstractProcessor>();

			foreach (var dest in Destinations) {
				var canRenderGray2 = false;
				var canRenderGray4 = false;
				var destGray2 = dest as IGray2;
				var destGray4 = dest as IGray4;
				if (destGray2 != null) {
					canRenderGray2 = true;
					if (RenderAsGray2) {
						Logger.Info("Enabling 2-bit grayscale rendering for {0}", dest.Name);
					}
				} else if (destGray4 != null) {
					canRenderGray4 = true;
					if (RenderAsGray4) {
						Logger.Info("Enabling 4-bit grayscale rendering for {0}", dest.Name);
					}
				}
				// now subscribe
				Source.OnResume.Subscribe(x => {
					Logger.Info("Frames coming in from {0}.", Source.Name);
				});
				Source.OnPause.Subscribe(x => {
					Logger.Info("Frames stopped from {0}.", Source.Name);
					onCompleted?.Invoke();
				});
				try {

					if (Destinations.Count == 1 && canRenderGray2 && RenderAsGray2 && Source is IFrameSourceGray2) {
						Logger.Info("Sending unprocessed 2-bit data from {0} to {1}", Source.Name, dest.Name);
						var disposable = ((IFrameSourceGray2)Source).GetGray2Frames().Subscribe(frame => {
							destGray2.RenderGray2(frame);
						}, ex => {
							throw ex;
						});
						_activeSources.Add(disposable);

					} else if (Destinations.Count == 1 && canRenderGray4 && RenderAsGray4 && Source is IFrameSourceGray4) {
						Logger.Info("Sending unprocessed 4-bit data from {0} to {1}", Source.Name, dest.Name);
						var disposable = ((IFrameSourceGray4)Source).GetGray4Frames().Subscribe(frame => {
							destGray4.RenderGray4(frame);
						}, ex => {
							throw ex;
						});
						_activeSources.Add(disposable);

					} else {
						var disposable = Source.GetFrames().Subscribe(bmp => {

							_beforeProcessed.OnNext(bmp);
							if (Processors != null) {
								// TODO don't process non-greyscale compatible processors when gray4 is enabled
								bmp = enabledProcessors
									.Where(processor => dest.IsRgb || processor.IsGrayscaleCompatible)
									.Aggregate(bmp, (currentBmp, processor) => processor.Process(currentBmp, dest));
							}
							if (RenderAsGray2 && canRenderGray2) {
								destGray2?.RenderGray2(bmp);

							} else if (RenderAsGray4 && canRenderGray4) {
								destGray4?.RenderGray4(bmp);

							} else {
								dest.Render(bmp);
							}

						}, ex => {
							if (onError != null && (ex is CropRectangleOutOfRangeException || ex is RenderException)) {
								onError.Invoke(ex);

							} else {
								throw ex;
							}
						});
						_activeSources.Add(disposable);
					}

				} catch (AdminRightsRequiredException ex) {
					IsRendering = false;
					if (onError != null) {
						onError.Invoke(ex);

					} else {
						throw ex;
					}
				}
			}
			return new RenderDisposable(this, _activeSources);
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
				throw new Exception("Must dispose renderer first!");
			}
			foreach (var dest in Destinations) {
				dest.Dispose();
			}
		}
	}

	/// <summary>
	/// A disposable that unsubscribes from all destinations from the source and 
	/// hence stops rendering.
	/// </summary>
	/// <remarks>
	/// Note that destinations are still active, i.e. not yet disposed and can
	/// be re-subscribed if necssary.
	/// </remarks>
	class RenderDisposable : IDisposable
	{
		private readonly RenderGraph _graph;
		private readonly List<IDisposable> _activeSources;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public RenderDisposable(RenderGraph graph, List<IDisposable> activeSources)
		{
			_graph = graph;
			_activeSources = activeSources;
		}

		public void Dispose()
		{
			foreach (var source in _activeSources) {
				source.Dispose();
			}
			Logger.Info("Source for {0} renderer(s) stopped.", _activeSources.Count);
			_activeSources.Clear();
			_graph.IsRendering = false;
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
