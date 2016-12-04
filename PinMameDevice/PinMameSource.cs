using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using LibDmd.Input;

namespace PinMameDevice
{
	class PinMameSource : IFrameSource, IFrameSourceGray2
	{
		public string Name { get; } = "PinMAME Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<BitmapSource> _frames;
		public readonly Subject<byte[]> FramesGray2 = new Subject<byte[]>();

		public PinMameSource()
		{
		}

		public IObservable<BitmapSource> GetFrames()
		{
			throw new NotImplementedException();
		}

		public IObservable<byte[]> GetGray2Frames()
		{
			return FramesGray2;
		}
	}
}
