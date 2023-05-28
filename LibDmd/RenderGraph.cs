using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
	/// A render graph can also contain an <see cref="IConverter"/>. These are
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
		public List<IDestination> Destinations { get; set; }

		/// <summary>
		/// If set, convert the frame format.
		/// </summary>
		public IConverter Converter { get; set; }

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
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		private IDisposable _idleRenderer;
		private IDisposable _activeRenderer;
		private RenderGraph _idleRenderGraph;
		
		private readonly CompositeDisposable _activeSources = new CompositeDisposable();
		
		#endregion

		#region Lifecycle

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
		}

		/// <summary>
		/// Sets the palette for rendering grayscale images.
		/// </summary>
		/// <param name="colors">Palette to set</param>
		/// <param name="index">Palette index</param>
		public void SetPalette(Color[] colors, int index = -1)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);
		}

		/// <summary>
		/// Removes a previously set palette
		/// </summary>
		public void ClearPalette()
		{
			_gray2Palette = null;
			_gray4Palette = null;
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
				Logger.Info("Setting up {0} for {1} destination(s)", Name, Destinations.Count);

				// init converters
				IColoredGray2Source sourceConverterColoredGray2 = null;
				IColoredGray4Source sourceConverterColoredGray4 = null;
				IColoredGray6Source sourceConverterColoredGray6 = null;
				IRgb24Source sourceConverterRgb24 = null;

				// subscribe converter to incoming frames
				if (Converter != null) {
					sourceConverterColoredGray2 = Converter as IColoredGray2Source;
					sourceConverterColoredGray4 = Converter as IColoredGray4Source;
					sourceConverterColoredGray6 = Converter as IColoredGray6Source;
					sourceConverterRgb24 = Converter as IRgb24Source;
					
					// subscribe converter to incoming frames
					switch (Converter.From) {
						case FrameFormat.Gray2:
							if (sourceGray2 == null) {
								throw new IncompatibleSourceException($"Source {Source.Name} is not 2-bit compatible which is mandatory for converter {sourceConverterColoredGray2?.Name}.");
							}
							_activeSources.Add(sourceGray2.GetGray2Frames().Do(Converter.Convert).Subscribe());
							break;
						case FrameFormat.Gray4:
							if (sourceGray4 == null) {
								throw new IncompatibleSourceException($"Source {Source.Name} is not 4-bit compatible which is mandatory for converter {sourceConverterColoredGray4?.Name}.");
							}
							_activeSources.Add(sourceGray4.GetGray4Frames().Do(Converter.Convert).Subscribe());
							break;
						default:
							throw new IncompatibleGraphException($"Frame conversion from ${Converter.From} is not implemented.");
					}
				}

				foreach (var dest in Destinations) {

					var destColoredGray2 = dest as IColoredGray2Destination;
					var destColoredGray4 = dest as IColoredGray4Destination;
					var destColoredGray6 = dest as IColoredGray6Destination;
					var destRgb24 = dest as IRgb24Destination;
					var destBitmap = dest as IBitmapDestination;

					// So here's how convertors work:
					// They have one input type, given by IConvertor.From, but they can randomly 
					// output frames in different formats (both dimensions and bit depth).
					// For example, the ColoredGray2Colorizer outputs in ColoredGray2 or
					// ColoredGray4, depending if data is enhanced or not.
					//
					// So, for the output, the converter acts as ISource, implementing the specific 
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

						var converterConnected = false;
						
						// if converter emits colored gray-2 frames..
						if (sourceConverterColoredGray2 != null) {
							// if destination can render colored gray-2 frames...
							if (destColoredGray2 != null) {
								Logger.Info("Hooking colored 2-bit source of {0} converter to {1}.", sourceConverterColoredGray2.Name, dest.Name);
								Connect(sourceConverterColoredGray2, destColoredGray2, FrameFormat.ColoredGray2, FrameFormat.ColoredGray2);
								converterConnected = true;

							// try to convert to rgb24
							} else if (destRgb24 != null) {
								Logger.Warn("Destination {0} doesn't support colored 2-bit frames from {1} converter, converting to RGB source.", dest.Name, sourceConverterColoredGray2.Name);
								Connect(sourceConverterColoredGray2, destRgb24, FrameFormat.ColoredGray2, FrameFormat.Rgb24);
								converterConnected = true;

							} else if (destBitmap != null) {
								Logger.Warn("Destination {0} doesn't support colored 2-bit frames from {1} converter, converting to RGB source.", dest.Name, sourceConverterColoredGray2.Name);
								Connect(sourceConverterColoredGray2, destBitmap, FrameFormat.ColoredGray2, FrameFormat.Bitmap);
								converterConnected = true;
							}
						}

						// if converter emits colored gray-4 frames..
						if (sourceConverterColoredGray4 != null) {
							// if destination can render colored gray-4 frames...
							if (destColoredGray4 != null) {
								Logger.Info("Hooking colored 4-bit source of {0} converter to {1}.", sourceConverterColoredGray4.Name, dest.Name);
								Connect(sourceConverterColoredGray4, destColoredGray4, FrameFormat.ColoredGray4, FrameFormat.ColoredGray4);
								converterConnected = true;

								// otherwise, convert to rgb24
							} else if (destRgb24 != null) {
								Logger.Warn("Destination {0} doesn't support colored 4-bit frames from {1} converter, converting to RGB source.", dest.Name, sourceConverterColoredGray4.Name);
								Connect(sourceConverterColoredGray4, destRgb24, FrameFormat.ColoredGray4, FrameFormat.Rgb24);
								converterConnected = true;

							} else if (destBitmap != null) {
								Logger.Warn("Destination {0} doesn't support colored 4-bit frames from {1} converter, converting to Bitmap source.", dest.Name, sourceConverterColoredGray4.Name);
								Connect(sourceConverterColoredGray4, destBitmap, FrameFormat.ColoredGray4, FrameFormat.Bitmap);
								converterConnected = true;
							}
						}

						// if converter emits colored gray-6 frames..
						if (sourceConverterColoredGray6 != null) {
							// if destination can render colored gray-6 frames...
							if (destColoredGray6 != null) {
								Logger.Info("Hooking colored 6-bit source of {0} converter to {1}.", sourceConverterColoredGray6.Name, dest.Name);
								Connect(sourceConverterColoredGray6, destColoredGray6, FrameFormat.ColoredGray6, FrameFormat.ColoredGray6);
								converterConnected = true;

							// otherwise, convert to rgb24
							} else if (destRgb24 != null) {
								Logger.Warn("Destination {0} doesn't support colored 6-bit frames from {1} converter, converting to RGB source.", dest.Name, sourceConverterColoredGray6.Name);
								Connect(sourceConverterColoredGray6, destRgb24, FrameFormat.ColoredGray6, FrameFormat.Rgb24);
								converterConnected = true;

							} else if (destBitmap != null) {
								Logger.Warn("Destination {0} doesn't support colored 6-bit frames from {1} converter, converting to Bitmap source.", dest.Name, sourceConverterColoredGray6.Name);
								Connect(sourceConverterColoredGray6, destBitmap, FrameFormat.ColoredGray6, FrameFormat.Bitmap);
								converterConnected = true;
							}
						}

						// if converter emits RGB24 frames..
						if (sourceConverterRgb24 != null && destRgb24 != null) {
							Logger.Info("Hooking RGB24 source of {0} converter to {1}.", sourceConverterRgb24.Name, dest.Name);
							Connect(sourceConverterRgb24, destRgb24, FrameFormat.Rgb24, FrameFormat.Rgb24);
							converterConnected = true;
						}

						// render graph is already set up through converters, so we skip the rest below
						if (converterConnected) {
							continue;
						}
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
					var destAlphaNumeric = dest as IAlphaNumericDestination;

					var sourceColoredGray2 = Source as IColoredGray2Source;
					var sourceColoredGray4 = Source as IColoredGray4Source;
					var sourceColoredGray6 = Source as IColoredGray6Source;
					var sourceRgb24 = Source as IRgb24Source;
					var sourceBitmap = Source as IBitmapSource;
					var sourceAlphaNumeric = Source as IAlphaNumericSource;

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
				if (onError != null) {
					onError.Invoke(ex);
				} else {
					throw;
				}
			}
			_activeRenderer = new RenderDisposable(this, _activeSources);
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
							Subscribe(
								sourceGray2.GetGray2Frames(), 
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray2.RenderGray2
							);
							break;

						// gray2 -> gray4
						case FrameFormat.Gray4:
							throw new IncompatibleGraphException("Cannot convert from gray2 to gray4 (every gray4 destination should be able to do gray2 as well).");

						// gray2 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray2, dest, destRgb24, from, to);
							Subscribe(
								sourceGray2.GetGray2Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToRgb24(_gray2Palette ?? _gray2Colors)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24
							);
							break;

						// gray2 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray2, dest, destBitmap, from, to);
							Subscribe(
								sourceGray2.GetGray2Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToBmp(_gray2Palette ?? _gray2Colors)
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
								sourceGray4.GetGray4Frames(),
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
								sourceGray4.GetGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// gray4 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceGray4, dest, destRgb24, from, to);
							Subscribe(
								sourceGray4.GetGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToRgb24(_gray4Palette ?? _gray4Colors)
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// gray4 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceGray4, dest, destBitmap, from, to);
							Subscribe(
								sourceGray4.GetGray4Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToBmp(_gray4Palette ?? _gray4Colors)
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
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// rgb24 -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceRgb24, dest, destRgb24, from, to);
							Subscribe(
								sourceRgb24.GetRgb24Frames(),
								frame => frame
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// rgb24 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceRgb24, dest, destBitmap, from, to);
							Subscribe(
								sourceRgb24.GetRgb24Frames(),
								frame => frame
									.ConvertToBmp()
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
									.TransformGray(this, destFixedSize, destMultiSize),
								destGray4.RenderGray4);
							break;

						// bitmap -> rgb24
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceBitmap, dest, destRgb24, from, to);
							Subscribe(
								sourceBitmap.GetBitmapFrames(),
								frame => frame
									.ConvertToRgb24()
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// bitmap -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(Source, sourceBitmap, dest, destBitmap, from, to);
							Subscribe(
								sourceBitmap.GetBitmapFrames(),
								frame => frame
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
						case FrameFormat.Rgb24:
							AssertCompatibility(source, sourceColoredGray6, dest, destRgb24, from, to);
							Subscribe(
								sourceColoredGray6.GetColoredGray6Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToRgb24()
									.TransformRgb24(this, destFixedSize, destMultiSize),
								destRgb24.RenderRgb24);
							break;

						// colored gray6 -> bitmap
						case FrameFormat.Bitmap:
							AssertCompatibility(source, sourceColoredGray6, dest, destBitmap, from, to);
							Subscribe(
								sourceColoredGray6.GetColoredGray6Frames(),
								frame => frame
									.TransformHdScaling(destFixedSize, ScalerMode)
									.ConvertToBitmap()
									.Transform(this, destFixedSize, destMultiSize),
								destBitmap.RenderBitmap);
							break;

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
				dest = dest.ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current));
				_activeSources.Add(dest.Subscribe(f => StartIdling()));

			} else {
				
				// subscribe and add to active sources
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
				//src = src.ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current));
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
