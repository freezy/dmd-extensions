using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Converter;
using NLog;
using LibDmd.Input;
using LibDmd.Input.PBFX2Grabber;
using LibDmd.Input.TPAGrabber;
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
	public class RenderGraph : IRenderer
	{
		/// <summary>
		/// A source is something that produces frames at an arbitrary resolution with
		/// an arbitrary framerate.
		/// </summary>
		public ISource Source { get; set; }

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
		public List<IDestination> Destinations { get; set; }

		/// <summary>
		/// If set, convert bitrate. Overrides <see cref="RenderAs"/>.
		/// </summary>
		public IConverter Converter { get; set; }

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
		/// How data is internally processed. Default is Bitmap, which passes bitmaps 
		/// and allows processing, but is the slowest.
		/// </summary>
		public RenderBitLength RenderAs { get; set; } = RenderBitLength.Bitmap;

		/// <summary>
		/// If set, flips the image vertically.
		/// </summary>
		public bool FlipVertically { get; set; }

		/// <summary>
		/// If set, flips the image horizontally.
		/// </summary>
		public bool FlipHorizontally { get; set; }

		/// <summary>
		/// How the image is resized for destinations with fixed width
		/// </summary>
		public ResizeMode Resize { get; set; } = ResizeMode.Stretch;

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
				switch (RenderAs) 
				{
					case RenderBitLength.Gray2:
						var destGray2 = dest as IGray2Destination;
						AssertCompatibility(dest, destGray2, "2-bit");
						Logger.Info("Enabling 2-bit grayscale rendering for {0}", dest.Name);
						destGray2.RenderGray2(ImageUtil.ConvertToGray2(bmp));
						break;

					case RenderBitLength.Gray4:
						var destGray4 = dest as IGray4Destination;
						AssertCompatibility(dest, destGray4, "4-bit");
						destGray4.RenderGray4(ImageUtil.ConvertToGray4(bmp));
						break;

					case RenderBitLength.Rgb24:
						var destRgb24 = dest as IRgb24Destination;
						AssertCompatibility(dest, destRgb24, "24-bit");
						destRgb24.RenderRgb24(ImageUtil.ConvertToRgb24(bmp));
						break;

					case RenderBitLength.Bitmap:
						var destBitmap = dest as IBitmapDestination;
						AssertCompatibility(dest, destBitmap, "Bitmap");
						destBitmap.RenderBitmap(bmp);
						break;
					default:
						throw new ArgumentOutOfRangeException();
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

			try {

				foreach (var dest in Destinations) 
				{
					var destFixedSize = dest as IFixedSizeDestination;
					var destResizable = dest as IResizableDestination;

					// check for 2->24 bit converter
					if (Converter?.From == RenderBitLength.Gray2 && Converter.To == RenderBitLength.Rgb24) {
						var sourceGray2 = Source as IGray2Source;
						var destRgb24 = dest as IRgb24Destination;
						var converterSource = Converter as IRgb24Source;
						AssertCompatibility(Source, sourceGray2, dest, destRgb24, "2-bit", "24-bit");
						Logger.Info("Sending 2-bit frames from \"{0}\" as 24-bit frames to \"{1}\"", Source.Name, dest.Name);
						var disposable = sourceGray2.GetGray2Frames()
							.Select(Converter.Convert)
							.Where(frame => frame != null)
							.Select(frame => TransformRgb24(Source.Dimensions.Value.Width, Source.Dimensions.Value.Height, frame, destFixedSize))
							.Subscribe(destRgb24.RenderRgb24, ex => { throw new Exception("Render error.", ex); });
						_activeSources.Add(disposable);
						if (converterSource != null) {
							// for pre-recorded animations, a converter can become source.
							Logger.Info("Added converter as additional RGB24 source.");
							_activeSources.Add(converterSource.GetRgb24Frames().Subscribe(destRgb24.RenderRgb24));
						}
						continue;
					}
					
					// check for 4->24 bit converter
					if (Converter?.From == RenderBitLength.Gray4 && Converter.To == RenderBitLength.Rgb24) {
						var sourceGray4 = Source as IGray4Source;
						var destRgb24 = dest as IRgb24Destination;
						var converterSource = Converter as IRgb24Source;
						AssertCompatibility(Source, sourceGray4, dest, destRgb24, "4-bit", "24-bit");
						Logger.Info("Sending 4-bit frames from \"{0}\" as 24-bit frames to \"{1}\"", Source.Name, dest.Name);
						var disposable = sourceGray4.GetGray4Frames()
							.Select(Converter.Convert)
							.Where(frame => frame != null)
							.Select(frame => TransformRgb24(Source.Dimensions.Value.Width, Source.Dimensions.Value.Height, frame, destFixedSize))
							.Subscribe(destRgb24.RenderRgb24, ex => { throw new Exception("Render error.", ex); });
						_activeSources.Add(disposable);
						if (converterSource != null) {
							// for pre-recorded animations, a converter can become source.
							Logger.Info("Added converter as additional RGB24 source.");
							_activeSources.Add(converterSource.GetRgb24Frames().Subscribe(destRgb24.RenderRgb24));
						}
						continue;
					}

					if (Converter != null) {
						throw new NotImplementedException($"Frame convertion from ${Converter.From} to ${Converter.To} not implemented.");
					}

					if (destResizable != null) {
						Source.Dimensions.Subscribe(dim => destResizable.SetDimensions(dim.Width, dim.Height));
					}
					switch (RenderAs)
					{
						case RenderBitLength.Gray2: {
							var sourceGray2 = Source as IGray2Source;
							var destGray2 = dest as IGray2Destination;
							AssertCompatibility(Source, sourceGray2, dest, destGray2, "2-bit");
							Logger.Info("Sending unprocessed 2-bit data from \"{0}\" to \"{1}\"", Source.Name, dest.Name);
							var disposable = sourceGray2.GetGray2Frames()
								.Where(frame => frame != null)
								.Select(frame => TransformGray2(Source.Dimensions.Value.Width, Source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray2.RenderGray2, ex => { throw new Exception("Render error.", ex); });
							_activeSources.Add(disposable);
							break;
						}
						case RenderBitLength.Gray4: {
							var sourceGray4 = Source as IGray4Source;
							var destGray4 = dest as IGray4Destination;
							AssertCompatibility(Source, sourceGray4, dest, destGray4, "4-bit");
							Logger.Info("Sending unprocessed 4-bit data from \"{0}\" to \"{1}\"", Source.Name, dest.Name);
							var disposable = sourceGray4.GetGray4Frames()
								.Where(frame => frame != null)
								.Select(frame => TransformGray4(Source.Dimensions.Value.Width, Source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray4.RenderGray4, ex => { throw new Exception("Render error.", ex); });
							_activeSources.Add(disposable);
							break;
						}
						case RenderBitLength.Rgb24: {
							var sourceRgb24 = Source as IRgb24Source;
							var destRgb24 = dest as IRgb24Destination;
							AssertCompatibility(Source, sourceRgb24, dest, destRgb24, "24-bit");
							Logger.Info("Sending unprocessed 24-bit RGB data from \"{0}\" to \"{1}\"", Source.Name, dest.Name);
							var disposable = sourceRgb24.GetRgb24Frames()
								.Where(frame => frame != null)
								.Select(frame => TransformRgb24(Source.Dimensions.Value.Width, Source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destRgb24.RenderRgb24, ex => { throw new Exception("Render error.", ex); });
							_activeSources.Add(disposable);
							break;
						}
						case RenderBitLength.Bitmap: {
							var sourceBitmap = Source as IBitmapSource;
							var destBitmap = dest as IBitmapDestination;
							AssertCompatibility(Source, sourceBitmap, dest, destBitmap, "bitmap");
							Logger.Info("Sending bitmap data from \"{0}\" to \"{1}\"", Source.Name, dest.Name);
							var enabledProcessors = Processors?.Where(processor => processor.Enabled) ?? new List<AbstractProcessor>();
							var disposable = sourceBitmap.GetBitmapFrames()
									.Where(bmp => bmp != null)
									.Do(_beforeProcessed.OnNext)
									.Select(bmp => enabledProcessors.Aggregate(bmp, (currentBmp, processor) => processor.Process(currentBmp, dest)))
									.Select(bmp => Transform(bmp, destFixedSize))
									.Subscribe(destBitmap.RenderBitmap, ex => {
										if (onError != null && (ex is CropRectangleOutOfRangeException || ex is RenderException)) {
											onError.Invoke(ex);
										} else {
											throw new Exception("Error rendering bitmap.", ex);
										}
									});
							_activeSources.Add(disposable);
							break;
						}
						default:
							throw new ArgumentOutOfRangeException();
					}

					// now subscribe
					Source.OnResume.Subscribe(x => { Logger.Info("Frames coming in from {0}.", Source.Name); });
					Source.OnPause.Subscribe(x => {
						Logger.Info("Frames stopped from {0}.", Source.Name);
						onCompleted?.Invoke();
					});
				}

			} catch (AdminRightsRequiredException ex) {
				IsRendering = false;
				if (onError != null) {
					onError.Invoke(ex);
				} else {
					throw ex;
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

		private byte[] TransformGray2(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return TransformationUtil.Flip(width, height, 1, frame, FlipHorizontally, FlipVertically);
			}
			var bmp = ImageUtil.ConvertFromGray2(width, height, frame, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray2(transformedBmp);
			return transformedFrame;
		}

		private byte[] TransformGray4(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return TransformationUtil.Flip(width, height, 1, frame, FlipHorizontally, FlipVertically);
			}
			var bmp = ImageUtil.ConvertFromGray4(width, height, frame, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray4(transformedBmp);
			return transformedFrame;
		}

		private byte[] TransformRgb24(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return TransformationUtil.Flip(width, height, 3, frame, FlipHorizontally, FlipVertically);
			}
			var bmp = ImageUtil.ConvertFromRgb24(width, height, frame);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = new byte[dest.DmdWidth * dest.DmdHeight * 3];
			ImageUtil.ConvertToRgb24(transformedBmp, transformedFrame);
			return transformedFrame;
		}

		private BitmapSource Transform(BitmapSource bmp, IFixedSizeDestination dest)
		{
			return dest == null ? bmp : TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
		}

		/// <summary>
		/// Makes sure that a given source is compatible with a given destination or throws an exception.
		/// </summary>
		/// <param name="src">Original source</param>
		/// <param name="castedSource">Casted source, will be checked against null</param>
		/// <param name="dest">Original destination</param>
		/// <param name="castedDest">Casted source, will be checked against null</param>
		/// <param name="what">Message</param>
		private static void AssertCompatibility(ISource src, object castedSource, IDestination dest, object castedDest, string what, string whatDest = null)
		{
			if (castedSource == null && castedDest == null) {
				if (whatDest != null) {
					throw new IncompatibleRenderer($"Source \"${src.Name}\" is not ${what} compatible and destination \"${dest.Name}\" is not ${whatDest} compatible.");
                }
				throw new IncompatibleRenderer($"Neither source \"${src.Name}\" nor destination \"${dest.Name}\" are ${what} compatible.");
			}
			if (castedSource == null) {
				throw new IncompatibleRenderer("Source \"" + src.Name + "\" is not " + what + " compatible.");
			}
			AssertCompatibility(dest, castedDest, whatDest ?? what);
		}
		
		/// <summary>
		/// Makes sure that a given source is compatible with a given destination or throws an exception.
		/// </summary>
		/// <param name="dest">Original destination</param>
		/// <param name="castedDest">Casted source, will be checked against null</param>
		/// <param name="what">Message</param>
		private static void AssertCompatibility(IDestination dest, object castedDest, string what)
		{
			if (castedDest == null) {
				throw new IncompatibleRenderer("Destination \"" + dest.Name + "\" is not " + what + " compatible.");
			}
		}
	}

	public enum RenderBitLength
	{
		Gray2,
		Gray4,
		Rgb24,
		Bitmap
	}

	/// <summary>
	/// A disposable that unsubscribes from all destinations from the source and 
	/// hence stops rendering.
	/// </summary>
	/// <remarks>
	/// Note that destinations are still active, i.e. not yet disposed and can
	/// be re-subscribed if necssary.
	/// </remarks>
	internal class RenderDisposable : IDisposable
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

	/// <summary>
	/// Thrown when trying to force a bitlength and either source or destination isn't compatible.
	/// </summary>
	public class IncompatibleRenderer : Exception
	{
		public IncompatibleRenderer(string message) : base(message)
		{
		}
	}
}
