using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PinDmd.Input;
using PinDmd.Output;
using PinDmd.Processor;

namespace PinDmd
{
	public class RenderGraph
	{
		public IFrameSource Source { get; set; }
		public List<AbstractProcessor> Processors { get; set; }
		public List<IFrameDestination> Destinations { get; set; }
		public IObservable<BitmapSource> BeforeProcessed => _beforeProcessed;
		public bool IsRendering { get; set; }

		private readonly List<IDisposable> _activeSources = new List<IDisposable>();
		private readonly Subject<BitmapSource> _beforeProcessed = new Subject<BitmapSource>();

		public void StartRendering()
		{
			if (_activeSources.Count > 0) {
				throw new RendersAlreadyActiveException("Renders already active, please stop before re-launching.");
			}
			IsRendering = true;
			var enabledProcessors = Processors.Where(processor => processor.Enabled);

			foreach (var dest in Destinations) {
				var frames = Source.GetFrames();
				_activeSources.Add(frames.Subscribe(bmp => {

					_beforeProcessed.OnNext(bmp);

					if (Processors != null) {
						bmp = enabledProcessors.Aggregate(bmp,
							(current, processor) => processor.Process(current));
					}
					dest.Render(bmp);
				}));
			}
		}

		public void StopRendering()
		{
			foreach (var source in _activeSources) {
				source.Dispose();
			}
			Console.WriteLine("Source for {0} renderer(s) stopped.", _activeSources.Count);
			_activeSources.Clear();
			IsRendering = false;
		}

		public void Render(BitmapSource bmp)
		{
			foreach (var dest in Destinations) {
				dest.Render(bmp);
			}
		}
	}

	public class RendersAlreadyActiveException : Exception
	{
		public RendersAlreadyActiveException(string message) : base(message)
		{
		}
	}
}
