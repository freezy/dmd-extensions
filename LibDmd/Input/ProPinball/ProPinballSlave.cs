using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;
using static System.Reflection.Assembly;

namespace LibDmd.Input.ProPinball
{

	public class ProPinballSlave : IFrameSource
	{
		private const int Width = 128;
		private const int Height = 32;

		public string Name { get; } = "Pro Pinball";

		public Color Color { get; set; } = Color.FromRgb(255, 191, 0);

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private IObservable<BitmapSource> _frames;
		private ProPinballBridge.ProPinballDmd _bridge;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Starts sending frames.
		/// </summary>
		private void StartCapturing()
		{
			_bridge = new ProPinballBridge.ProPinballDmd();

			Logger.Info("DMD status: {0}", _bridge.Status);
			if (_bridge.Status != 0) {
				unsafe {
					throw new ProPinballSlaveException("Error connecting: " + new string(_bridge.Error));
				}
			}
			double hue, saturation, luminosity;
			ColorUtil.RgbToHsl(Color.R, Color.G, Color.B, out hue, out saturation, out luminosity);
			Logger.Info("Subscribing to Pro Pinball's message queue...");

			_frames = Observable.Create<BitmapSource>(o => {

				// this is blocking, so use a new thread
				var thread = new Thread(() => {
					unsafe {
						_bridge.GetFrames(frame => {
							var bmp = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgr32, null);
							var bufferSize = (Math.Abs(bmp.BackBufferStride) * Height + 2);
							var frameBuffer = new byte[bufferSize];

							var index = 0;
							bmp.Lock();
							for (var y = 0; y < Height; y++) {
								for (var x = 0; x < Width; x++) {

									var pixelLum = frame[y * Width + x];
									var lum = (double)pixelLum / 15 * luminosity;
									byte red, green, blue;
									ColorUtil.HslToRgb(hue, saturation, lum, out red, out green, out blue);
									
									frameBuffer[index] = blue;
									frameBuffer[index + 1] = green;
									frameBuffer[index + 2] = red;

									index += 4;
								}
							}
							bmp.WritePixels(new Int32Rect(0, 0, Width, Height), frameBuffer, bmp.BackBufferStride, 0);
							bmp.Unlock();
							bmp.Freeze();
							o.OnNext(bmp);

						}, err => {
							throw new ProPinballSlaveException(new string(err));

						}, () => {
							Logger.Debug("Received exit signal from Pro Pinball, closing.");
							o.OnCompleted();
							Thread.CurrentThread.Abort();
						});
					}
				});
				thread.Start();
				Logger.Debug("Subscribed to Pro Pinball's message queue.");

				return Disposable.Create(() => {
					thread.Abort();
					Logger.Debug("Disposing Pro Pinball's message queue...");
				});
			});
		}

		public IObservable<BitmapSource> GetFrames()
		{
			StartCapturing();
			return _frames;
		}
	}

	public class ProPinballSlaveException : Exception
	{
		public ProPinballSlaveException(string message) : base(message)
		{
		}
	}
}
