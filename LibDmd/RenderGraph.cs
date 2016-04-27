using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;
using LibDmd.Input;
using LibDmd.Input.PBFX2Grabber;
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
		public BaseFrameSource Source { get; set; }

		/// <summary>
		/// A processor is something that receives a frame, does some processing
		/// on it, and returns the processed frame.
		/// 
		/// All frames from the source are passed through all processors before
		/// the reach their destinations.
		/// 
		/// Examples of processors are convert to gray scale or resize.
		/// </summary>
		public List<BaseProcessor> Processors { get; set; }

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
		/// If true, profiling data is collected.
		/// </summary>
		public bool IsProfiling { get; set; }

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
		private readonly Profiler _profiler = new Profiler();
		private ProfilerFrame _profilerFrame;
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
		/// <param name="onError">When a known error occurs.</param>
		/// <returns>An IDisposable that stops rendering when disposed.</returns>
		public IDisposable StartRendering(Action onCompleted, Action<Exception> onError = null)
		{
			if (_activeSources.Count > 0) {
				throw new RendersAlreadyActiveException("Renders already active, please stop before re-launching.");
			}
			IsRendering = true;
			var enabledProcessors = Processors?.Where(processor => processor.Enabled) ?? new List<BaseProcessor>();

			foreach (var dest in Destinations) {
				var canRenderGray4 = false;
				var destGray4 = dest as IGray4;
				if (destGray4 != null) {
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
				var disposable = Source.GetFrames().Subscribe(frame =>
				{
					_profilerFrame?.Next();
					//Logger.Trace(_profilerFrame);
					_profilerFrame = frame.ProfilerFrame;
					_profilerFrame.Next(Source);

					var bmp = frame.Bitmap;
					_beforeProcessed.OnNext(bmp);
					if (Processors != null) {
						// TODO don't process non-greyscale compatible processors when gray4 is enabled
						bmp = enabledProcessors
							.Where(processor => dest.IsRgb || processor.IsGrayscaleCompatible)
							.Aggregate(bmp, (currentBmp, processor) => {
								var b = processor.Process(currentBmp);
								_profilerFrame.Next(processor);
								return b;
							});
					}
					if (RenderAsGray4 && canRenderGray4) {
						destGray4?.RenderGray4(bmp);
					} else {
						dest.Render(bmp);
					}
					_profilerFrame.Next(dest);
					_profiler.AddFrame(_profilerFrame);

				}, ex => {
					if (onError != null && ex is CropRectangleOutOfRangeException) {
						onError.Invoke(ex);

					} else {
						throw ex;
					}
				});
				_activeSources.Add(disposable);
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
