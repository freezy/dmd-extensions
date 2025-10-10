using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public abstract class DestinationDynamic<TFrame> : IResizableDestination, ITestDestination<TFrame>
	{
		public IConnectableObservable<TFrame> Frame { get; private set; }

		public bool NeedsIdentificationFrames => false;
		
		protected Subject<TFrame> LastFrame = new Subject<TFrame>();
		
		public void SetDimensions(Dimensions newDimensions)
		{
			throw new System.NotImplementedException();
		}

		protected DestinationDynamic()
		{
			Frame = LastFrame.FirstAsync().PublishLast();
			Frame.Connect();
		}

		public void Reset()
		{
			Frame = LastFrame.FirstAsync().PublishLast();
			Frame.Connect();
		}

		public void ClearDisplay()
		{
			LastFrame = null;
		}
	}
}
