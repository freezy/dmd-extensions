using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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

	public class ProPinballSlave : IGray4Source, IBitmapSource
	{
		public string Name { get; } = "Pro Pinball";
		public BehaviorSubject<DisplaySize> Dimensions { get; } = new BehaviorSubject<DisplaySize>(new DisplaySize { Width = 128, Height = 32 });

		public Color Color { get; set; } = Color.FromRgb(255, 191, 0);

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly uint _messageBufferSize = 392;
		private ProPinballBridge.ProPinballDmd _bridge;
		private IObservable<BitmapSource> _frames;
		private IObservable<byte[]> _framesGrey4;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ProPinballSlave(string args)
		{
			var match = Regex.Match(args, @"m(\d+)", RegexOptions.IgnoreCase);
			if (match.Success) {
				_messageBufferSize = uint.Parse(match.Groups[1].Value);
			}
		}


		public IObservable<BitmapSource> GetFrames()
		{
			if (_frames != null) {
				return _frames;
			}
			CreateBridge();

			double hue, saturation, luminosity;
			ColorUtil.RgbToHsl(Color.R, Color.G, Color.B, out hue, out saturation, out luminosity);
			Logger.Info("Subscribing to Pro Pinball's message queue...");

			_frames = Observable.Create<BitmapSource>(o => {

				// this is blocking, so use a new thread
				var thread = new Thread(() => {
					unsafe {
						_bridge.GetFrames(frame => {
							o.OnNext(ImageUtil.ConvertFromGray4(Dimensions.Value.Width, Dimensions.Value.Width, frame, hue, saturation, luminosity));

						}, err => {
							throw new ProPinballSlaveException(new string(err));

						}, () => {
							Logger.Debug("Received exit signal from Pro Pinball, closing.");

							// spent 2 days figuring out why instantiating boost::interprocess::message_queue
							// in c++ would crash (or block) the app. fuck it, let's just kill the process.
							Process.GetCurrentProcess().Kill();
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
			return _frames;
		}

		public IObservable<byte[]> GetGray4Frames()
		{
			if (_framesGrey4 != null) {
				return _framesGrey4;
			}
			CreateBridge();

			Logger.Info("Subscribing to Pro Pinball's message queue...");
			_framesGrey4 = Observable.Create<byte[]>(o => {

				var len = Dimensions.Value.Width * Dimensions.Value.Height;

				// this is blocking, so use a new thread
				var thread = new Thread(() => {
					unsafe {
						_bridge.GetFrames(frame => {
							var arr = new byte[len];
							Marshal.Copy((IntPtr)frame, arr, 0, len);
							o.OnNext(arr);

						}, err => {
							throw new ProPinballSlaveException(new string(err));

						}, () => {
							Logger.Debug("Received exit signal from Pro Pinball, closing.");
							Process.GetCurrentProcess().Kill();
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
			return _framesGrey4;
		}

		private void CreateBridge()
		{
			if (_bridge != null) {
				throw new ProPinballSlaveException("Can only have one active source at the time!");
			}
			_bridge = new ProPinballBridge.ProPinballDmd(_messageBufferSize);
			if (_bridge.Status != 0) {
				unsafe {
					throw new ProPinballSlaveException("Error connecting: " + new string(_bridge.Error));
				}
			}
		}
	}

	public class ProPinballSlaveException : Exception
	{
		public ProPinballSlaveException(string message) : base(message)
		{
		}
	}
}
