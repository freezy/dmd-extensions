using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using VPinMAMELib;

namespace PinMameTest
{
	public class VPinMameController
	{
		private const int EventStarted = 1;
		private const int EventStopped = 0;

		private Subject<GameStatus> _status;
		private readonly Controller _controller = new Controller();

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
		}

		private void Stopped()
		{
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
}
