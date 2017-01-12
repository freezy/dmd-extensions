using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd
{
	/// <summary>
	/// Groups multiple render graphs.
	/// </summary>
	/// 
	/// <remarks>
	/// This should be used as soon as more than one render graph is created,
	/// since it also manages common properties such as the dimension observable
	/// among graphs.
	/// </remarks>
	public class RenderGraphCollection : IDisposable
	{
		private readonly List<RenderGraph> _graphs = new List<RenderGraph>(); 
		private readonly List<IDisposable> _renderers = new List<IDisposable>();
		private readonly List<IRgb24Destination> _rgb24Destinations = new List<IRgb24Destination>();
		private readonly BehaviorSubject<Dimensions> _dimensions = new BehaviorSubject<Dimensions>(new Dimensions { Width = 128, Height = 32 });

		public void Add(RenderGraph renderGraph)
		{
			// use a common observable for all sources so we get proper notification when any of them changes size
			renderGraph.Source.Dimensions = _dimensions;
			_graphs.Add(renderGraph);

			renderGraph.Destinations.ForEach(dest => {
				var d = dest as IRgb24Destination;
				if (!_rgb24Destinations.Contains(d)) {
					_rgb24Destinations.Add(d);
				}
			});
		}

		public void StartRendering()
		{
			_graphs.ForEach(graph => _renderers.Add(graph.StartRendering()));
		}

		public void SetColor(Color color)
		{
			_graphs.ForEach(graph => graph.SetColor(color));
			_rgb24Destinations.ForEach(dest => dest.SetColor(color));
		}

		public void ClearColor()
		{
			_graphs.ForEach(graph => graph.ClearColor());
			_rgb24Destinations.ForEach(dest => dest.ClearColor());
		}

		public void SetPalette(Color[] palette)
		{
			_graphs.ForEach(graph => graph.SetPalette(palette));
			_rgb24Destinations.ForEach(dest => dest.SetPalette(palette));
		}

		public void ClearPalette()
		{
			_graphs.ForEach(graph => graph.ClearPalette());
			_rgb24Destinations.ForEach(dest => dest.ClearPalette());
		}

		public void Dispose()
		{
			_renderers.ForEach(r => r.Dispose());
			_renderers.Clear();
			_rgb24Destinations.Clear();
			_graphs.ForEach(graph => graph.Dispose());
			_graphs.Clear();
		}
	}
}
