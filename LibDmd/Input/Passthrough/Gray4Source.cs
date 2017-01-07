using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// An input source that just contains a 4-bit observable.
	/// </summary>
	public class Gray4Source : AbstractSource, IGray4Source
	{
		public override string Name { get; }
		public RenderBitLength NativeFormat { get; set; } = RenderBitLength.Gray2;

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<byte[]> FramesGray4 = new Subject<byte[]>();

		public Gray4Source(string name)
		{
			Name = name;
		}

		public IObservable<byte[]> GetGray4Frames()
		{
			return FramesGray4;
		}
	}
}
