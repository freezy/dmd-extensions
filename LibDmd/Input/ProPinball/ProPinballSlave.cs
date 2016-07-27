using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using NLog;

namespace LibDmd.Input.ProPinball
{

	public class ProPinballSlave : IFrameSource
	{
		const int FrameSize = 128 * 32;

		public string Name { get; } = "Pro Pinball";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly ISubject<BitmapSource> _frames = new Subject<BitmapSource>();
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
					Logger.Error("Error: {0}", new string(_bridge.Error));
				}
			} else {
				Logger.Info("Subscribing to Pro Pinball's message queue...");
				unsafe {
					_bridge.GetFrames(frame => {
						Console.WriteLine("Got a frame!");
						var f = new byte[FrameSize];
						Marshal.Copy((IntPtr)frame, f, 0, FrameSize);
					});
				}
			}
		}

		public IObservable<BitmapSource> GetFrames()
		{
			StartCapturing();
			return _frames;
		}

	}
}
