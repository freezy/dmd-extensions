using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Input;

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

		public ColorRotationWrapper(IColoredGray6Source frameSource, IColorRotationSource rotationSource)
		{
			_source = frameSource;
			_disposables.Add(frameSource.GetColoredGray6Frames().Subscribe(UpdateFrame));
			_disposables.Add(rotationSource.GetPaletteChanges().Subscribe(UpdatePalette));
		}

		private void UpdateFrame(ColoredFrame frame)
		{
			_frame = frame;
			_rgb24Frames.OnNext(_frame.ConvertToRgb24());
		}

		public void UpdatePalette(Color[] palette)
		{
			_frame.Update(_frame.Data, palette);
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
