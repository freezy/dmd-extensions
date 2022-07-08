using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibDmd.Common;
using NLog;
using LibDmd.Input;
using LibDmd.Input.FileSystem;
using LibDmd.Output;
using ResizeMode = LibDmd.Input.ResizeMode;
using System.Reactive;
using System.Threading.Tasks;

namespace LibDmd
{
	/// <summary>
	/// A render pipeline. This the core of LibDmd.
	/// 
	/// Every render graph has one <see cref="ISource"/> and one or more 
	/// <see cref="IDestination"/>. Frames produced by the source are 
	/// dispatched to all destinations. Sources and destinations can be re-used
	/// in other graphs (converters act as source, hence the plural).
	/// 
	/// It's one of the graph's duties to figure out in which format the frames
	/// should be retrieved and sent to the destinations in the most efficient
	/// way. It does also the conversion between non-matching source and
	/// destination. 
	/// 
	/// </summary>
	public class RenderGraph : IRenderer
	{
		/// <summary>
		/// The render graph's name, mainly for logging purpose.
		/// </summary>
		public string Name { get; set; } = "Render Graph";

		/// <summary>
		/// A source is something that produces frames at an arbitrary resolution with
		/// an arbitrary framerate.
		/// </summary>
		public ISource Source { get; set; }

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
		/// True of the graph is currently active, i.e. if the source is
		/// producing frames.
		/// </summary>
		public bool IsRendering { get; set; }

		/// <summary>
		/// If set, flips the image vertically (top/down).
		/// </summary>
		public bool FlipVertically { get; set; }

		/// <summary>
		/// If set, flips the image horizontally (left/right).
		/// </summary>
		public bool FlipHorizontally { get; set; }

		/// <summary>
		/// How the image is resized for destinations with fixed width.
		/// </summary>
		public ResizeMode Resize { get; set; } = ResizeMode.Stretch;

		/// <summary>
		/// If >0, add a timer to each pipeline that clears the screen after n milliseconds.
		/// </summary>
		public int IdleAfter { get; set; } = 0;

		/// <summary>
		/// When IdleAfter is enabled, play this (blank screen if null)
		/// </summary>
		public string IdlePlay { get; set; }

		public ScalerMode ScalerMode { get; set; }

		public bool Colored { get; set; }

		/// <summary>
		/// The default color used if there is no palette defined
		/// </summary>
		public static readonly Color DefaultColor = Colors.OrangeRed;

		private readonly CompositeDisposable _activeSources = new CompositeDisposable();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Color[] _gray2Colors; 
		private Color[] _gray4Colors;
		private Color[] _gray6Colors;
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;
		private Color[] _gray6Palette;

		private IDisposable _idleRenderer;
		private IDisposable _activeRenderer;
		private RenderGraph _idleRenderGraph;

		public RenderGraph()
		{
			ClearColor();
		}

		/// <summary>
		/// Run before <see cref="StartRendering(Action{Exception})"/>
		/// </summary>
		/// <remarks>
		/// Either that or <see cref="RenderGraphCollection.Init"/> must be run. The latter does
		/// this in a global manner (i.e. doesn't just run this method on each RenderGraph).
		/// </remarks>
		/// <returns>This instance</returns>
		public IRenderer Init()
		{
			// set up the dimension change producer
			Source.Dimensions = new BehaviorSubject<Dimensions>(new Dimensions { Width = 128, Height = 32 });
			Destinations.ForEach(dest => {
				if (dest is IResizableDestination destResizable) {
					Source.Dimensions.Subscribe(dim => destResizable.SetDimensions(dim.Width, dim.Height));
				}
			});

			return this;
		}

		/// <summary>
		/// Renders a single bitmap on all destinations.
		/// </summary>
		/// <param name="bmp">Bitmap to render</param>
		/// <param name="onCompleted">If set, this action is executed once the bitmap is displayed.</param>
		public void Render(BitmapSource bmp, Action onCompleted = null)
		{
			var source = new PassthroughSource("Bitmap Source");
			Source = source;
			_activeRenderer = Init().StartRendering(onCompleted);
			source.FramesBitmap.OnNext(bmp);
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

			try {
				var sourceGray2 = Source as IGray2Source;
				var sourceGray4 = Source as IGray4Source;
				var sourceGray6 = Source as IGray6Source;
				var sourceColoredRgb24 = Source as IColoredRgb24Source;
				Logger.Info("Setting up {0} for {1} destination(s)", Name, Destinations.Count);

				foreach (var dest in Destinations) {

					var destRgb24 = dest as IRgb24Destination;

					// Now here we need to find the most efficient way of passing data from the source
					// to each destination. 
					// One thing to remember is that now we don't have a converter defining the
					// input format, so the source might able to deliver multiple different formats 
					// and the destination might be accepting multiple formats as well. 
					//
					// But since we know that a source doesn't implement any interface that would 
					// result in data loss (e.g. a 4-bit source will not implement IGray2Source), we
					// start looking at the most performant combinations first.
					//
					// So first we try to match the source format with the destination format. Then
					// we go on by looking at "upscaling" convertions, e.g. if a destination only
					// supports RGB24, then convert 2-bit to RGB24. Lastly we check "downscaling"
					// conversions, e.g. convert an RGB24 frame to 2-bit for outputs like PinDMD1
					// that can only render 4 shades.

					var destGray2 = dest as IGray2Destination;
					var destGray4 = dest as IGray4Destination;
					var destColoredRgb24 = dest as IColoredRgb24Destination;
					var destBitmap = dest as IBitmapDestination;
					var destAlphaNumeric = dest as IAlphaNumericDestination;

					var sourceRgb24 = Source as IRgb24Source;
					var sourceBitmap = Source as IBitmapSource;
					var sourceAlphaNumeric = Source as IAlphaNumericSource;

					// first, check if we do without conversion
					// coloredRgb24 -> coloredRgb24
					if (sourceColoredRgb24 != null && destColoredRgb24 != null && Colored)
					{
						Connect(Source, dest, FrameFormat.ColoredRgb24, FrameFormat.ColoredRgb24);
						continue;
					}
					// if coloring is active and destination has destColoredRgb24 skip all other connectors
					if (Colored && destColoredRgb24 != null && (Source.ToString().Equals("LibDmd.Input.PinMame.VpmGray2Source") || Source.ToString().Equals("LibDmd.Input.PinMame.VpmGray4Source")))
					{
						continue;
					}
					// gray2 -> gray2
					if (sourceGray2 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Gray2, FrameFormat.Gray2);
						continue;
					}
					// gray4 -> gray4
					if (sourceGray4 != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Gray4);
						continue;
					}
					// rgb24 -> rgb24
					if (sourceRgb24 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.Rgb24, FrameFormat.Rgb24);
						continue;
					}
					// bitmap -> bitmap
					if (sourceBitmap != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.Bitmap, FrameFormat.Bitmap);
						continue;
					}
					// alphanum -> alphanum
					if (sourceAlphaNumeric != null && destAlphaNumeric != null) {
						Connect(Source, dest, FrameFormat.AlphaNumeric, FrameFormat.AlphaNumeric);
						continue;
					}

					// then, start at the bottom
					// gray2 -> rgb24
					if (sourceGray2 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.Gray2, FrameFormat.Rgb24);
						continue;
					}
					// gray2 -> bitmap
					if (sourceGray2 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.Gray2, FrameFormat.Bitmap);
						continue;
					}
					// gray4 -> rgb24
					if (sourceGray4 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Rgb24);
						continue;
					}
					// gray4 -> bitmap
					if (sourceGray4 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Bitmap);
						continue;
					}
					// rgb24 -> bitmap
					if (sourceRgb24 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.Rgb24, FrameFormat.Bitmap);
						continue;
					}
					// bitmap -> rgb24
					if (sourceBitmap != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.Bitmap, FrameFormat.Rgb24);
						continue;
					}

					// finally, here we lose data
					// gray4 -> gray2
					if (sourceGray4 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Gray2);
						continue;
					}
					// rgb24 -> gray4
					if (sourceRgb24 != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.Rgb24, FrameFormat.Gray4);
						continue;
					}
					// bitmap -> gray4
					if (sourceBitmap != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.Bitmap, FrameFormat.Gray4);
						continue;
					}
					// rgb24 -> gray2
					if (sourceRgb24 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Rgb24, FrameFormat.Gray2);
						continue;
					}
					// bitmap -> gray2
					if (sourceBitmap != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Bitmap, FrameFormat.Gray2);
					}
				}

				// log status
				Source.OnResume?.Subscribe(x => { Logger.Info("Frames coming in from {0}.", Source.Name); });
				Source.OnPause?.Subscribe(x => {
					Logger.Info("Frames stopped from {0}.", Source.Name);
					onCompleted?.Invoke();
				});

			} catch (DebugPrivilegeException ex) {
				IsRendering = false;
				if (onError != null) {
					onError.Invoke(ex);
				} else {
					throw;
				}
			}
			_activeRenderer = new RenderDisposable(this, _activeSources);
			return _activeRenderer;
		}

		/// <summary>
		/// Connects a source with a destination and defines in which mode data is
		/// sent and received.
		/// </summary>
		/// <remarks>
		/// Note that render bitlength is enforced, i.e. even if the destination 
		/// supports the "from" bitlength, it will be converted to the given "to"
		/// bitlength.
		/// </remarks>
		/// <param name="source">Source to subscribe to</param>
		/// <param name="dest">Destination to send the data to</param>
		/// <param name="from">Data format to read from source (incompatible source will throw exception)</param>
		/// <param name="to">Data forma to send to destination (incompatible destination will throw exception)</param>
		[SuppressMessage("ReSharper", "PossibleNullReferenceException")]
		private void Connect(ISource source, IDestination dest, FrameFormat from, FrameFormat to)
		{
			var destFixedSize = dest as IFixedSizeDestination;
			var destGray2 = dest as IGray2Destination;
			var destGray4 = dest as IGray4Destination;
			var destRgb24 = dest as IRgb24Destination;
			var destColoredRgb24 = dest as IColoredRgb24Destination;
			var destBitmap = dest as IBitmapDestination;
			var destAlphaNumeric = dest as IAlphaNumericDestination;

			try {
				Dispatcher.CurrentDispatcher.Invoke(() => Logger.Info("Connecting {0} to {1} ({2} => {3})", source.Name, dest.Name, @from, to));
			
			} catch (TaskCanceledException e) {
				Logger.Error(e, "Main thread seems already destroyed, aborting.");
			}

			switch (from) { 

				// source is gray2:
				case FrameFormat.Gray2:
					var sourceGray2 = source as IGray2Source;
					switch (to)
					{
						// gray2 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceGray2, dest, destGray2, from, to);
							Subscribe(sourceGray2.GetGray2Frames()
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// gray2 -> gray4
						case FrameFormat.Gray4:
							throw new NotImplementedException("Cannot convert from gray2 to gray4 (every gray4 destination should be able to do gray2 as well).");

						// gray2 -> gray6
						case FrameFormat.Gray6:
							throw new NotImplementedException("Cannot convert from gray2 to gray6 (every gray6 destination should be able to do gray2 as well).");

						// gray2 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray2, dest, destRgb24, from, to);
							Subscribe(sourceGray2.GetGray2Frames()
									.Select(frame => TransformScaling(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize))
									.Select(frame => ColorizeGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray2, dest, destBitmap, from, to);
							Subscribe(sourceGray2.GetGray2Frames()
									.Select(frame => TransformScaling(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize))
									.Select(frame => ImageUtil.ConvertFromRgb24(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										ColorizeGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame)
									))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is gray4:
				case FrameFormat.Gray4:
					var sourceGray4 = source as IGray4Source;
					switch (to) {
						// gray4 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceGray4, dest, destGray2, from, to);
							Subscribe(sourceGray4.GetGray4Frames()
									.Select(frame => FrameUtil.ConvertGrayToGray(frame.Data, new byte[] { 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3 }))
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// gray4 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceGray4, dest, destGray4, from, to);
							Subscribe(sourceGray4.GetGray4Frames()
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize)),
								destGray4.RenderGray4);
							break;

						// gray4 -> gray6
						case FrameFormat.Gray6:
							throw new NotImplementedException("Cannot convert from gray4 to gray6 (every gray6 destination should be able to do gray4 as well).");

						// gray4 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray4, dest, destRgb24, from, to);
							Subscribe(sourceGray4.GetGray4Frames()
									.Select(frame => TransformScaling(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize))
									.Select(frame => ColorizeGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray4, dest, destBitmap, from, to);
							Subscribe(sourceGray4.GetGray4Frames()
									.Select(frame => TransformScaling(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize))
									.Select(frame => ImageUtil.ConvertFromRgb24(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										ColorizeGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame)
									))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is rgb24:
				case FrameFormat.Rgb24:
					var sourceRgb24 = source as IRgb24Source;
					switch (to) {
						// rgb24 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceRgb24, dest, destGray2, from, to);
							Subscribe(sourceRgb24.GetRgb24Frames()
									.Select(frame => ImageUtil.ConvertToGray(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, 4))
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// rgb24 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceRgb24, dest, destGray4, from, to);
							Subscribe(sourceRgb24.GetRgb24Frames()
									.Select(frame => ImageUtil.ConvertToGray(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, 16))
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray4.RenderGray4);
							break;

						// rgb24 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceRgb24, dest, destRgb24, from, to);
							Subscribe(sourceRgb24.GetRgb24Frames()
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// rgb24 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceRgb24, dest, destBitmap, from, to);
							Subscribe(sourceRgb24.GetRgb24Frames()
									.Select(frame => ImageUtil.ConvertFromRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is ColoredRgb24:
				case FrameFormat.ColoredRgb24:
					var sourceColoredRgb24 = source as IColoredRgb24Source;
					switch (to)
					{
						// rgb24 -> rgb24
						case FrameFormat.ColoredRgb24:
							AssertCompatibility(source, sourceColoredRgb24, dest, destColoredRgb24, from, to);
							Subscribe(sourceColoredRgb24.GetColoredRgb24Frames()
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize)),
								destColoredRgb24.RenderColoredRgb24);
							break;

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is bitmap:
				case FrameFormat.Bitmap:
					var sourceBitmap = source as IBitmapSource;
					switch (to) {
						// bitmap -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceBitmap, dest, destGray2, from, to);
							Subscribe(sourceBitmap.GetBitmapFrames()
									.Select(bmp => ImageUtil.ConvertToGray2(bmp))
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// bitmap -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceBitmap, dest, destGray4, from, to);
							Subscribe(sourceBitmap.GetBitmapFrames()
									.Select(bmp => ImageUtil.ConvertToGray4(bmp))
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray4.RenderGray4);
							break;

						// bitmap -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceBitmap, dest, destRgb24, from, to);
							Subscribe(sourceBitmap.GetBitmapFrames()
									.Select(bmp => ImageUtil.ConvertToRgb24(bmp))
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// bitmap -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(Source, sourceBitmap, dest, destBitmap, from, to);
							Subscribe(sourceBitmap.GetBitmapFrames()
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is alphanumeric:
				case FrameFormat.AlphaNumeric:
					var sourceAlphaNumeric = source as IAlphaNumericSource;
					switch (to) {

						// colored alphanumeric -> alphanumeric
						case FrameFormat.AlphaNumeric:
							AssertCompatibility(source, sourceAlphaNumeric, dest, destAlphaNumeric, from, to);
							Subscribe(sourceAlphaNumeric.GetAlphaNumericFrames(), destAlphaNumeric.RenderAlphaNumeric);
							break;
						
						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Similar method to ObserveOn but only keeping the latest notification
		/// </summary>
		/// 
		/// <remarks>
		/// This method will drop unprocessed notifications received while processing a previous notification.
		/// 
		/// This code is largely spread across internet, and seems to initiate from Lee Campbell and Wilka Hudson in this post:
		/// https://social.msdn.microsoft.com/Forums/en-US/bbcc1af9-64b4-456b-9038-a540cb5f5de5/how-do-i-ignore-allexceptthelatest-value-when-my-subscribe-method-is-running
		/// The implementation below comes from this blog post: http://www.zerobugbuild.com/?p=192
		/// </remarks>
		public static IObservable<T> ObserveLatestOn<T>(IObservable<T> source, IScheduler scheduler)
		{
			return Observable.Create<T>(observer =>
			{
				Notification<T> outsideNotification = null;
				var gate = new object();
				bool active = false;
				var cancelable = new MultipleAssignmentDisposable();
				var disposable = source.Materialize().Subscribe(thisNotification =>
				{
					bool alreadyActive;
					lock (gate)
					{
						alreadyActive = active;
						active = true;
						outsideNotification = thisNotification;
					}

					if (!alreadyActive)
					{
						cancelable.Disposable = scheduler.Schedule(self =>
						{
							Notification<T> localNotification = null;
							lock (gate)
							{
								localNotification = outsideNotification;
								outsideNotification = null;
							}
							localNotification.Accept(observer);
							bool hasPendingNotification = false;
							lock (gate)
							{
								hasPendingNotification = active = (outsideNotification != null);
							}
							if (hasPendingNotification)
							{
								self();
							}
						});
					}
				});
				return new CompositeDisposable(disposable, cancelable);
			});
		}

		/// <summary>
		/// Subscribes to the given source and links it to the given destination.
		/// </summary>
		/// 
		/// <remarks>
		/// This also does all the common stuff, i.e. setting the correct scheduler,
		/// enabling idle detection, etc.
		/// </remarks>
		/// 
		/// <typeparam name="T">Frame type, already converted to match destination</typeparam>
		/// <param name="src">Source observable</param>
		/// <param name="onNext">Action to run on destination</param>
		private void Subscribe<T>(IObservable<T> src, Action<T> onNext) where T : class
		{
			// always observe on default thread, and only the latest notification (i.e. drop missed frames)
			src = ObserveLatestOn(src, Scheduler.Default);

			// set idle timeout if enabled
			if (IdleAfter > 0) {

				// So we want the sequence to continue after the timeout, which is why 
				// IObservable.Timeout is not well suited.
				// A better approach is to run onNext with IObservable.Do and subscribe
				// to the timeout through IObservable.Throttle.
				Logger.Info("Setting idle timeout to {0}ms.", IdleAfter);

				// now render it
				src = src.Do(_ => StopIdleing());
				src = src.Do(onNext);

				// but subscribe to a throttled idle action
				src = src.Throttle(TimeSpan.FromMilliseconds(IdleAfter));

				// execute on main thread
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
				src = src.ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current));
				_activeSources.Add(src.Subscribe(f => StartIdleing()));

			} else {
				// subscribe and add to active sources
				_activeSources.Add(src.Subscribe(onNext));
			}
		}

		/// <summary>
		/// Starts playing the idle animation, picture or just clears the screen,
		/// depending on configuration.
		/// </summary>
		/// 
		/// <remarks>
		/// This sets up a new render graph with the current destinations.
		/// </remarks>
		private void StartIdleing()
		{
			if (IdlePlay != null) {
				ISource source;
				Logger.Info("Idle timeout ({0}ms), playing {1}.", IdleAfter, IdlePlay);
				switch (Path.GetExtension(IdlePlay.ToLower())) {
					case ".png":
					case ".jpg":
						source = new ImageSourceBitmap(IdlePlay);
						break;

					case ".gif":
						source = new GifSource(IdlePlay);
						break;

					default:
						Logger.Error("Unsupported format " + Path.GetExtension(IdlePlay.ToLower()) + ". Supported formats: png, jpg, gif.");
						return;
				}
				_idleRenderGraph = new RenderGraph {
					Name = "Idle Renderer",
					Source = source,
					Destinations = Destinations,
					Resize = Resize,
					FlipHorizontally = FlipHorizontally,
					FlipVertically = FlipVertically
				};
				_idleRenderer = _idleRenderGraph.StartRendering();

			} else {
				Logger.Info("Idle timeout ({0}ms), clearing display.", IdleAfter);
				ClearDisplay();
			}
		}

		/// <summary>
		/// Stops idling source.
		/// </summary>
		private void StopIdleing()
		{
			if (_idleRenderer != null) {
				_idleRenderer.Dispose();
				_idleRenderer = null;
			}
			if (_idleRenderGraph != null) {
				_idleRenderGraph.Dispose();
				_idleRenderGraph = null;
			}
		}

		private byte[] ColorizeGray2(int width, int height, byte[] frame)
		{
			return ColorUtil.ColorizeFrame(width, height, frame, _gray2Palette ?? _gray2Colors);
		}

		private byte[] ColorizeGray4(int width, int height, byte[] frame)
		{
			return ColorUtil.ColorizeFrame(width, height, frame, _gray4Palette ?? _gray4Colors);
		}

		private byte[] ColorizeGray6(int width, int height, byte[] frame)
		{
			return ColorUtil.ColorizeFrame(width, height, frame, _gray6Palette ?? _gray6Colors);
		}

		/// <summary>
		/// Returns true if at least one of the destinations can receive RGB24 frames.
		/// </summary>
		/// <returns>True if RGB24 is supported, false otherwise</returns>
		private bool HasRgb24Destination()
		{
			return Destinations.OfType<IRgb24Destination>().Any();
		}

		/// <summary>
		/// Sets the color with which a grayscale source is rendered on the RGB display.
		/// </summary>
		/// <param name="color">Rendered color</param>
		public void SetColor(Color color)
		{
			_gray2Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 4);
			_gray4Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 16);
			_gray6Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 64);
		}

		/// <summary>
		/// Sets the palette for rendering grayscale images.
		/// </summary>
		/// <param name="colors">Pallette to set</param>
		/// <param name="index">Palette index</param>
		public void SetPalette(Color[] colors, int index = -1)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);
			_gray6Palette = ColorUtil.GetPalette(colors, 64);
		}

		/// <summary>
		/// Removes a previously set palette
		/// </summary>
		public void ClearPalette()
		{
			_gray2Palette = null;
			_gray4Palette = null;
			_gray6Palette = null;
		}

		/// <summary>
		/// Resets the color
		/// </summary>
		public void ClearColor()
		{
			SetColor(DefaultColor);
		}

		/// <summary>
		/// Clears the display on all destinations.
		/// </summary>
		public void ClearDisplay()
		{
			Destinations.ForEach(dest => dest.ClearDisplay());
		}

		/// <summary>
		/// Disposes the graph and all elements.
		/// </summary>
		/// <remarks>
		/// Run this before exiting the application.
		/// </remarks>
		public void Dispose()
		{
			Logger.Debug("Disposing {0}...", Name);
			if (_activeRenderer != null) {
				_activeRenderer.Dispose();
				_activeRenderer = null;
			}
			if (Destinations == null) {
				return;
			}
			foreach (var dest in Destinations) {
				dest.Dispose();
			}
			foreach (var source in _activeSources) {
				source.Dispose();
			}
		}

		private byte[] TransformScaling(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			if ((dest != null && !dest.DmdAllowHdScaling) || (width * height == frame.Length))
			{
				return frame;
			}

			if (ScalerMode == ScalerMode.Doubler)
			{
				return FrameUtil.ScaleDouble(width, height, 4, frame);
			}
			else
			{
				return FrameUtil.Scale2x(width, height, frame);
			}

		}

		private byte[] TransformGray2(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			frame = TransformScaling(width, height, frame, dest);

			if (dest == null)
			{
				return TransformationUtil.Flip(width, height, 1, frame, FlipHorizontally, FlipVertically);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return frame;
			}
			if (width == dest.DmdWidth * 2 && height == dest.DmdHeight * 2 && !FlipHorizontally && !FlipVertically)
			{
				return width * height == frame.Length ? FrameUtil.ScaleDownFrame(dest.DmdWidth, dest.DmdHeight, frame) : frame;
			}

			// block-copy for same width but smaller height
			if (width == dest.DmdWidth && height < dest.DmdHeight && Resize != ResizeMode.Stretch && !FlipHorizontally && !FlipVertically) {
				var transformedFrame = new byte[dest.DmdWidth * dest.DmdHeight];
				Buffer.BlockCopy(frame, 0, transformedFrame, ((dest.DmdHeight - height) / 2) * dest.DmdWidth, frame.Length);
				return transformedFrame;
			}

			var bmp = ImageUtil.ConvertFromGray2(width, height, frame, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			return ImageUtil.ConvertToGray2(transformedBmp);
		}

		private byte[] TransformGray4(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			frame = TransformScaling(width, height, frame, dest);

			if (dest == null)
			{
				return TransformationUtil.Flip(width, height, 1, frame, FlipHorizontally, FlipVertically);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return frame;
			}
			if (width == dest.DmdWidth * 2 && height == dest.DmdHeight * 2 && !FlipHorizontally && !FlipVertically)
			{
				return width * height == frame.Length ? FrameUtil.ScaleDownFrame(dest.DmdWidth, dest.DmdHeight, frame) : frame;
			}

			var bmp = ImageUtil.ConvertFromGray4(width, height, frame, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray4(transformedBmp);
			return transformedFrame;
		}
		private byte[] TransformGray6(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			frame = TransformScaling(width, height, frame, dest);

			if (dest == null)
			{
				return TransformationUtil.Flip(width, height, 1, frame, FlipHorizontally, FlipVertically);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically)
			{
				return frame;
			}
			if (width == dest.DmdWidth * 2 && height == dest.DmdHeight * 2 && !FlipHorizontally && !FlipVertically)
			{
				return width * height == frame.Length ? FrameUtil.ScaleDownFrame(dest.DmdWidth, dest.DmdHeight, frame) : frame;
			}

			var bmp = ImageUtil.ConvertFromGray6(width, height, frame, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray6(transformedBmp);
			return transformedFrame;
		}

		private byte[] TransformRgb24(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
				var flipframe = TransformationUtil.Flip(width, height, 3, frame, FlipHorizontally, FlipVertically);
				if ((width * height * 3 != frame.Length)) { 
					if (ScalerMode == ScalerMode.Doubler)
					{
						return FrameUtil.ScaleDoubleRGB(width, height, 4, flipframe);
					}
					if (ScalerMode == ScalerMode.Scale2x)
					{
						return FrameUtil.Scale2xRGB(width, height, flipframe);
					}
				}
				return flipframe;
			}

			BitmapSource bmp;

			if (frame.Length == width / 2 * height / 2 * 3)
			{
				bmp = ImageUtil.ConvertFromRgb24(width/2, height/2, frame);
			}
			else
			{
				bmp = ImageUtil.ConvertFromRgb24(width, height, frame);
			}
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = new byte[dest.DmdWidth*dest.DmdHeight*3];
			ImageUtil.ConvertToRgb24(transformedBmp, transformedFrame);
			return transformedFrame;
		}

		private BitmapSource Transform(BitmapSource bmp, IFixedSizeDestination dest)
		{
			if (dest == null && !FlipHorizontally && !FlipVertically) {
				return bmp;
			}
			int width, height;
			if (dest == null) {
				width = bmp.PixelWidth;
				height = bmp.PixelHeight;
			} else {
				width = dest.DmdWidth;
				height = dest.DmdHeight;
			}
			return TransformationUtil.Transform(bmp, width, height, Resize, FlipHorizontally, FlipVertically);
		}

		/// <summary>
		/// Makes sure that a given source is compatible with a given destination or throws an exception.
		/// </summary>
		/// <param name="src">Original source</param>
		/// <param name="castedSource">Casted source, will be checked against null</param>
		/// <param name="dest">Original destination</param>
		/// <param name="castedDest">Casted source, will be checked against null</param>
		/// <param name="whatFrom">Name of the source</param>
		/// <param name="whatDest">Name of destination</param>
		private static void AssertCompatibility(ISource src, object castedSource, IDestination dest, object castedDest, string whatFrom, string whatDest = null)
		{
			if (castedSource == null && castedDest == null) {
				if (whatDest != null) {
					throw new IncompatibleRenderer($"Source \"${src.Name}\" is not ${whatFrom} compatible and destination \"${dest.Name}\" is not ${whatDest} compatible.");
				}
				throw new IncompatibleRenderer($"Neither source \"${src.Name}\" nor destination \"${dest.Name}\" are ${whatFrom} compatible.");
			}
			if (castedSource == null) {
				throw new IncompatibleRenderer("Source \"" + src.Name + "\" is not " + whatFrom + " compatible.");
			}
			AssertCompatibility(dest, castedDest, whatFrom, whatDest);
		}

		/// <summary>
		/// Makes sure that a given source is compatible with a given destination or throws an exception.
		/// </summary>
		/// <param name="dest">Original destination</param>
		/// <param name="castedDest">Casted source, will be checked against null</param>
		/// <param name="whatFrom">Name of the source</param>
		/// <param name="whatDest">Name of destination</param>
		// ReSharper disable once UnusedParameter.Local
		private static void AssertCompatibility(IDestination dest, object castedDest, string whatFrom, string whatDest)
		{
			if (castedDest == null) {
				throw new IncompatibleRenderer("Destination \"" + dest.Name + "\" is not " + whatDest + " compatible (" + whatFrom + ").");
			}
		}

		private static void AssertCompatibility(ISource src, object castedSource, IDestination dest, object castedDest, FrameFormat from, FrameFormat to)
		{
			AssertCompatibility(src, castedSource, dest, castedDest, from.ToString(), to.ToString());
		}
	}

	/// <summary>
	/// Defines how data is sent from a source and received by a destination.
	/// </summary>
	public enum FrameFormat
	{
		/// <summary>
		/// A 2-bit grayscale frame (4 shades)
		/// </summary>
		Gray2,

		/// <summary>
		/// A 4-bit grayscale frame (16 shades)
		/// </summary>
		Gray4,

		/// <summary>
		/// A 6-bit grayscale frame (64 shades)
		/// </summary>
		Gray6,

		/// <summary>
		/// An RGB24 frame
		/// </summary>
		Rgb24,

		/// <summary>
		/// An colored RGB24 frame
		/// </summary>
		ColoredRgb24,

		/// <summary>
		/// A bitmap
		/// </summary>
		Bitmap,

		/// <summary>
		/// An alphanumeric frame
		/// </summary>
		AlphaNumeric,
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
		private readonly CompositeDisposable _activeSources;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public RenderDisposable(RenderGraph graph, CompositeDisposable activeSources)
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

	/// <summary>
	/// Thrown when trying to convert a source incompatible with the convertor.
	/// </summary>
	public class IncompatibleSourceException : Exception
	{
		public IncompatibleSourceException(string message) : base(message)
		{
		}
	}

	/// <summary>
	/// Thrown when DmdDevice.ini was explicitly specified but not found.
	/// </summary>
	public class IniNotFoundException : Exception
	{
		public IniNotFoundException(string path) : base("Could not find DmdDevice.ini at " + path)
		{
		}
	}
}
