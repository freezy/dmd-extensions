using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using PinDmd.Input;
using PinDmd.Output;
using PinDmd.Processor;

namespace PinDmd
{
	public class RenderGraph
	{
		public IFrameSource Source { get; set; }
		public List<IProcessor> Processors { get; set; }
		public List<IFrameDestination> Destinations { get; set; }

		private readonly List<IDisposable> _activeRenderers = new List<IDisposable>();

		public void StartRendering()
		{
			if (_activeRenderers.Count > 0) {
				throw new RendersAlreadyActiveException("Renders already active, please stop before re-launching.");
			}

			var enabledProcessors = Processors.Where(processor => processor.Enabled);

			foreach (var dest in Destinations) {
				var frames = Source.GetFrames();
				if (Processors != null) {
					_activeRenderers.Add(frames.Subscribe(bmp => {
						bmp = enabledProcessors.Aggregate(bmp, (current, processor) => processor.Process(current));
						dest.RenderBitmap(bmp);
					}));

				} else {
					_activeRenderers.Add(frames.Subscribe(dest.Render));
				}
			}
		}

		public void StopRendering()
		{
			foreach (var activeRenderer in _activeRenderers) {
				activeRenderer.Dispose();
			}
			Console.WriteLine("{0} renderer(s) stopped.", _activeRenderers.Count);
			_activeRenderers.Clear();
		}

		public void Render(Bitmap bmp)
		{
			foreach (var dest in Destinations) {
				dest.RenderBitmap(bmp);
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
