using System;
using System.Collections.Generic;
using System.Linq;
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

		public void StartRendering()
		{
			foreach (var dest in Destinations) {
				dest.StartRendering(Source);
			}

		}

		public void StopRendering()
		{
			foreach (var dest in Destinations) {
				dest.StopRendering();
			}
		}

	}
}
