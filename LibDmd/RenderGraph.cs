﻿using System;
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
using LibDmd.Converter;
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
	/// A render graph can also contain an <see cref="IConverter"/>. These are
	/// classes that for a defined input format produce different output 
	/// formats. An example would be a 2-bit source that gets converted to
	/// RGB24, or, to colored 2- and 4-bit.
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
		/// If set, convert the frame format.
		/// </summary>
		public IConverter Converter { get; set; }

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

			// initialize converter
			if (Converter is ISource converter) {
				converter.Dimensions = Source.Dimensions;
			}
			Converter?.Init();

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
				Logger.Info("Setting up {0} for {1} destination(s)", Name, Destinations.Count);

				// init converters
				IColoredGray2Source coloredGray2SourceConverter = null;
				IColoredGray4Source coloredGray4SourceConverter = null;
				IColoredGray6Source coloredGray6SourceConverter = null;
				IRgb24Source rgb24SourceConverter = null;

				if (Converter != null && HasRgb24Destination()) {
					coloredGray2SourceConverter = Converter as IColoredGray2Source;
					coloredGray4SourceConverter = Converter as IColoredGray4Source;
					coloredGray6SourceConverter = Converter as IColoredGray6Source;
					// ReSharper disable once SuspiciousTypeConversion.Global
					rgb24SourceConverter = Converter as IRgb24Source;
					
					// send frames to converter
					switch (Converter.From) {
						case FrameFormat.Gray2:
							if (sourceGray2 == null) {
								throw new IncompatibleSourceException($"Source {Source.Name} is not 2-bit compatible which is mandatory for converter {coloredGray2SourceConverter?.Name}.");
							}
							_activeSources.Add(sourceGray2.GetGray2Frames().Do(Converter.Convert).Subscribe());
							break;
						case FrameFormat.Gray4:
							if (sourceGray4 == null) {
								throw new IncompatibleSourceException($"Source {Source.Name} is not 4-bit compatible which is mandatory for converter {coloredGray4SourceConverter?.Name}.");
							}
							_activeSources.Add(sourceGray4.GetGray4Frames().Do(Converter.Convert).Subscribe());
							break;
						case FrameFormat.Gray6:
							if (sourceGray6 == null)
							{
								throw new IncompatibleSourceException($"Source {Source.Name} is not 6-bit compatible which is mandatory for converter {coloredGray6SourceConverter?.Name}.");
							}
							_activeSources.Add(sourceGray6.GetGray6Frames().Do(Converter.Convert).Subscribe());
							break;
						default:
							throw new NotImplementedException($"Frame convertion from ${Converter.From} is not implemented.");
					}
				}

				foreach (var dest in Destinations) {

					var destColoredGray2 = dest as IColoredGray2Destination;
					var destColoredGray4 = dest as IColoredGray4Destination;
					var destColoredGray6 = dest as IColoredGray6Destination;
					var destRgb24 = dest as IRgb24Destination;

					// So here's how convertors work:
					// They have one input type, given by IConvertor.From, but they can randomly 
					// output frames in different formats. For example, the ColoredGray2Colorizer
					// outputs in ColoredGray2 or ColoredGray4, depending if data is enhanced
					// or not.
					//
					// So for the output, the converter acts as ISource, implementing the specific 
					// interfaces supported. Currently the following output sources are supported:
					//
					//    IColoredGray2Source, IColoredGray4Source and IRgb24Source.
					//
					// Other types don't make much sense (i.e. you don't convert *down* to 
					// IGray2Source).
					//
					// In the block below, those sources are linked to each destination. If a 
					// destination doesn't support a colored gray source, it tries to convert
					// it up to RGB24, otherwise fails. For example, PinDMD3 which only supports
					// IColoredGray2Source but not IColoredGray4Source due to bad software design
					// will get the IColoredGray4Source converted up to RGB24.
					if (Converter != null && destRgb24 != null) {
						
						// if converter emits colored gray-2 frames..
						if (coloredGray2SourceConverter != null) {
							// if destination can render colored gray-2 frames...
							if (destColoredGray2 != null) {
								//Logger.Info("Hooking colored 2-bit source of {0} converter to {1}.", coloredGray2SourceConverter.Name, dest.Name);
								Connect(coloredGray2SourceConverter, destColoredGray2, FrameFormat.ColoredGray2, FrameFormat.ColoredGray2);

							// otherwise, try to convert to rgb24
							} else {
								//Logger.Warn("Destination {0} doesn't support colored 2-bit frames from {1} converter, converting to RGB source.", dest.Name, coloredGray2SourceConverter.Name);
								Connect(coloredGray2SourceConverter, destRgb24, FrameFormat.ColoredGray2, FrameFormat.Rgb24);
							}
						}

						// if converter emits colored gray-4 frames..
						if (coloredGray4SourceConverter != null) {
							// if destination can render colored gray-4 frames...
							if (destColoredGray4 != null) {
								//Logger.Info("Hooking colored 4-bit source of {0} converter to {1}.", coloredGray4SourceConverter.Name, dest.Name);
								Connect(coloredGray4SourceConverter, destColoredGray4, FrameFormat.ColoredGray4, FrameFormat.ColoredGray4);

							// otherwise, convert to rgb24
							} else {
								//Logger.Warn("Destination {0} doesn't support colored 4-bit frames from {1} converter, converting to RGB source.", dest.Name, coloredGray4SourceConverter.Name);
								Connect(coloredGray4SourceConverter, destRgb24, FrameFormat.ColoredGray4, FrameFormat.Rgb24);
							}
						}

						// if converter emits colored gray-6 frames..
						if (coloredGray6SourceConverter != null)
						{
							// if destination can render colored gray-6 frames...
							if (destColoredGray6 != null)
							{
								//Logger.Info("Hooking colored 6-bit source of {0} converter to {1}.", coloredGray6SourceConverter.Name, dest.Name);
								Connect(coloredGray6SourceConverter, destColoredGray6, FrameFormat.ColoredGray6, FrameFormat.ColoredGray6);

								// otherwise, convert to rgb24
							} else {
								//Logger.Warn("Destination {0} doesn't support colored 6-bit frames from {1} converter, converting to RGB source.", dest.Name, coloredGray6SourceConverter.Name);
								Connect(coloredGray6SourceConverter, destRgb24, FrameFormat.ColoredGray6, FrameFormat.Rgb24);
							}
						}

						// if converter emits RGB24 frames..
						if (rgb24SourceConverter != null) {
							Logger.Info("Hooking RGB24 source of {0} converter to {1}.", rgb24SourceConverter.Name, dest.Name);
							Connect(rgb24SourceConverter, destRgb24, FrameFormat.Rgb24, FrameFormat.Rgb24);
						}

						// render graph is already set up through converters, so we skip the rest below
						continue;
					}

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
					var destGray6 = dest as IGray6Destination;
					var destBitmap = dest as IBitmapDestination;
					var destAlphaNumeric = dest as IAlphaNumericDestination;

					var sourceColoredGray2 = Source as IColoredGray2Source;
					var sourceColoredGray4 = Source as IColoredGray4Source;
					var sourceColoredGray6 = Source as IColoredGray6Source;
					var sourceRgb24 = Source as IRgb24Source;
					var sourceBitmap = Source as IBitmapSource;
					var sourceAlphaNumeric = Source as IAlphaNumericSource;

					// first, check if we do without conversion
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
					// gray6 -> gray6
					if (sourceGray6 != null && destGray6 != null)
					{
						Connect(Source, dest, FrameFormat.Gray6, FrameFormat.Gray6);
						continue;
					}
					// colored gray2 -> colored gray2
					if (sourceColoredGray2 != null && destColoredGray2 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray2, FrameFormat.ColoredGray2);
						continue;
					}
					// colored gray4 -> colored gray4
					if (sourceColoredGray4 != null && destColoredGray4 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray4, FrameFormat.ColoredGray4);
						continue;
					}
					// colored gray6 -> colored gray6
					if (sourceColoredGray6 != null && destColoredGray6 != null)
					{
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.ColoredGray6);
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
					// colored gray2 -> rgb24
					if (sourceColoredGray2 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray2, FrameFormat.Rgb24);
						continue;
					}
					// colored gray2 -> bitmap
					if (sourceColoredGray2 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.ColoredGray2, FrameFormat.Bitmap);
						continue;
					}
					// colored gray4 -> rgb24
					if (sourceColoredGray4 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray4, FrameFormat.Rgb24);
						continue;
					}
					// colored gray4 -> bitmap
					if (sourceColoredGray4 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.ColoredGray4, FrameFormat.Bitmap);
						continue;
					}
					// colored gray6 -> rgb24
					if (sourceColoredGray6 != null && destRgb24 != null)
					{
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.Rgb24);
						continue;
					}
					// colored gray6 -> bitmap
					if (sourceColoredGray6 != null && destBitmap != null)
					{
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.Bitmap);
						continue;
					}

					// finally, here we lose data
					// gray4 -> gray2
					if (sourceGray4 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Gray2);
						continue;
					}
					// colored gray4 -> gray4
					if (sourceColoredGray4 != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray4, FrameFormat.Gray4);
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
					// colored gray2 -> gray2
					if (sourceColoredGray2 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray2, FrameFormat.Gray2);
						continue;
					}
					// colored gray4 -> gray2
					if (sourceColoredGray4 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray4, FrameFormat.Gray2);
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
			var destGray6 = dest as IGray6Destination;
			var destRgb24 = dest as IRgb24Destination;
			var destBitmap = dest as IBitmapDestination;
			var destColoredGray2 = dest as IColoredGray2Destination;
			var destColoredGray4 = dest as IColoredGray4Destination;
			var destColoredGray6 = dest as IColoredGray6Destination;
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
									.Select(frame => ColorizeGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data))
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray2, dest, destBitmap, from, to);
							Subscribe(sourceGray2.GetGray2Frames()
									.Select(frame => ImageUtil.ConvertFromRgb24(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										ColorizeGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data)
									))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						// gray2 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from gray2 to colored gray2 (doesn't make any sense, colored gray2 can also do gray2).");

						// gray2 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from gray2 to colored gray2 (a colored gray4 destination should also be able to do gray2 directly).");

						// gray2 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new NotImplementedException("Cannot convert from gray2 to colored gray6 (a colored gray6 destination should also be able to do gray2 directly).");

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
									.Select(frame => ColorizeGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data))
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray4, dest, destBitmap, from, to);
							Subscribe(sourceGray4.GetGray4Frames()
									.Select(frame => ImageUtil.ConvertFromRgb24(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										ColorizeGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data)
									))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						// gray4 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from gray4 to colored gray2 (doesn't make any sense, colored gray2 can also do gray4).");

						// gray4 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from gray4 to colored gray4 (doesn't make any sense, colored gray4 can also do gray2).");

						// gray4 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new NotImplementedException("Cannot convert from gray4 to colored gray6 (doesn't make any sense, colored gray6 can also do gray4).");

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is gray6:
				case FrameFormat.Gray6:
					var sourceGray6 = source as IGray6Source;
					switch (to)
					{
						// gray6 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceGray6, dest, destGray2, from, to);
							Subscribe(sourceGray6.GetGray6Frames()
									.Select(frame => FrameUtil.ConvertGrayToGray(frame.Data, new byte[] { 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3, 0x3, 0x3}))
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// gray6 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceGray6, dest, destGray4, from, to);
							Subscribe(sourceGray6.GetGray6Frames()
									.Select(frame => FrameUtil.ConvertGrayToGray(frame.Data, new byte[] { 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3, 0x4, 0x4, 0x4, 0x4, 0x5, 0x5, 0x5, 0x5, 0x6, 0x6, 0x6, 0x6, 0x7, 0x7, 0x7, 0x7 }))
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray4.RenderGray4);
							break;

						// gray6 -> gray6
						case FrameFormat.Gray6:
							AssertCompatibility(source, sourceGray6, dest, destGray6, from, to);
							Subscribe(sourceGray6.GetGray6Frames()
									.Select(frame => TransformGray6(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, destFixedSize)),
								destGray6.RenderGray6);
							break;

						// gray6 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray6, dest, destRgb24, from, to);
							Subscribe(sourceGray6.GetGray6Frames()
									.Select(frame => ColorizeGray6(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data))
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// gray6 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray6, dest, destBitmap, from, to);
							Subscribe(sourceGray6.GetGray6Frames()
									.Select(frame => ImageUtil.ConvertFromRgb24(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										ColorizeGray6(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data)
									))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						// gray6 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from gray4 to colored gray2 (doesn't make any sense, colored gray2 can also do gray4).");

						// gray6 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from gray4 to colored gray4 (doesn't make any sense, colored gray4 can also do gray2).");

						// gray6 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new NotImplementedException("Cannot convert from gray4 to colored gray6 (doesn't make any sense, colored gray6 can also do gray4).");

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

						// rgb24 -> gray6
						case FrameFormat.Gray6:
							AssertCompatibility(source, sourceRgb24, dest, destGray6, from, to);
							Subscribe(sourceRgb24.GetRgb24Frames()
									.Select(frame => ImageUtil.ConvertToGray(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Data, 64))
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray6.RenderGray6);
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

						// rgb24 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from rgb24 to colored gray2 (colored gray2 only has 4 colors per frame).");

						// rgb24 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from rgb24 to colored gray2 (colored gray4 only has 16 colors per frame).");

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

						// bitmap -> gray6
						case FrameFormat.Gray6:
							AssertCompatibility(source, sourceBitmap, dest, destGray6, from, to);
							Subscribe(sourceBitmap.GetBitmapFrames()
									.Select(bmp => ImageUtil.ConvertToGray6(bmp))
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray6.RenderGray6);
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

						// bitmap -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from bitmap to colored gray2 (colored gray2 only has 4 colors per frame).");

						// bitmap -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from bitmap to colored gray2 (colored gray4 only has 16 colors per frame).");

						// bitmap -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new NotImplementedException("Cannot convert from bitmap to colored gray6 (colored gray6 only has 64 colors per frame).");

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is colored gray2:
				case FrameFormat.ColoredGray2:
					var sourceColoredGray2 = source as IColoredGray2Source;
					switch (to) {

						// colored gray2 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceColoredGray2, dest, destGray2, from, to);
							Subscribe(sourceColoredGray2.GetColoredGray2Frames()
									.Select(frame => FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes))
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// colored gray2 -> gray4
						case FrameFormat.Gray4:
							throw new NotImplementedException("Cannot convert from colored gray2 to gray4 (it's not like we can extract luminosity from the colors...)");

						// colored gray2 -> gray6
						case FrameFormat.Gray6:
							throw new NotImplementedException("Cannot convert from colored gray2 to gray6 (it's not like we can extract luminosity from the colors...)");

						// colored gray2 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray2, dest, destRgb24, from, to);
							Subscribe(sourceColoredGray2.GetColoredGray2Frames()
									.Select(frame => ColorUtil.ColorizeFrame(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height, 
										FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes), 
										frame.Palette)
									)
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// colored gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray2, dest, destBitmap, from, to);
							Subscribe(sourceColoredGray2.GetColoredGray2Frames()
									.Select(frame => ColorUtil.ColorizeFrame(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes),
										frame.Palette)
									)
									.Select(frame => ImageUtil.ConvertFromRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						// colored gray2 -> colored gray2
						case FrameFormat.ColoredGray2:
							AssertCompatibility(source, sourceColoredGray2, dest, destColoredGray2, from, to);
							Subscribe(sourceColoredGray2.GetColoredGray2Frames()
									.Select(frame => TransformColoredGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)), destColoredGray2.RenderColoredGray2);
							break;

						// colored gray2 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from colored gray2 to colored gray4 (if a destination can do colored gray4 it should be able to do colored gray2 directly).");

						case FrameFormat.ColoredGray6:
							throw new NotImplementedException("Cannot convert from colored gray2 to colored gray6 (if a destination can do colored gray6 it should be able to do colored gray2 directly).");

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is colored gray4:
				case FrameFormat.ColoredGray4:
					var sourceColoredGray4 = source as IColoredGray4Source;
					switch (to) {

						// colored gray4 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceColoredGray4, dest, destGray2, from, to);
							Subscribe(sourceColoredGray4.GetColoredGray4Frames()
									.Select(frame => FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes))
									.Select(frame => FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3 }))
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// colored gray4 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceColoredGray4, dest, destGray4, from, to);
							Subscribe(sourceColoredGray4.GetColoredGray4Frames()
									.Select(frame => FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes))
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray4.RenderGray4);
							break;

						// colored gray4 -> gray6
						case FrameFormat.Gray6:
							throw new NotImplementedException("Cannot convert from colored gray4 to gray6 (it's not like we can extract luminosity from the colors...)");

						// colored gray4 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray4, dest, destRgb24, from, to);
							Subscribe(sourceColoredGray4.GetColoredGray4Frames()
									.Select(frame => ColorUtil.ColorizeFrame(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes),
										frame.Palette)
									)
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// colored gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray4, dest, destBitmap, from, to);
							Subscribe(
								sourceColoredGray4.GetColoredGray4Frames()
									.Select(frame => ColorUtil.ColorizeFrame(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes),
										frame.Palette)
									)
									.Select(frame => ImageUtil.ConvertFromRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						// colored gray4 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from colored gray4 to colored gray2 (use rgb24 instead of down-coloring).");

						// colored gray4 -> colored gray4
						case FrameFormat.ColoredGray4:
							AssertCompatibility(source, sourceColoredGray4, dest, destColoredGray4, from, to);
							Subscribe(sourceColoredGray4.GetColoredGray4Frames()
									.Select(frame => TransformColoredGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destColoredGray4.RenderColoredGray4);
							break;
						
						// colored gray4 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new NotImplementedException("Cannot convert from colored gray4 to colored gray6 (use rgb24 instead of down-coloring).");

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is colored gray6:
				case FrameFormat.ColoredGray6:
					var sourceColoredGray6 = source as IColoredGray6Source;
					switch (to)
					{

						// colored gray6 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceColoredGray6, dest, destGray2, from, to);
							Subscribe(sourceColoredGray6.GetColoredGray6Frames()
									.Select(frame => FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes))
									.Select(frame => FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3 }))
									.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray2.RenderGray2);
							break;

						// colored gray6 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceColoredGray6, dest, destGray4, from, to);
							Subscribe(sourceColoredGray6.GetColoredGray6Frames()
									.Select(frame => FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes))
									.Select(frame => FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3 }))
									.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray4.RenderGray4);
							break;

						// colored gray6 -> gray6
						case FrameFormat.Gray6:
							AssertCompatibility(source, sourceColoredGray6, dest, destGray6, from, to);
							Subscribe(sourceColoredGray6.GetColoredGray6Frames()
									.Select(frame => FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes))
									.Select(frame => TransformGray6(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destGray6.RenderGray6);
							break;

						// colored gray6 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray6, dest, destRgb24, from, to);
							Subscribe(sourceColoredGray6.GetColoredGray6Frames()
									.Select(frame => ColorUtil.ColorizeFrame(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes),
										frame.Palette)
									)
									.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destRgb24.RenderRgb24);
							break;

						// colored gray6 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray6, dest, destBitmap, from, to);
							Subscribe(
								sourceColoredGray6.GetColoredGray6Frames()
									.Select(frame => ColorUtil.ColorizeFrame(
										source.Dimensions.Value.Width,
										source.Dimensions.Value.Height,
										FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame.Planes),
										frame.Palette)
									)
									.Select(frame => ImageUtil.ConvertFromRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
									.Select(bmp => Transform(bmp, destFixedSize)),
								destBitmap.RenderBitmap);
							break;

						// colored gray6 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from colored gray4 to colored gray2 (use rgb24 instead of down-coloring).");

						// colored gray6 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from colored gray6 to colored gray4 (use rgb24 instead of down-coloring).");

						// colored gray6 -> colored gray6
						case FrameFormat.ColoredGray6:
							AssertCompatibility(source, sourceColoredGray6, dest, destColoredGray6, from, to);
							Subscribe(sourceColoredGray6.GetColoredGray6Frames()
									.Select(frame => TransformColoredGray6(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize)),
								destColoredGray6.RenderColoredGray6);
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

		private byte[] TransformGray2(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
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
			if (dest == null) {
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

		private ColoredFrame TransformColoredGray2(int width, int height, ColoredFrame frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return new ColoredFrame(TransformationUtil.Flip(width, height, frame.Planes, FlipHorizontally, FlipVertically), frame.Palette, frame.PaletteIndex);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return frame;
			}
			if (width == dest.DmdWidth * 2 && height == dest.DmdHeight * 2 && !FlipHorizontally && !FlipVertically)
			{
				return new ColoredFrame(FrameUtil.ScaleDown(dest.DmdWidth, dest.DmdHeight, frame.Planes), frame.Palette, frame.PaletteIndex);
			}

			var bmp = ImageUtil.ConvertFromGray2(width, height, FrameUtil.Join(width, height, frame.Planes), 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray2(transformedBmp);
			return new ColoredFrame(FrameUtil.Split(dest.DmdWidth, dest.DmdHeight, 2, transformedFrame), frame.Palette, frame.PaletteIndex);
		}

		private ColoredFrame TransformColoredGray4(int width, int height, ColoredFrame frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return new ColoredFrame(TransformationUtil.Flip(width, height, frame.Planes, FlipHorizontally, FlipVertically), frame.Palette, frame.PaletteIndex);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return frame;
			}
			if (width == dest.DmdWidth * 2 && height == dest.DmdHeight * 2 && !FlipHorizontally && !FlipVertically)
			{
				return new ColoredFrame(FrameUtil.ScaleDown(dest.DmdWidth, dest.DmdHeight, frame.Planes), frame.Palette, frame.PaletteIndex);
			}

			var bmp = ImageUtil.ConvertFromGray4(width, height, FrameUtil.Join(width, height, frame.Planes), 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray4(transformedBmp);
			return new ColoredFrame(FrameUtil.Split(dest.DmdWidth, dest.DmdHeight, 4, transformedFrame), frame.Palette, frame.PaletteIndex);
		}

		private ColoredFrame TransformColoredGray6(int width, int height, ColoredFrame frame, IFixedSizeDestination dest)
		{
			if (dest == null)
			{
				return new ColoredFrame(TransformationUtil.Flip(width, height, frame.Planes, FlipHorizontally, FlipVertically), frame.Palette, frame.PaletteIndex);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically)
			{
				return frame;
			}
			if (width == dest.DmdWidth * 2 && height == dest.DmdHeight * 2 && !FlipHorizontally && !FlipVertically)
			{
				return new ColoredFrame(FrameUtil.ScaleDown(dest.DmdWidth, dest.DmdHeight, frame.Planes),frame.Palette, frame.PaletteIndex);
			}

			var bmp = ImageUtil.ConvertFromGray6(width, height, FrameUtil.Join(width, height, frame.Planes), 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray6(transformedBmp);
			return new ColoredFrame(FrameUtil.Split(dest.DmdWidth, dest.DmdHeight, 6, transformedFrame), frame.Palette, frame.PaletteIndex);
		}

		private byte[] TransformRgb24(int width, int height, byte[] frame, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return TransformationUtil.Flip(width, height, 3, frame, FlipHorizontally, FlipVertically);
			}
			var bmp = ImageUtil.ConvertFromRgb24(width, height, frame);
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
		/// A bitmap
		/// </summary>
		Bitmap,

		/// <summary>
		/// A 2-bit grayscale frame bundled with a 4-color palette
		/// </summary>
		ColoredGray2,

		/// <summary>
		/// A 4-bit grayscale frame bundled with a 16-color palette
		/// </summary>
		ColoredGray4,

		/// <summary>
		/// A 6-bit grayscale frame bundled with a 64-color palette
		/// </summary>
		ColoredGray6,

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
