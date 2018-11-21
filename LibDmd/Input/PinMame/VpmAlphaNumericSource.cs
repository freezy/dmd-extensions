using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Common;

namespace LibDmd.Input.PinMame
{
	/// <summary>
	/// Receives alphanumeric frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class VpmAlphaNumericSource : AbstractSource, IAlphaNumericSource
	{
		public override string Name { get; } = "VPM Alpha Numeric Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;
		
		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly ISubject<AlphaNumericFrame> _framesAlphaNumeric;

		public VpmAlphaNumericSource(AlphaNumericFrame initialFrame)
		{
			_framesAlphaNumeric = new BehaviorSubject<AlphaNumericFrame>(initialFrame);
		}

		public VpmAlphaNumericSource()
		{
			_framesAlphaNumeric =  new Subject<AlphaNumericFrame>();
		}

		public void NextFrame(AlphaNumericFrame frame)
		{
			_framesAlphaNumeric.OnNext(frame);
		}

		public IObservable<AlphaNumericFrame> GetAlphaNumericFrames()
		{
			return _framesAlphaNumeric;
		}
	}
}
