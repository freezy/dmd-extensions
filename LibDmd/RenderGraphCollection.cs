using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Input;

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

		private readonly BehaviorSubject<Dimensions> _dimensions = new BehaviorSubject<Dimensions>(new Dimensions { Width = 128, Height = 32 });

		public void Add(RenderGraph renderGraph)
		{
			renderGraph.Source.Dimensions = _dimensions;
			_graphs.Add(renderGraph);
		}

		public void StartRendering()
		{
			_graphs.ForEach(graph => _renderers.Add(graph.StartRendering()));
		}

		public void SetColor(Color color)
		{
			_graphs.ForEach(graph => graph.SetColor(color));
		}

		public void ClearColor()
		{
			_graphs.ForEach(graph => graph.ClearColor());
		}

		public void SetPalette(Color[] palette)
		{
			_graphs.ForEach(graph => graph.SetPalette(palette));
		}

		public void ClearPalette()
		{
			_graphs.ForEach(graph => graph.ClearPalette());
		}

		public void Dispose()
		{
			_renderers.ForEach(r => r.Dispose());
			_renderers.Clear();
			_graphs.ForEach(graph => graph.Dispose());
			_graphs.Clear();
		}
	}
}
