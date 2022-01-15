using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using VPinMAMELib;

namespace PinMameTest
{
	public class VPinMameController
	{
		//public IObservable<DmdFrame> WhenDmdUpdatesFrame  => _whenDmdUpdatesFrame;
		public DmdSize DmdSize { get; private set; }
		public DmdFrame Dmd => UpdateDmd();

		private const int EventStarted = 1;
		private const int EventStopped = 0;

		private Subject<GameStatus> _status;
		private readonly Controller _controller = new Controller();
		//private readonly Subject<DmdFrame> _whenDmdUpdatesFrame = new Subject<DmdFrame>();

		//private IDisposable _ticks;
		private DmdFrame _dmd;

		public VPinMameController()
		{
			//_controller.Antialias = true;
			_controller.ShowDMDOnly = false;
			_controller.ShowFrame = false;
			_controller.ShowTitle = false;
			_controller.DoubleSize = true;
			_controller.LockDisplay = true;
			_controller.ShowDMDOnly = false;
			_controller.Hidden = true;
			_controller.HandleKeyboard = true;
			Console.WriteLine("[{0}] VPinMAME Controller created.", Thread.CurrentThread.ManagedThreadId);
		}

		/// <summary>
		/// Starts Visual PinMAME emulation of game.
		/// </summary>
		/// <param name="gameName">PinMAME short name (e.g. "tz_94h")</param>
		/// <returns>Observable returning game statuses</returns>
		public IObservable<GameStatus> Run(string gameName)
		{
			_controller.GameName = gameName;
			_status = new Subject<GameStatus>();

			Observable.FromEvent<_IControllerEvents_OnStateChangeEventHandler, int>(
				h => _controller.OnStateChange += h,
				h => _controller.OnStateChange -= h)
			.Subscribe(e => {
				Console.WriteLine("[{0}] Got new event from controller: {1}.", Thread.CurrentThread.ManagedThreadId, e);
				switch (e) {
					case EventStarted:
						Started();
						_status.OnNext(GameStatus.Started);
						break;
					case EventStopped:
						_status.OnNext(GameStatus.Stopped);
						Stopped();
						break;
					default:
						Console.WriteLine("Unkown event {0}", e);
						break;
				}
			});
			_controller.Run();
			return _status;
		}

		private void Started()
		{
			DmdSize = new DmdSize(_controller.RawDmdWidth, _controller.RawDmdHeight);
			_dmd = new DmdFrame(DmdSize);
/*			_ticks = Observable
				.Interval(TimeSpan.FromMilliseconds(1000))
				.SubscribeOn(Scheduler.Default)
				.Subscribe(x => { Update(); });*/
		}

		private void Update()
		{
			UpdateDmd();
		}

		private DmdFrame UpdateDmd()
		{
			var pixels = _controller.RawDmdPixels as object[];
			if (pixels == null) {
				return _dmd;
			}
			for (var i = 0; i < pixels.Length; i++) {
				var pixel = (byte)pixels[i];
				_dmd.Update(i % 128, i / 128, pixel / 100.0f);
			}
			//_whenDmdUpdatesFrame.OnNext(_dmd);
			return _dmd;
		}

		private void Stopped()
		{
			//_ticks.Dispose();
		}

		public VPinMameController Pause()
		{
			_controller.Pause = true;
			_status.OnNext(GameStatus.Paused);
			return this;
		}

		public VPinMameController Resume()
		{
			_controller.Pause = false;
			_status.OnNext(GameStatus.Resumed);
			return this;
		}

		public VPinMameController Stop()
		{
			_controller.Stop();
			return this;
		}
	}

	public enum GameStatus
	{
		Started, Stopped, Paused, Resumed
	}

	public class DmdFrame
	{
		public readonly DmdPixel[,] Pixels;
		public readonly DmdSize Size;

		public DmdFrame(DmdSize size)
		{
			Size = size;
			Pixels = new DmdPixel[size.Width, size.Height];
			for (var x = 0; x < size.Width; x++) {
				for (var y = 0; y < size.Height; y++) {
					Pixels[x, y] = new DmdPixel();
				}
			}
		}

		public void Update(int x, int y, float brightness)
		{
			Pixels[x, y].Set(x, y, brightness);
		}
	}

	public class DmdPixel
	{
		public int X { get; private set; }
		public int Y { get; private set; }
		public float Brightness { get; private set; }

		public void Set(int x, int y, float brightness)
		{
			X = x;
			Y = y;
			Brightness = brightness;
		}
	}

	public class DmdSize
	{
		public int Width { get; }
		public int Height { get; }

		public DmdSize(int width, int height)
		{
			Width = width;
			Height = height;
		}
	}
}
