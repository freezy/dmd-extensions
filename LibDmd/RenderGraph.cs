using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Converter;
using NLog;
using LibDmd.Input;
using LibDmd.Input.Passthrough;
using LibDmd.Input.TPAGrabber;
using LibDmd.Output;
using ResizeMode = LibDmd.Input.ResizeMode;

namespace LibDmd
{
	/// <summary>
	/// A render pipeline. This the core of LibDmd.
	/// 
	/// Every render graph has one <see cref="ISource"/> and one or more 
	/// <see cref="IDestination"/>. Frames produced by the source are 
	/// dispatched to all destinations. Sources and destinations can be re-used
	/// in other graphs.
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
		/// If set, flips the image vertically (top/down).
		/// </summary>
		public bool FlipVertically { get; set; }

		/// <summary>
		/// If set, flips the image horizontally (left/right).
		/// </summary>
		public bool FlipHorizontally { get; set; }

		/// <summary>
		/// How the image is resized for destinations with fixed width
		/// </summary>
		public ResizeMode Resize { get; set; } = ResizeMode.Stretch;

		/// <summary>
		/// The default color used if there are no palette is defined
		/// </summary>
		public static readonly Color DefaultColor = Colors.OrangeRed;

		private readonly List<IDisposable> _activeSources = new List<IDisposable>();
		private readonly Subject<BitmapSource> _beforeProcessed = new Subject<BitmapSource>();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Color[] _gray2Colors; 
		private Color[] _gray4Colors; 
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		public RenderGraph()
		{
			ClearColor();
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
			StartRendering(onCompleted);
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
		///  <param name="onError">When a known error occurs.</param>
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
				Logger.Info("Setting up {0} for {1} destination(s)", Name, Destinations.Count);
				foreach (var dest in Destinations) 
				{
					var destResizable = dest as IResizableDestination;

					var destColoredGray2 = dest as IColoredGray2Destination;
					var destColoredGray4 = dest as IColoredGray4Destination;
					var destRgb24 = dest as IRgb24Destination;

					if (destResizable != null) {
						Source.Dimensions.Subscribe(dim => destResizable.SetDimensions(dim.Width, dim.Height));
					}

					// So here's how convertors work:
					// They have one input type, given by IConvertor.From, but they can randomly 
					// output frames in different formats. For example, the ColoredGray2Colorizer
					// outputs in ColoredGray2 or ColoredGray4, depending if data is enhanced
					// or not.
					// So for the output, the converter acts as ISource, implementing the specific 
					// interfaces supported. Currently the following output sources are supported:
					//    - IColoredGray2Source, IColoredGray4Source and IRgb24Source
					// Other types don't make much sense (i.e. you don't convert *down* to 
					// IGray2Source).
					// In the code below, those sources are linked to each destination. If a 
					// destination doesn't support a colored gray source, it tries to convert
					// it up to RGB24, otherwise fails. For example, PinDMD3 which only supports
					// IColoredGray2Source but not IColoredGray4Source due to bad software design
					// will get the IColoredGray4Source converted up to RGB24.
					if (Converter != null && destRgb24 != null) {
						var coloredGray2SourceConverter = Converter as IColoredGray2Source;
						var coloredGray4SourceConverter = Converter as IColoredGray4Source;
						// ReSharper disable once SuspiciousTypeConversion.Global
						var rgb24SourceConverter = Converter as IRgb24Source;

						// send frames to converter
						switch (Converter.From) {
							case FrameFormat.Gray2:
								if (sourceGray2 == null) {
									throw new IncompatibleSourceException($"Source {Source.Name} is not 2-bit compatible which is mandatory for converter {rgb24SourceConverter?.Name}.");
								}
								_activeSources.Add(sourceGray2.GetGray2Frames().Do(Converter.Convert).Subscribe());
								break;
							case FrameFormat.Gray4:
								if (sourceGray4 == null) {
									throw new IncompatibleSourceException($"Source {Source.Name} is not 4-bit compatible which is mandatory for converter {rgb24SourceConverter?.Name}.");
								}
								_activeSources.Add(sourceGray4.GetGray4Frames().Do(Converter.Convert).Subscribe());
								break;
							default:
								throw new NotImplementedException($"Frame convertion from ${Converter.From} is not implemented.");
						}

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

					// Since we know that a source doesn't implement any interface that would result
					// in data loss (e.g. a 4-bit source will not implement IGray2Source), we start
					// looking at the most performant combinations first.

					// So first we try to match the source format with the destination format. Then
					// we go on by looking at "upscaling" convertions, e.g. if a destination only
					// supports RGB24, then convert 2-bit to RGB24. Lastly we check "downscaling"
					// conversions, e.g. convert an RGB24 frame to 2-bit for outputs like PinDMD1
					// that can only render 4 shades.

					var destGray2 = dest as IGray2Destination;
					var destGray4 = dest as IGray4Destination;
					var destBitmap = dest as IBitmapDestination;

					var sourceColoredGray2 = Source as IColoredGray2Source;
					var sourceColoredGray4 = Source as IColoredGray4Source;
					var sourceRgb24 = Source as IRgb24Source;
					var sourceBitmap = Source as IBitmapSource;

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

					// finally, here we lose data
					// gray4 -> gray2
					if (sourceGray4 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Gray2);
						continue;
					}
					// rgb24 -> gray2
					if (sourceRgb24 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Rgb24, FrameFormat.Gray2);
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
					// bitmap -> gray2
					if (sourceBitmap != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Bitmap, FrameFormat.Gray2);
						continue;
					}

					// log status
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
		private void Connect(ISource source, IDestination dest, FrameFormat from, FrameFormat to)
		{
			var destFixedSize = dest as IFixedSizeDestination;
			var destGray2 = dest as IGray2Destination;
			var destGray4 = dest as IGray4Destination;
			var destRgb24 = dest as IRgb24Destination;
			var destBitmap = dest as IBitmapDestination;
			var destColoredGray2 = dest as IColoredGray2Destination;
			var destColoredGray4 = dest as IColoredGray4Destination;
			Logger.Info("Connecting {0} to {1} ({2} => {3})", source.Name, dest.Name, from.ToString(), to.ToString());

			switch (from) { 

				// source is gray2:
				case FrameFormat.Gray2:
					var sourceGray2 = source as IGray2Source;
					switch (to)
					{
						// gray2 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceGray2, dest, destGray2, from, to);
							_activeSources.Add(sourceGray2.GetGray2Frames()
								.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray2.RenderGray2));
							break;

						// gray2 -> gray4
						case FrameFormat.Gray4:
							throw new NotImplementedException("Cannot convert from gray2 to gray4 (every gray4 destination should be able to do gray2 as well).");

						// gray2 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray2, dest, destRgb24, from, to);
							_activeSources.Add(sourceGray2.GetGray2Frames()
								.Select(frame => ColorizeGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
								.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destRgb24.RenderRgb24));
							break;

						// gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray2, dest, destBitmap, from, to);
							_activeSources.Add(sourceGray2.GetGray2Frames()
								.Select(frame => ImageUtil.ConvertFromRgb24(
									source.Dimensions.Value.Width,
									source.Dimensions.Value.Height,
									ColorizeGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame)
								))
								.Select(bmp => Transform(bmp, destFixedSize))
								.Subscribe(destBitmap.RenderBitmap));
							break;

						// gray2 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from gray2 to colored gray2 (doesn't make any sense, colored gray2 can also do gray2).");

						// gray2 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from gray2 to colored gray2 (a colored gray4 destination should also be able to do gray2 directly).");

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
							_activeSources.Add(sourceGray4.GetGray4Frames()
								.Select(frame => FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x3 }))
								.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray2.RenderGray2));
							break;

						// gray4 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceGray4, dest, destGray4, from, to);
							_activeSources.Add(sourceGray4.GetGray4Frames()
								.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray4.RenderGray4));
							break;

						// gray4 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray4, dest, destRgb24, from, to);
							_activeSources.Add(sourceGray4.GetGray4Frames()
								.Select(frame => ColorizeGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
								.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destRgb24.RenderRgb24));
							break;

						// gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray4, dest, destBitmap, from, to);
							_activeSources.Add(sourceGray4.GetGray4Frames()
								.Select(frame => ImageUtil.ConvertFromRgb24(
									source.Dimensions.Value.Width,
									source.Dimensions.Value.Height,
									ColorizeGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame)
								))
								.Select(bmp => Transform(bmp, destFixedSize))
								.Subscribe(destBitmap.RenderBitmap));
							break;

						// gray4 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from gray4 to colored gray2 (doesn't make any sense, colored gray2 can also do gray4).");

						// gray4 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from gray2 to colored gray2 (doesn't make any sense, colored gray4 can also do gray4).");

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
							_activeSources.Add(sourceRgb24.GetRgb24Frames()
								.Select(frame => ImageUtil.ConvertToGray(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, 4))
								.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray2.RenderGray2));
							break;

						// rgb24 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceRgb24, dest, destGray4, from, to);
							_activeSources.Add(sourceRgb24.GetRgb24Frames()
								.Select(frame => ImageUtil.ConvertToGray(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, 16))
								.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray4.RenderGray4));
							break;

						// rgb24 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceRgb24, dest, destRgb24, from, to);
							_activeSources.Add(sourceRgb24.GetRgb24Frames()
								.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destRgb24.RenderRgb24));
							break;

						// rgb24 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceRgb24, dest, destBitmap, from, to);
							_activeSources.Add(sourceRgb24.GetRgb24Frames()
								.Select(frame => ImageUtil.ConvertFromRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
								.Select(bmp => Transform(bmp, destFixedSize))
								.Subscribe(destBitmap.RenderBitmap));
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
							_activeSources.Add(sourceBitmap.GetBitmapFrames()
								.Select(bmp => ImageUtil.ConvertToGray2(bmp))
								.Select(frame => TransformGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray2.RenderGray2));
							break;

						// bitmap -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceBitmap, dest, destGray4, from, to);
							_activeSources.Add(sourceBitmap.GetBitmapFrames()
								.Select(bmp => ImageUtil.ConvertToGray4(bmp))
								.Select(frame => TransformGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destGray4.RenderGray4));
							break;

						// bitmap -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceBitmap, dest, destRgb24, from, to);
							_activeSources.Add(sourceBitmap.GetBitmapFrames()
								.Select(bmp => ImageUtil.ConvertToRgb24(bmp))
								.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destRgb24.RenderRgb24));
							break;

						// bitmap -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(Source, sourceBitmap, dest, destBitmap, from, to);
							_activeSources.Add(sourceBitmap.GetBitmapFrames()
								.Select(bmp => Transform(bmp, destFixedSize))
								.Subscribe(destBitmap.RenderBitmap));
							break;

						// bitmap -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from bitmap to colored gray2 (colored gray2 only has 4 colors per frame).");

						// bitmap -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from bitmap to colored gray2 (colored gray4 only has 16 colors per frame).");

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
							throw new NotImplementedException("Cannot convert from colored gray2 to gray2 (just use gray2 without palette!)");

						// colored gray2 -> gray4
						case FrameFormat.Gray4:
							throw new NotImplementedException("Cannot convert from colored gray2 to gray4 (it's not like we can extract luminosity from the colors...)");

						// colored gray2 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray2, dest, destRgb24, from, to);
							_activeSources.Add(sourceColoredGray2.GetColoredGray2Frames()
								.Select(x => ColorUtil.ColorizeFrame(
									source.Dimensions.Value.Width,
									source.Dimensions.Value.Height, 
									FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, x.Item1), 
									x.Item2)
								)
								.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destRgb24.RenderRgb24));
							break;

						// colored gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray2, dest, destBitmap, from, to);
							_activeSources.Add(sourceColoredGray2.GetColoredGray2Frames()
								.Select(x => ColorUtil.ColorizeFrame(
									source.Dimensions.Value.Width,
									source.Dimensions.Value.Height,
									FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, x.Item1),
									x.Item2)
								)
								.Select(frame => ImageUtil.ConvertFromRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
								.Select(bmp => Transform(bmp, destFixedSize))
								.Subscribe(destBitmap.RenderBitmap));
							break;

						// colored gray2 -> colored gray2
						case FrameFormat.ColoredGray2:
							AssertCompatibility(source, sourceColoredGray2, dest, destColoredGray2, from, to);
							_activeSources.Add(sourceColoredGray2.GetColoredGray2Frames()
								.Select(x => TransformColoredGray2(source.Dimensions.Value.Width, source.Dimensions.Value.Height, x.Item1, x.Item2, destFixedSize))
								.Subscribe(x => destColoredGray2.RenderColoredGray2(x.Item1, x.Item2)));
							break;

						// colored gray2 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new NotImplementedException("Cannot convert from colored gray2 to colored gray4 (if a destination can do colored gray4 it should be able to do colored gray2 directly).");

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
							throw new NotImplementedException("Cannot convert from colored gray4 to gray2 (use gray4 without palette)");

						// colored gray4 -> gray4
						case FrameFormat.Gray4:
							throw new NotImplementedException("Cannot convert from colored gray4 to gray4 (just use gray4 without palette!)");

						// colored gray4 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray4, dest, destRgb24, from, to);
							_activeSources.Add(sourceColoredGray4.GetColoredGray4Frames()
								.Select(x => ColorUtil.ColorizeFrame(
									source.Dimensions.Value.Width,
									source.Dimensions.Value.Height,
									FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, x.Item1),
									x.Item2)
								)
								.Select(frame => TransformRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame, destFixedSize))
								.Subscribe(destRgb24.RenderRgb24));
							break;

						// colored gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray4, dest, destBitmap, from, to);
							_activeSources.Add(sourceColoredGray4.GetColoredGray4Frames()
								.Select(x => ColorUtil.ColorizeFrame(
									source.Dimensions.Value.Width,
									source.Dimensions.Value.Height,
									FrameUtil.Join(source.Dimensions.Value.Width, source.Dimensions.Value.Height, x.Item1),
									x.Item2)
								)
								.Select(frame => ImageUtil.ConvertFromRgb24(source.Dimensions.Value.Width, source.Dimensions.Value.Height, frame))
								.Select(bmp => Transform(bmp, destFixedSize))
								.Subscribe(destBitmap.RenderBitmap));
							break;

						// colored gray4 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new NotImplementedException("Cannot convert from colored gray4 to colored gray2 (use rgb24 instead of down-coloring).");

						// colored gray4 -> colored gray4
						case FrameFormat.ColoredGray4:
							AssertCompatibility(source, sourceColoredGray4, dest, destColoredGray4, from, to);
							_activeSources.Add(sourceColoredGray4.GetColoredGray4Frames()
								.Select(x => TransformColoredGray4(source.Dimensions.Value.Width, source.Dimensions.Value.Height, x.Item1, x.Item2, destFixedSize))
								.Subscribe(x => destColoredGray4.RenderColoredGray4(x.Item1, x.Item2)));
							break;
						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
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

		/// <summary>
		/// Sets the color with which a grayscale source is rendered on the RGB display.
		/// </summary>
		/// <param name="color">Rendered color</param>
		public void SetColor(Color color)
		{
			// set color for internal coloring
			_gray2Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 4);
			_gray4Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 16);

			// set color for external coloring
			if (Destinations == null) {
				return;
			}
			foreach (var rgbDest in Destinations.Select(d => d as IRgb24Destination)) {
				rgbDest?.SetColor(color);
			}
		}

		/// <summary>
		/// Sets the palette for rendering grayscale images.
		/// </summary>
		/// <param name="colors"></param>
		public void SetPalette(Color[] colors)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);

			if (Destinations == null) {
				return;
			}
			foreach (var rgbDest in Destinations.Select(d => d as IRgb24Destination)) {
				rgbDest?.SetPalette(colors);
			}
		}

		/// <summary>
		/// Removes a previously set palette
		/// </summary>
		public void ClearPalette()
		{
			_gray2Palette = null;
			_gray4Palette = null;
			if (Destinations == null) {
				return;
			}
			foreach (var rgbDest in Destinations.Select(d => d as IRgb24Destination)) {
				rgbDest?.ClearPalette();
			}
		}

		/// <summary>
		/// Resets the color
		/// </summary>
		public void ClearColor()
		{
			SetColor(DefaultColor);
			if (Destinations == null) {
				return;
			}
			foreach (var rgbDest in Destinations.Select(d => d as IRgb24Destination)) {
				rgbDest?.ClearColor();
			}
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
			if (Destinations == null) {
				return;
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
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return frame;
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
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return frame;
			}
			var bmp = ImageUtil.ConvertFromGray4(width, height, frame, 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray4(transformedBmp);
			return transformedFrame;
		}

		private Tuple<byte[][], Color[]> TransformColoredGray2(int width, int height, byte[][] planes, Color[] palette, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return new Tuple<byte[][], Color[]>(TransformationUtil.Flip(width, height, planes, FlipHorizontally, FlipVertically), palette);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return new Tuple<byte[][], Color[]>(planes, palette);
			}
			var bmp = ImageUtil.ConvertFromGray2(width, height, FrameUtil.Join(width, height, planes), 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray2(transformedBmp);
			return new Tuple<byte[][], Color[]>(FrameUtil.Split(width, height, 2, transformedFrame), palette);
		}

		private Tuple<byte[][], Color[]> TransformColoredGray4(int width, int height, byte[][] planes, Color[] palette, IFixedSizeDestination dest)
		{
			if (dest == null) {
				return new Tuple<byte[][], Color[]>(TransformationUtil.Flip(width, height, planes, FlipHorizontally, FlipVertically), palette);
			}
			if (width == dest.DmdWidth && height == dest.DmdHeight && !FlipHorizontally && !FlipVertically) {
				return new Tuple<byte[][], Color[]>(planes, palette);
			}
			var bmp = ImageUtil.ConvertFromGray4(width, height, FrameUtil.Join(width, height, planes), 0, 1, 1);
			var transformedBmp = TransformationUtil.Transform(bmp, dest.DmdWidth, dest.DmdHeight, Resize, FlipHorizontally, FlipVertically);
			var transformedFrame = ImageUtil.ConvertToGray4(transformedBmp);
			return new Tuple<byte[][], Color[]>(FrameUtil.Split(width, height, 4, transformedFrame), palette);
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
		ColoredGray4
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

	/// <summary>
	/// Thrown when trying to convert a source incompatible with the convertor.
	/// </summary>
	public class IncompatibleSourceException : Exception
	{
		public IncompatibleSourceException(string message) : base(message)
		{
		}
	}
}
