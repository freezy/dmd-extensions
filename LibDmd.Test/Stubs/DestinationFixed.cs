using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public abstract class DestinationFixed<TFrame> : IFixedSizeDestination, ITestDestination<TFrame>
	{
		public IConnectableObservable<TFrame> Frame { get; private set; }
		public bool NeedsIdentificationFrames => false;
		
		public Dimensions FixedSize { get; }
		
		public bool DmdAllowHdScaling { get; set; }
		
		protected Subject<TFrame> LastFrame = new Subject<TFrame>();

		private IDisposable _disposable;

		protected DestinationFixed(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true)
		{
			FixedSize = new Dimensions(dmdWidth, dmdHeight);
			DmdAllowHdScaling = dmdAllowHdScaling;
			Frame = LastFrame.FirstAsync().PublishLast();
			_disposable = Frame.Connect();
		}

		public void Reset()
		{
			_disposable?.Dispose();
			Frame = LastFrame.FirstAsync().PublishLast();
			_disposable = Frame.Connect();
		}

		public void ClearDisplay()
		{
			LastFrame = null;
		}
	}
}
