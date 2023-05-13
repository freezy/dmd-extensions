using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class TestGray2Destination : IGray2Destination, IFixedSizeDestination
	{
		public string Name => "Test Destination (Fixed Gray2)";
		public bool IsAvailable => true;
		public int DmdWidth { get; }
		public int DmdHeight { get; }
		public bool DmdAllowHdScaling { get; }

		public readonly IConnectableObservable<byte[]> LastFrame;
		private Subject<byte[]> _lastFrame = new Subject<byte[]>();

		public TestGray2Destination(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true)
		{
			DmdWidth = dmdWidth;
			DmdHeight = dmdHeight;
			DmdAllowHdScaling = dmdAllowHdScaling;
			LastFrame = _lastFrame.FirstAsync().PublishLast();
			LastFrame.Connect();
		}

		public void ClearDisplay()
		{
			_lastFrame = null;
		}

		public void RenderGray2(byte[] frame)
		{
			_lastFrame.OnNext(frame);
		}
		
		public void Dispose()
		{
		}
	}
}
