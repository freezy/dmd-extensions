using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using LibDmd.Input;

namespace PinMameDevice
{
	/// <summary>
	/// An input source that is linked to VPinMAME.
	/// </summary>
	/// 
	/// <remarks>
	/// Data originates through <see cref="DmdDevice"/>, which is called by VPM,
	/// then passed to <see cref="DmdExt"/> which passes the frames to this 
	/// class.
	/// </remarks>
	class PinMameSource : IFrameSource, IFrameSourceGray2, IFrameSourceGray4, IFrameSourceRgb24
	{
		public string Name { get; } = "PinMAME Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<byte[]> FramesGray2 = new Subject<byte[]>();
		public readonly Subject<byte[]> FramesGray4 = new Subject<byte[]>();
		public readonly Subject<byte[]> FramesRgb24 = new Subject<byte[]>();

		public IObservable<BitmapSource> GetFrames()
		{
			// doesn't need to implemented, we'll never get bitmaps from VPinMAME.
			throw new NotImplementedException();
		}

		public IObservable<byte[]> GetGray2Frames()
		{
			return FramesGray2;
		}

		public IObservable<byte[]> GetGray4Frames()
		{
			return FramesGray4;
		}

		public IObservable<byte[]> GetRgb24Frames()
		{
			return FramesRgb24;
		}
	}
}
