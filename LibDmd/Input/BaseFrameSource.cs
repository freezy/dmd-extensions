using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.RightsManagement;
using System.Windows.Media.Imaging;
using LibDmd.Common;

namespace LibDmd.Input
{
	/// <summary>
	/// Acts as source for any frames ending up on the DMD.
	/// </summary>
	/// <remarks>
	/// Since we want a contineous flow of frames, the method to override
	/// returns an observable. Note that the producer decides on the frequency
	/// in which frames are delivered to the consumer.
	/// </remarks>
	public abstract class BaseFrameSource
	{
		/// <summary>
		/// A display name for the source
		/// </summary>
		public abstract string Name { get; }

		/// <summary>
		/// Returns an observable that produces a sequence of frames.
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		/// <returns></returns>
		public abstract IObservable<Frame> GetFrames();

		/// <summary>
		/// An observable that triggers when the source starts providing frames.
		/// </summary>
		public IObservable<Unit> OnResume => _onResume;

		/// <summary>
		/// An observable that triggers when the source is interrupted, e.g. a game is stopped.
		/// </summary>
		public IObservable<Unit> OnPause => _onPause;

		protected readonly ISubject<Unit> _onResume = new Subject<Unit>();
		protected readonly ISubject<Unit> _onPause = new Subject<Unit>();
	}

	public class Frame
	{
		public static bool EnableProfiling = true;

		public BitmapSource Bitmap { get; set; }
		public ProfilerFrame ProfilerFrame { get; }
		public bool HasBitmap => Bitmap != null;

		public Frame()
		{
			if (EnableProfiling) {
				ProfilerFrame = new ProfilerFrame();
			}
		}

		public Frame SetBitmap(BitmapSource bmp)
		{
			Bitmap = bmp;
			return this;
		}
	}
}
