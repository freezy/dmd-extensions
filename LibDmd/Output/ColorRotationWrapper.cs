using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Common;

namespace LibDmd.Output
{
	public class ColorRotationWrapper : IRgb24Source, IDisposable
	{
		public string Name => _source.Name;
		public bool IsAvailable => true;
		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;
		public IObservable<DmdFrame> GetRgb24Frames() => _rgb24Frames;

		private readonly IColoredGray6Source _source;

		private readonly CompositeDisposable _disposables = new CompositeDisposable();
		private readonly Subject<DmdFrame> _rgb24Frames = new Subject<DmdFrame>();
		private ColoredFrame _frame;
		private readonly object _gate = new object();
		private readonly FrameBufferPool _buffers = new FrameBufferPool();
		private readonly bool _reuseFrameBuffers;

		public ColorRotationWrapper(IColoredGray6Source frameSource, IColorRotationSource rotationSource)
			: this(frameSource, rotationSource, false) { }

		public ColorRotationWrapper(IColoredGray6Source frameSource, IColorRotationSource rotationSource, bool reuseFrameBuffers)
		{
			_reuseFrameBuffers = reuseFrameBuffers;
			_source = frameSource;
			_disposables.Add(frameSource.GetColoredGray6Frames().Subscribe(UpdateFrame));
			_disposables.Add(rotationSource.GetPaletteChanges().Subscribe(UpdatePalette));
		}

		private void UpdateFrame(ColoredFrame frame)
		{
			lock (_gate) {
				if (_frame == null) _frame = new ColoredFrame();
				_frame.Update(frame);
				Render();
			}
		}

		private void UpdatePalette(Color[] palette)
		{
			lock (_gate) {
				if (_frame == null) return;
				Array.Copy(palette, _frame.Palette, palette.Length);
				Render();
			}
		}

		private void Render()
		{
			using (_reuseFrameBuffers ? _buffers.Enter() : FrameBufferPool.Suspend())
				_rgb24Frames.OnNext(_frame.ConvertToRgb24());
		}

		public void ClearDisplay()
		{
			// ignore, that's a side effect from IColorRotationDestination we don't need
		}

		public void Dispose()
		{
			_disposables.Dispose();
			_rgb24Frames?.Dispose();
		}
	}
}
