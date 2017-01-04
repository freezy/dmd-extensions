using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using LibDmd.Input;

namespace LibDmd.Input
{
	/// <summary>
	/// An input source just contains observables with all subjects.
	/// </summary>
	public class PassthroughSource : AbstractSource, IGray2Source, IGray4Source, IRgb24Source
	{
		public override string Name { get; } = "Passthrough Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<byte[]> FramesGray2 = new Subject<byte[]>();
		public readonly Subject<byte[]> FramesGray4 = new Subject<byte[]>();
		public readonly Subject<byte[]> FramesRgb24 = new Subject<byte[]>();
		public readonly Subject<BitmapSource> FramesBitmap = new Subject<BitmapSource>();

		public IObservable<BitmapSource> GetFrames()
		{
			return FramesBitmap;
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
