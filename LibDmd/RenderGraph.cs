using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibDmd.Common;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Input.FileSystem;
using LibDmd.Output;
using NLog;

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
	/// A render graph can also contain an <see cref="AbstractConverter"/>. These are
	/// classes that for a defined input format produce different output 
	/// formats. An example would be a 2-bit source that gets converted to
	/// RGB24, or, to colored 2- and 4-bit.
	/// </summary>
	public class RenderGraph : IRenderer
	{
		#region Configuration
		
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
		public List<IDestination> Destinations {
			get => _destinations;
			set {
				_destinations = value;
				_refs.Add(_destinations);
			} }
		private List<IDestination> _destinations;

		public void AddDestination(IDestination dest)
		{
			_destinations.Add(dest);
			_refs.Add(dest);
		}

		/// <summary>
		/// If set, convert the frame format.
		/// </summary>
		public AbstractConverter Converter { get; set; }

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
		public int IdleAfter { get; set; }

		/// <summary>
		/// When IdleAfter is enabled, play this (blank screen if null)
		/// </summary>
		public string IdlePlay { get; set; }

		public ScalerMode ScalerMode { get; set; } = ScalerMode.None;

		#endregion

		#region Constants

		/// <summary>
		/// The default color used if there is no palette defined
		/// </summary>
		public static readonly Color DefaultColor = Colors.OrangeRed;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

		#region Members

		private Color[] _gray2Colors; 
		private Color[] _gray4Colors;
		private Color[] _gray6Colors;
		private Color[] _gray8Colors;
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;
		private Color[] _gray6Palette;
		private Color[] _gray8Palette;

		private IDisposable _idleRenderer;
		private IDisposable _activeRenderer;
		private RenderGraph _idleRenderGraph;
		
		private readonly CompositeDisposable _activeSources = new CompositeDisposable();
		private readonly bool _runOnMainThread;
		private readonly UndisposedReferences _refs;

		#endregion

		#region Lifecycle

		public RenderGraph(UndisposedReferences refs, bool runOnMainThread = false)
		{
			_refs = refs;
			_runOnMainThread = runOnMainThread;
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
			source.FramesBitmap.OnNext(new BmpFrame(bmp));
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
		/// Sets the color with which a grayscale source is rendered on the RGB display.
		/// </summary>
		/// <param name="color">Rendered color</param>
		public void SetColor(Color color)
		{
			_gray2Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 4);
			_gray4Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 16);
			_gray6Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 64);
			_gray8Colors = ColorUtil.GetPalette(new []{Colors.Black, color}, 256);

			Logger.Info($"[RenderGraph] SetColor(0%: {_gray2Colors[0]}, 33%:{_gray2Colors[1]}, 66%:{_gray2Colors[2]}, 100%:{_gray2Colors[3]})");
		}

		/// <summary>
		/// Sets the palette for rendering grayscale images.
		/// </summary>
		/// <param name="colors">Palette to set</param>
		public void SetPalette(Color[] colors)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);
			_gray6Palette = ColorUtil.GetPalette(colors, 64);
			_gray8Palette = ColorUtil.GetPalette(colors, 256);
		}

		/// <summary>
		/// Removes a previously set palette
		/// </summary>
		public void ClearPalette()
		{
			_gray2Palette = null;
			_gray4Palette = null;
			_gray6Palette = null;
			_gray8Palette = null;
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
			Converter?.Dispose();

			if (Destinations != null) {
				foreach (var dest in Destinations) {
					_refs.Dispose(dest);
				}
			}
			foreach (var source in _activeSources) {
				source.Dispose();
			}
		}
		
		#endregion

		#region Graph Connections

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

			try {
				var sourceGray2 = Source as IGray2Source;
				var sourceGray4 = Source as IGray4Source;
				var sourceGray8 = Source as IGray8Source;
				var sourceAlphaNumeric = Source as IAlphaNumericSource;
				Logger.Info("Setting up {0} for {1} destination(s) [ {2} ]", Name, Destinations.Count, string.Join(", ", Destinations.Select(d => d.Name)));

				// subscribe converter to incoming frames
				if (Converter != null) {

					// subscribe converter to incoming frames
					foreach (var from in Converter.From) {
						switch (from) {
							case FrameFormat.Gray2:
								if (sourceGray2 != null) {
									Logger.Info($"  == Listening to {sourceGray2.Name} for {((ISource)Converter).Name} ({from})");
									_activeSources.Add(sourceGray2.GetGray2Frames(!Converter.NeedsDuplicateFrames, false).Do(Converter.Convert).Subscribe());
								}
								break;
							case FrameFormat.Gray4:
								if (sourceGray4 != null) {
									Logger.Info($"  == Listening to {sourceGray4.Name} for {((ISource)Converter).Name} ({from})");
									_activeSources.Add(sourceGray4.GetGray4Frames(!Converter.NeedsDuplicateFrames, false).Do(Converter.Convert).Subscribe());
								}
								break;
							case FrameFormat.AlphaNumeric:
								if (sourceAlphaNumeric != null) {
									Logger.Info($"  == Listening to {sourceAlphaNumeric.Name} for {((ISource)Converter).Name} ({from})");
									_activeSources.Add(sourceAlphaNumeric.GetAlphaNumericFrames().Do(Converter.Convert).Subscribe());
								}
								break;
							default:
								throw new IncompatibleGraphException($"Frame conversion from ${from} is not implemented.");
						}
					}
				}

				foreach (var dest in Destinations) {

					var destColoredGray2 = dest as IColoredGray2Destination;
					var destColoredGray4 = dest as IColoredGray4Destination;
					var destColoredGray6 = dest as IColoredGray6Destination;
					var destRgb565 = dest as IRgb565Destination;
					var destRgb24 = dest as IRgb24Destination;
					var destBitmap = dest as IBitmapDestination;
					var destAlphaNumeric = dest as IAlphaNumericDestination;

					// So here's how convertors work:
					// They have multiple input types, given by IConvertor.From, and they can
					// randomly output frames in different formats (both dimensions and bit depth).
					//
					// For the output, the converter acts as ISource, implementing the specific
					// interfaces supported. Currently the following output sources are supported:
					//
					//    IColoredGray2Source, IColoredGray4Source, IColoredGray6Source and IRgb24Source.
					//
					// Other types don't make much sense (i.e. you don't convert *down* to 
					// IGray2Source).
					//
					// In the block below, those sources are linked to each destination. If a 
					// destination doesn't support a colored gray source, it tries to convert
					// it up to RGB24, otherwise fails. For example, PinDMD3 which only supports
					// IColoredGray2Source but not IColoredGray4Source due to bad software design
					// will get the IColoredGray4Source converted up to RGB24.
					if (Converter != null) {

						Logger.Info($"  ** Linking converter {Converter.Name} to {dest.Name}...");

						// if converter emits colored gray-2 frames..
						if (Converter is IColoredGray2Source sourceConverterColoredGray2 && !Converter.IsConnected(dest, FrameFormat.ColoredGray2)) {
							// if destination can render colored gray-2 frames...
							if (destColoredGray2 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray2, FrameFormat.ColoredGray2)) {
								Connect(sourceConverterColoredGray2, destColoredGray2, FrameFormat.ColoredGray2, FrameFormat.ColoredGray2);

							// try to convert to rgb
							} else if (destRgb565 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray2, FrameFormat.Rgb565)) {
								Logger.Warn("    -- Destination doesn't support colored 2-bit frames from converter, converting to RGB565 source.");
								Connect(sourceConverterColoredGray2, destRgb565, FrameFormat.ColoredGray2, FrameFormat.Rgb565);

							} else if (destRgb24 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray2, FrameFormat.Rgb24)) {
								Logger.Warn("    -- Destination doesn't support colored 2-bit frames from converter, converting to RGB source.");
								Connect(sourceConverterColoredGray2, destRgb24, FrameFormat.ColoredGray2, FrameFormat.Rgb24);

							} else if (destBitmap != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray2, FrameFormat.Bitmap)) {
								Logger.Warn("    -- Destination doesn't support colored 2-bit frames from converter, converting to RGB source.");
								Connect(sourceConverterColoredGray2, destBitmap, FrameFormat.ColoredGray2, FrameFormat.Bitmap);

							} else {
								Logger.Warn("    -- Destination doesn't support colored 2-bit frames or RGB from converter, ignoring converter.");
							}
						}

						// if converter emits colored gray-4 frames..
						if (Converter is IColoredGray4Source sourceConverterColoredGray4 && !Converter.IsConnected(dest, FrameFormat.ColoredGray4)) {
							// if destination can render colored gray-4 frames...
							if (destColoredGray4 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray4,FrameFormat.ColoredGray4)) {
								Connect(sourceConverterColoredGray4, destColoredGray4, FrameFormat.ColoredGray4, FrameFormat.ColoredGray4);

							// try to convert to rgb
							} else if (destRgb565 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray4, FrameFormat.Rgb565)) {
								Logger.Warn("    -- Destination doesn't support colored 4-bit frames from converter, converting to RGB565 source.");
								Connect(sourceConverterColoredGray4, destRgb565, FrameFormat.ColoredGray4, FrameFormat.Rgb565);

							} else if (destRgb24 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray4,FrameFormat.Rgb24)) {
								Logger.Warn("    -- Destination doesn't support colored 4-bit frames from converter, converting to RGB source.");
								Connect(sourceConverterColoredGray4, destRgb24, FrameFormat.ColoredGray4, FrameFormat.Rgb24);

							} else if (destBitmap != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray4,FrameFormat.Bitmap)) {
								Logger.Warn("    -- Destination doesn't support colored 4-bit frames from converter, converting to Bitmap source.");
								Connect(sourceConverterColoredGray4, destBitmap, FrameFormat.ColoredGray4, FrameFormat.Bitmap);
							} else {
								Logger.Warn("    -- Destination doesn't support colored 4-bit frames from converter, ignoring converter.");
							}
						}

						// if converter emits rgb565 frames..
						if (Converter is IRgb565Source sourceConverterRgb565 && !Converter.IsConnected(dest, FrameFormat.Rgb565)) {
							// if destination can render rgb565 frames...
							if (destRgb565 != null && !Converter.IsConnected(dest, FrameFormat.Rgb565,FrameFormat.Rgb565)) {
								Connect(sourceConverterRgb565, destRgb565, FrameFormat.Rgb565, FrameFormat.Rgb565);

							// otherwise, convert to rgb24
							} else if (destRgb24 != null && !Converter.IsConnected(dest, FrameFormat.Rgb565,FrameFormat.Rgb24)) {
								Logger.Warn("    -- Destination doesn't support RGB565 frames from converter, converting to RGB24 source.");
								Connect(sourceConverterRgb565, destRgb24, FrameFormat.Rgb565, FrameFormat.Rgb24);

							} else if (destBitmap != null && !Converter.IsConnected(dest, FrameFormat.Rgb565,FrameFormat.Bitmap)) {
								Logger.Warn("    -- Destination doesn't support RGB565 frames from converter, converting to Bitmap source.");
								Connect(sourceConverterRgb565, destBitmap, FrameFormat.Rgb565, FrameFormat.Bitmap);
							} else {
								Logger.Warn("    -- Destination doesn't support RGB565 frames from converter, ignoring converter.");
							}
						}

						// if converter emits colored gray-6 frames..
						if (Converter is IColoredGray6Source sourceConverterColoredGray6 && !Converter.IsConnected(dest, FrameFormat.ColoredGray6)) {
							// if destination can render colored gray-6 frames...
							if (destColoredGray6 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray6, FrameFormat.ColoredGray6)) {
								Connect(sourceConverterColoredGray6, destColoredGray6, FrameFormat.ColoredGray6, FrameFormat.ColoredGray6);

							// otherwise, convert to rgb24
							} else if (destRgb24 != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray6, FrameFormat.Rgb24)) {
								Logger.Warn("    -- Destination doesn't support colored 6-bit frames from converter, converting to RGB source.");
								Connect(sourceConverterColoredGray6, destRgb24, FrameFormat.ColoredGray6, FrameFormat.Rgb24);

							} else if (destBitmap != null && !Converter.IsConnected(dest, FrameFormat.ColoredGray6, FrameFormat.Bitmap)) {
								Logger.Warn("    -- Destination doesn't support colored 6-bit frames from converter, converting to Bitmap source.");
								Connect(sourceConverterColoredGray6, destBitmap, FrameFormat.ColoredGray6, FrameFormat.Bitmap);
							} else {
								Logger.Warn("    -- Destination doesn't support colored 6-bit frames from converter, ignoring converter.");
							}
						}

						// if converter emits RGB24 frames..
						if (Converter is IRgb24Source sourceConverterRgb24 && destRgb24 != null && !Converter.IsConnected(dest, FrameFormat.Rgb24, FrameFormat.Rgb24)) {
							Connect(sourceConverterRgb24, destRgb24, FrameFormat.Rgb24, FrameFormat.Rgb24);
						}

						// this is mainly for the passing through alphanumeric frames from the switching converter.
						if (Converter is IAlphaNumericSource sourceConverterAlphaNumeric && destAlphaNumeric != null && !Converter.IsConnected(dest, FrameFormat.AlphaNumeric, FrameFormat.AlphaNumeric)) {
							Connect(sourceConverterAlphaNumeric, destAlphaNumeric, FrameFormat.AlphaNumeric, FrameFormat.AlphaNumeric);
						}

						// if converter emits color rotations
						if (Converter is IColorRotationSource sourceColorRotation && dest is IColorRotationDestination destColorRotation && !Converter.IsConnected(destColorRotation)) {
							Logger.Info("    ~> Subscribing destination {0} to color rotation palette changes from {1}.", destColorRotation.Name, sourceColorRotation.Name);
							Subscribe(
								sourceColorRotation.GetPaletteChanges(),
								palette => palette,
								destColorRotation.UpdatePalette
							);
							Converter.SetConnected(destColorRotation);
						}

						// if converter frame events
						if (Converter is IFrameEventSource sourceFrameEvent && dest is IFrameEventDestination destFrameEvent && !Converter.IsConnected(destFrameEvent)) {
							Logger.Info("    ~> Subscribing destination {0} to frame events from {1}.", destFrameEvent.Name, sourceFrameEvent.Name);
							Subscribe(sourceFrameEvent.GetFrameEventInit(), e => e, destFrameEvent.OnFrameEventInit);
							Subscribe(sourceFrameEvent.GetFrameEvents(), e => e, destFrameEvent.OnFrameEvent);
							Converter.SetConnected(destFrameEvent);
						}

						// if the above yielded to a connection to the converter, we skip the rest below
						if (Converter.IsConnected(dest)) {
							continue;
						}
					}

					// Now here we need to find the most efficient way of passing data from the source
					// to each destination. 
					// One thing to remember is that now we don't have a converter defining the
					// input format, so the source might be able to deliver multiple different formats
					// and the destination might be accepting multiple formats as well. 
					//
					// But since we know that a source doesn't implement any interface that would 
					// result in data loss (e.g. a 4-bit source will not implement IGray2Source), we
					// start looking at the most performant combinations first.
					//
					// So first we try to match the source format with the destination format. Then
					// we go on by looking at "up-scaling" conversions, e.g. if a destination only
					// supports RGB24, then convert 2-bit to RGB24. Lastly we check "downscaling"
					// conversions, e.g. convert an RGB24 frame to 2-bit for outputs like PinDMD1
					// that can only render 4 shades.

					var destGray2 = dest as IGray2Destination;
					var destGray4 = dest as IGray4Destination;
					var destGray8 = dest as IGray8Destination;

					var sourceColoredGray2 = Source as IColoredGray2Source;
					var sourceColoredGray4 = Source as IColoredGray4Source;
					var sourceColoredGray6 = Source as IColoredGray6Source;
					var sourceRgb565 = Source as IRgb565Source;
					var sourceRgb24 = Source as IRgb24Source;
					var sourceBitmap = Source as IBitmapSource;

					// first, check if we do without conversion. the order is important here!
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
					// gray8 -> gray8
					if (sourceGray8 != null && destGray8 != null) {
						Connect(Source, dest, FrameFormat.Gray8, FrameFormat.Gray8);
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
					if (sourceColoredGray6 != null && destColoredGray6 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.ColoredGray6);
						continue;
					}
					// rgb565 -> rgb565
					if (sourceRgb565 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.Rgb565, FrameFormat.Rgb565);
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
					// gray2 -> rgb565
					if (sourceGray2 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.Gray2, FrameFormat.Rgb565);
						continue;
					}
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
					// gray4 -> rgb565
					if (sourceGray4 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Rgb565);
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
					// gray8 -> rgb565
					if (sourceGray8 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.Gray8, FrameFormat.Rgb565);
						continue;
					}
					// gray8 -> rgb24
					if (sourceGray8 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.Gray8, FrameFormat.Rgb24);
						continue;
					}
					// gray8 -> bitmap
					if (sourceGray8 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.Gray8, FrameFormat.Bitmap);
						continue;
					}
					// rgb565 -> rgb24
					if (sourceRgb565 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.Rgb565, FrameFormat.Rgb24);
						continue;
					}
					// rgb565 -> bitmap
					if (sourceRgb565 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.Rgb565, FrameFormat.Bitmap);
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
					if (sourceColoredGray6 != null && destRgb24 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.Rgb24);
						continue;
					}
					// colored gray6 -> bitmap
					if (sourceColoredGray6 != null && destBitmap != null) {
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.Bitmap);
						continue;
					}

					// finally, here we lose data
					// gray4 -> gray2
					if (sourceGray4 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Gray4, FrameFormat.Gray2);
						continue;
					}
					// gray8 -> colored gray6
					if (sourceGray8 != null && destColoredGray6 != null) {
						Connect(Source, dest, FrameFormat.Gray8, FrameFormat.ColoredGray6);
						continue;
					}
					// gray8 -> gray4
					if (sourceGray8 != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.Gray8, FrameFormat.Gray4);
						continue;
					}
					// gray8 -> gray2
					if (sourceGray8 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Gray8, FrameFormat.Gray2);
						continue;
					}
					// colored gray6 -> rgb565
					if (sourceColoredGray6 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.Rgb565);
						continue;
					}
					// colored gray6 -> gray4
					if (sourceColoredGray6 != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.Gray4);
						continue;
					}
					// colored gray6 -> gray2
					if (sourceColoredGray6 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray6, FrameFormat.Gray2);
						continue;
					}
					// colored gray4 -> gray4
					if (sourceColoredGray4 != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray4, FrameFormat.Gray4);
						continue;
					}
					// rgb565 -> gray4
					if (sourceRgb565 != null && destGray4 != null) {
						Connect(Source, dest, FrameFormat.Rgb565, FrameFormat.Gray4);
						continue;
					}
					// rgb565 -> gray2
					if (sourceRgb565 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.Rgb565, FrameFormat.Gray2);
						continue;
					}
					// rgb24 -> rgb565
					if (sourceRgb24 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.Rgb24, FrameFormat.Rgb565);
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
					// colored gray2 -> rgb565
					if (sourceColoredGray2 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray2, FrameFormat.Rgb565);
						continue;
					}
					// colored gray2 -> gray2
					if (sourceColoredGray2 != null && destGray2 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray2, FrameFormat.Gray2);
						continue;
					}
					// colored gray4 -> rgb565
					if (sourceColoredGray4 != null && destRgb565 != null) {
						Connect(Source, dest, FrameFormat.ColoredGray4, FrameFormat.Rgb565);
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
				if (onError != null) {
					onError.Invoke(ex);
				} else {
					throw;
				}
			}
			_activeRenderer = new RenderDisposable(_refs, _activeSources);
			return _activeRenderer;
		}
		
		#endregion

		#region Pipeline Setup

		/// <summary>
		/// Connects a source with a destination and defines in which mode data is
		/// sent and received.
		/// </summary>
		/// <remarks>
		/// Note that render bit length is enforced, i.e. even if the destination 
		/// supports the "from" bit length, it will be converted to the given "to"
		/// bit length.
		/// </remarks>
		/// <param name="source">Source to subscribe to</param>
		/// <param name="dest">Destination to send the data to</param>
		/// <param name="from">Data format to read from source (incompatible source will throw exception)</param>
		/// <param name="to">Data format to send to destination (incompatible destination will throw exception)</param>
		//[SuppressMessage("ReSharper", "PossibleNullReferenceException")]
		private void Connect(ISource source, IDestination dest, FrameFormat from, FrameFormat to)
		{
			var destFixedSize = dest as IFixedSizeDestination;
			var destMultiSize = dest as IMultiSizeDestination;
			var destGray2 = dest as IGray2Destination;
			var destGray4 = dest as IGray4Destination;
			var destGray8 = dest as IGray8Destination;
			var destRgb565 = dest as IRgb565Destination;
			var destRgb24 = dest as IRgb24Destination;
			var destBitmap = dest as IBitmapDestination;
			var destColoredGray2 = dest as IColoredGray2Destination;
			var destColoredGray4 = dest as IColoredGray4Destination;
			var destColoredGray6 = dest as IColoredGray6Destination;
			var destAlphaNumeric = dest as IAlphaNumericDestination;

			var indent = "";
			if (source is AbstractConverter converter) {
				converter.SetConnected(dest, from, to);
				indent = "  ";
			}

			try {
				var deduped = string.Empty;
				if (from == FrameFormat.Gray2 || from == FrameFormat.Gray4) {
					deduped = dest.NeedsDuplicateFrames ? " - not deduped" : " - deduped";
				}
				Dispatcher.CurrentDispatcher.Invoke(() => Logger.Info($"  {indent}-> Connecting {source.Name} to {dest.Name} ({@from} -> {to}){deduped}"));
			
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
							Subscribe(
								sourceGray2.GetGray2Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2
							);
							break;

						// gray2 -> gray4
						case FrameFormat.Gray4:
							throw new IncompatibleGraphException("Cannot convert from gray2 to gray4 (every gray4 destination should be able to do gray2 as well).");

						// gray2 -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceGray2, dest, destRgb565, from, to);
							Subscribe(
								sourceGray2.GetGray2Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToRgb565(_gray2Palette ?? _gray2Colors)
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565
							);
							break;

						// gray2 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray2, dest, destRgb24, from, to);
							Subscribe(
								sourceGray2.GetGray2Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToRgb24(_gray2Palette ?? _gray2Colors)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24
							);
							break;

						// gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray2, dest, destBitmap, from, to);
							Subscribe(
								sourceGray2.GetGray2Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToBmp(_gray2Palette ?? _gray2Colors)
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap
							);
							break;

						// gray2 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new IncompatibleGraphException("Cannot convert from gray2 to colored gray2 (doesn't make any sense, colored gray2 can also do gray2).");

						// gray2 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new IncompatibleGraphException("Cannot convert from gray2 to colored gray2 (a colored gray4 destination should also be able to do gray2 directly).");

						// gray2 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new IncompatibleGraphException("Cannot convert from gray2 to colored gray6 (a colored gray6 destination should also be able to do gray2 directly).");

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
							Subscribe(
								sourceGray4.GetGray4Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray2()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// gray4 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceGray4, dest, destGray4, from, to);
							Subscribe(
								sourceGray4.GetGray4Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// gray4 -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceGray4, dest, destRgb565, from, to);
							Subscribe(
								sourceGray4.GetGray4Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToRgb565(_gray4Palette ?? _gray4Colors)
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565
							);
							break;

						// gray4 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray4, dest, destRgb24, from, to);
							Subscribe(
								sourceGray4.GetGray4Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToRgb24(_gray4Palette ?? _gray4Colors)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray4, dest, destBitmap, from, to);
							Subscribe(
								sourceGray4.GetGray4Frames(!dest.NeedsDuplicateFrames, !dest.NeedsIdentificationFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToBmp(_gray4Palette ?? _gray4Colors)
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;

						// gray4 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new IncompatibleGraphException("Cannot convert from gray4 to colored gray2 (doesn't make any sense, colored gray2 can also do gray4).");

						// gray4 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new IncompatibleGraphException("Cannot convert from gray4 to colored gray4 (doesn't make any sense, colored gray4 can also do gray2).");

						// gray4 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new IncompatibleGraphException("Cannot convert from gray4 to colored gray6 (doesn't make any sense, colored gray6 can also do gray4).");

						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;


				// source is gray8:
				case FrameFormat.Gray8:
					var sourceGray8 = source as IGray8Source;
					switch (to) {

						// gray8 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceGray8, dest, destGray4, from, to);
							Subscribe(
								sourceGray8.GetGray8Frames(!dest.NeedsDuplicateFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray4()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// gray8 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceGray8, dest, destGray2, from, to);
							Subscribe(
								sourceGray8.GetGray8Frames(!dest.NeedsDuplicateFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray2()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// gray8 -> gray8
						case FrameFormat.Gray8:
							AssertCompatibility(source, sourceGray8, dest, destGray8, from, to);
							Subscribe(
								sourceGray8.GetGray8Frames(!dest.NeedsDuplicateFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray8.RenderGray8);
							break;

						// gray8 -> colored gray6
						case FrameFormat.ColoredGray6:
							AssertCompatibility(source, sourceGray8, dest, destColoredGray6, from, to);
							Subscribe(
								sourceGray8.GetGray8Frames(!dest.NeedsDuplicateFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGray8ToColoredGray6(_gray6Palette ?? _gray6Colors)
									.Transform(this, destFixedSize, destMultiSize),
								destColoredGray6.RenderColoredGray6
							);
							break;

						// gray8 -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceGray8, dest, destRgb565, from, to);
							Subscribe(
								sourceGray8.GetGray8Frames(!dest.NeedsDuplicateFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToRgb565(_gray8Palette ?? _gray8Colors)
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565
							);
							break;

						// gray8 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray8, dest, destRgb24, from, to);
							Subscribe(
								sourceGray8.GetGray8Frames(!dest.NeedsDuplicateFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToRgb24(_gray8Palette ?? _gray8Colors)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// gray8 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray8, dest, destBitmap, from, to);
							Subscribe(
								sourceGray8.GetGray8Frames(!dest.NeedsDuplicateFrames),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertGrayToBmp(_gray8Palette ?? _gray8Colors)
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;


						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;

				// source is rgb565:
				case FrameFormat.Rgb565:
					var sourceRgb565 = source as IRgb565Source;
					switch (to) {
						// rgb565 -> gray2
						case FrameFormat.Gray2:
							AssertCompatibility(source, sourceRgb565, dest, destGray2, from, to);
							Subscribe(
								sourceRgb565.GetRgb565Frames(),
								frame => frame
									.ConvertToGray2()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// rgb565 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceRgb565, dest, destGray4, from, to);
							Subscribe(
								sourceRgb565.GetRgb565Frames(),
								frame => frame
									.ConvertToGray4()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// rgb565 -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceRgb565, dest, destRgb565, from, to);
							Subscribe(
								sourceRgb565.GetRgb565Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565
							);
							break;

						// rgb565 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceRgb565, dest, destRgb24, from, to);
							Subscribe(
								sourceRgb565.GetRgb565Frames(),
								frame => frame
									.ConvertRgb565ToRgb24()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// rgb565 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceRgb565, dest, destBitmap, from, to);
							Subscribe(
								sourceRgb565.GetRgb565Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertRgbToBmp()
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;

						// rgb565 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new IncompatibleGraphException("Cannot convert from rgb565 to colored gray2 (colored gray2 only has 4 colors per frame).");

						// rgb565 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new IncompatibleGraphException("Cannot convert from rgb565 to colored gray2 (colored gray4 only has 16 colors per frame).");

						// rgb565 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new IncompatibleGraphException("Cannot convert from rgb565 to colored gray6 (colored gray6 only has 64 colors per frame).");

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
							Subscribe(
								sourceRgb24.GetRgb24Frames(),
								frame => frame
									.ConvertToGray2()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// rgb24 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceRgb24, dest, destGray4, from, to);
							Subscribe(
								sourceRgb24.GetRgb24Frames(),
								frame => frame
									.ConvertToGray4()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// rgb24 -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceRgb24, dest, destRgb565, from, to);
							Subscribe(
								sourceRgb24.GetRgb24Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertRgb24ToRgb565()
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565
							);
							break;

						// rgb24 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceRgb24, dest, destRgb24, from, to);
							Subscribe(
								sourceRgb24.GetRgb24Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// rgb24 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceRgb24, dest, destBitmap, from, to);
							Subscribe(
								sourceRgb24.GetRgb24Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertRgbToBmp()
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;

						// rgb24 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new IncompatibleGraphException("Cannot convert from rgb24 to colored gray2 (colored gray2 only has 4 colors per frame).");

						// rgb24 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new IncompatibleGraphException("Cannot convert from rgb24 to colored gray2 (colored gray4 only has 16 colors per frame).");

						// rgb24 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new IncompatibleGraphException("Cannot convert from rgb24 to colored gray6 (colored gray6 only has 64 colors per frame).");

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
							Subscribe(
								sourceBitmap.GetBitmapFrames(),
								frame => frame
									.ConvertToGray2()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// bitmap -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceBitmap, dest, destGray4, from, to);
							Subscribe(
								sourceBitmap.GetBitmapFrames(),
								frame => frame
									.ConvertToGray4()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;


						// bitmap -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceBitmap, dest, destRgb565, from, to);
							Subscribe(
								sourceBitmap.GetBitmapFrames(),
								frame => frame
									.ConvertToRgb565()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565);
							break;

						// bitmap -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceBitmap, dest, destRgb24, from, to);
							Subscribe(
								sourceBitmap.GetBitmapFrames(),
								frame => frame
									.ConvertToRgb24()
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// bitmap -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(Source, sourceBitmap, dest, destBitmap, from, to);
							Subscribe(
								sourceBitmap.GetBitmapFrames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;

						// bitmap -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new IncompatibleGraphException("Cannot convert from bitmap to colored gray2 (colored gray2 only has 4 colors per frame).");

						// bitmap -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new IncompatibleGraphException("Cannot convert from bitmap to colored gray2 (colored gray4 only has 16 colors per frame).");

						// bitmap -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new IncompatibleGraphException("Cannot convert from bitmap to colored gray6 (colored gray6 only has 64 colors per frame).");

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
							Subscribe(
								sourceColoredGray2.GetColoredGray2Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// colored gray2 -> gray4
						case FrameFormat.Gray4:
							throw new IncompatibleGraphException("Cannot convert from colored gray2 to gray4 (it's not like we can extract luminosity from the colors...)");

						// colored gray2 -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceColoredGray2, dest, destRgb565, from, to);
							Subscribe(
								sourceColoredGray2.GetColoredGray2Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToRgb565()
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565);
							break;

						// colored gray2 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray2, dest, destRgb24, from, to);
							Subscribe(
								sourceColoredGray2.GetColoredGray2Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToRgb24()
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// colored gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray2, dest, destBitmap, from, to);
							Subscribe(
								sourceColoredGray2.GetColoredGray2Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToBitmap()
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;

						// colored gray2 -> colored gray2
						case FrameFormat.ColoredGray2:
							AssertCompatibility(source, sourceColoredGray2, dest, destColoredGray2, from, to);
							Subscribe(
								sourceColoredGray2.GetColoredGray2Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.Transform(this, destFixedSize, destMultiSize),
								destColoredGray2.RenderColoredGray2);
							break;

						// colored gray2 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new IncompatibleGraphException("Cannot convert from colored gray2 to colored gray4 (if a destination can do colored gray4 it should be able to do colored gray2 directly).");

						case FrameFormat.ColoredGray6:
							throw new IncompatibleGraphException("Cannot convert from colored gray2 to colored gray6 (if a destination can do colored gray6 it should be able to do colored gray2 directly).");

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
							Subscribe(
								sourceColoredGray4.GetColoredGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray()
									.ConvertToGray2()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// colored gray4 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceColoredGray4, dest, destGray4, from, to);
							Subscribe(
								sourceColoredGray4.GetColoredGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// colored gray4 -> rgb565
						case FrameFormat.Rgb565:
							AssertCompatibility(source, sourceColoredGray4, dest, destRgb565, from, to);
							Subscribe(
								sourceColoredGray4.GetColoredGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToRgb565()
									.TransformRgb565(this, destFixedSize, destMultiSize),
								destRgb565.RenderRgb565);
							break;

						// colored gray4 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray4, dest, destRgb24, from, to);
							Subscribe(
								sourceColoredGray4.GetColoredGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToRgb24()
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// colored gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray4, dest, destBitmap, from, to);
							Subscribe(
								sourceColoredGray4.GetColoredGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToBitmap()
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;

						// colored gray4 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new IncompatibleGraphException("Cannot convert from colored gray4 to colored gray2 (use rgb24 instead of down-coloring).");

						// colored gray4 -> colored gray4
						case FrameFormat.ColoredGray4:
							AssertCompatibility(source, sourceColoredGray4, dest, destColoredGray4, from, to);
							Subscribe(
								sourceColoredGray4.GetColoredGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.Transform(this, destFixedSize, destMultiSize),
								destColoredGray4.RenderColoredGray4);
							break;
						
						// colored gray4 -> colored gray6
						case FrameFormat.ColoredGray6:
							throw new IncompatibleGraphException("Cannot convert from colored gray4 to colored gray6 (use rgb24 instead of down-coloring).");

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
							Subscribe(
								sourceColoredGray6.GetColoredGray6Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray()
									.ConvertToGray2()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2);
							break;

						// colored gray6 -> gray4
						case FrameFormat.Gray4:
							AssertCompatibility(source, sourceColoredGray6, dest, destGray4, from, to);
							Subscribe(
								sourceColoredGray6.GetColoredGray6Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToGray()
									.ConvertToGray4()
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// colored gray6 -> rgb24
						case FrameFormat.Rgb24: {
							AssertCompatibility(source, sourceColoredGray6, dest, destRgb24, from, to);
							if (Converter is IColorRotationSource colorRotationSource) {
								var rotationWrapper = new ColorRotationWrapper(sourceColoredGray6, colorRotationSource);
								Subscribe(
									rotationWrapper.GetRgb24Frames(),
									frame => frame
										.TransformHdScaling(destFixedSize, ScalerMode)
										.TransformRgb24(this, destFixedSize, destMultiSize),
									destRgb24.RenderRgb24);
								_activeSources.Add(rotationWrapper);
							} else {
								Subscribe(
									sourceColoredGray6.GetColoredGray6Frames(),
									frame => frame
										.TransformHdScaling(destFixedSize, ScalerMode)
										.ConvertToRgb24()
										.TransformRgb24(this, destFixedSize, destMultiSize),
									destRgb24.RenderRgb24);
							}
							break;
						}

						// colored gray6 -> bitmap
						case FrameFormat.Bitmap: {
							AssertCompatibility(source, sourceColoredGray6, dest, destBitmap, from, to);
							if (Converter is IColorRotationSource colorRotationSource) {
								var rotationWrapper = new ColorRotationWrapper(sourceColoredGray6, colorRotationSource);
								Subscribe(
									rotationWrapper.GetRgb24Frames(),
									frame => frame
										.TransformHdScaling(destFixedSize, ScalerMode)
										.ConvertRgbToBmp()
										.Transform(this, destFixedSize, destMultiSize),
									destBitmap.RenderBitmap);
							} else {
								Subscribe(
									sourceColoredGray6.GetColoredGray6Frames(),
									frame => frame
										.TransformHdScaling(destFixedSize, ScalerMode)
										.ConvertToBitmap()
										.Transform(this, destFixedSize, destMultiSize),
									destBitmap.RenderBitmap);
							}
							break;
						}

						// colored gray6 -> colored gray2
						case FrameFormat.ColoredGray2:
							throw new IncompatibleGraphException("Cannot convert from colored gray4 to colored gray2 (use rgb24 instead of down-coloring).");

						// colored gray6 -> colored gray4
						case FrameFormat.ColoredGray4:
							throw new IncompatibleGraphException("Cannot convert from colored gray6 to colored gray4 (use rgb24 instead of down-coloring).");

						// colored gray6 -> colored gray6
						case FrameFormat.ColoredGray6:
							AssertCompatibility(source, sourceColoredGray6, dest, destColoredGray6, from, to);
							Subscribe(
								sourceColoredGray6.GetColoredGray6Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.Transform(this, destFixedSize, destMultiSize),
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
							Subscribe(
								sourceAlphaNumeric.GetAlphaNumericFrames(),
								f=> f,
								destAlphaNumeric.RenderAlphaNumeric);
							break;
						
						default:
							throw new ArgumentOutOfRangeException(nameof(to), to, null);
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
		
		#endregion

		#region Utils

		/// <summary>
		/// Subscribes to the given source and links it to the given destination.
		/// </summary>
		///
		/// <remarks>
		/// This also does all the common stuff, i.e. setting the correct scheduler,
		/// enabling idle detection, etc.
		/// </remarks>
		///
		/// <typeparam name="TIn">Source frame type</typeparam>
		/// <typeparam name="TOut">Destination frame type</typeparam>
		/// <param name="src">Source observable</param>
		/// <param name="processor">Converts and transforms the frame to match to the destination format</param>
		/// <param name="onNext">Action to run on destination</param>
		private void Subscribe<TIn, TOut>(IObservable<TIn> src, Func<TIn, TOut> processor, Action<TOut> onNext) where TIn : class, ICloneable
		{
			// set idle timeout if enabled
			if (IdleAfter > 0) {

				// So we want the sequence to continue after the timeout, which is why
				// IObservable.Timeout is not well suited.
				// A better approach is to run onNext with IObservable.Do and subscribe
				// to the timeout through IObservable.Throttle.
				Logger.Info("Setting idle timeout to {0}ms.", IdleAfter);

				// now render it
				src = src.Do(_ => StopIdling());
				var dest = src.Select(frame => (TIn)frame.Clone()).Select(processor).Do(onNext);

				// but subscribe to a throttled idle action
				dest = dest.Throttle(TimeSpan.FromMilliseconds(IdleAfter));

				// execute on main thread
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
				if (!_runOnMainThread) {
					dest = dest.ObserveOn(Scheduler.Default);
				}
				_activeSources.Add(dest.Subscribe(f => StartIdling()));

			} else {

				// subscribe and add to active sources
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

				// run frame processing on separate thread.
				if (!_runOnMainThread) {
					src = src.ObserveOn(Scheduler.Default);
				}

				_activeSources.Add(src.Select(frame => (TIn)frame.Clone()).Select(processor).Subscribe(onNext));
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
		private void StartIdling()
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
				_idleRenderGraph = new RenderGraph(new UndisposedReferences()) {
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
		private void StopIdling()
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
		
		#endregion
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
		/// An 8-bit grayscale frame (256 shades) - result from PWM dithering
		/// </summary>
		Gray8,

		/// <summary>
		/// A 16-bit RGB frame
		/// </summary>
		Rgb565,

		/// <summary>
		/// A 24-bit RGB frame
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
	/// be re-subscribed if necessary.
	/// </remarks>
	internal class RenderDisposable : IDisposable
	{
		private readonly UndisposedReferences _refs;
		private readonly CompositeDisposable _activeSources;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public RenderDisposable(UndisposedReferences refs, CompositeDisposable activeSources)
		{
			_refs = refs;
			_activeSources = activeSources;
			_refs.Add(activeSources);
		}

		public void Dispose()
		{
			var numDisposed = 0;
			foreach (var source in _activeSources) {
				numDisposed += _refs.Dispose(source) ? 1 : 0;
			}

			if (numDisposed > 0) {
				Logger.Info("Source for {0} renderer(s) stopped.", numDisposed);
			}

			_activeSources.Clear();
		}
	}

	/// <summary>
	/// Keeps references of undisposed objects, so we don't dispose them multiple times.
	/// </summary>
	public class UndisposedReferences
	{
		private readonly HashSet<IDisposable> _refs = new HashSet<IDisposable>();

		public void Add(List<IDestination> destinations)
		{
			lock (_refs) {
				foreach (var dest in destinations) {
					_refs.Add(dest);
				}
			}
		}

		public void Add(IDestination dest)
		{
			lock (_refs) {
				_refs.Add(dest);
			}
		}

		public void Add(CompositeDisposable activeSources)
		{
			lock (_refs) {
				foreach (var src in activeSources) {
					_refs.Add(src);
				}
			}
		}

		public bool Dispose(IDisposable disposable)
		{
			lock (_refs) {
				if (!_refs.Contains(disposable)) {
					return false;
				}
				disposable.Dispose();
				_refs.Remove(disposable);
				return true;
			}
		}
	}

	#region Exceptions

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
	/// Thrown when trying to connect a source to a destination that are incompatible.
	/// </summary>
	public class IncompatibleGraphException : Exception
	{
		public IncompatibleGraphException(string message) : base(message)
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
	
	#endregion
}
