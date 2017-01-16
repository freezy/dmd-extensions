using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Converter;
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
		private readonly List<IResizableDestination> _resizableDestinations = new List<IResizableDestination>();
		private readonly List<IConverter> _converters = new List<IConverter>();
		private readonly BehaviorSubject<Dimensions> _dimensions = new BehaviorSubject<Dimensions>(new Dimensions { Width = 128, Height = 32 });

		public void Add(RenderGraph renderGraph)
		{
			// use a common observable for all sources so we get proper notification when any of them changes size
			renderGraph.Source.Dimensions = _dimensions;
			var converter = renderGraph.Converter as ISource;
			if (converter != null) {
				converter.Dimensions = _dimensions;
			}
			_graphs.Add(renderGraph);

			renderGraph.Destinations.ForEach(dest => {
				var rgb24Destination = dest as IRgb24Destination;
				var resizableDestinations = dest as IResizableDestination;
				if (rgb24Destination != null && !_rgb24Destinations.Contains(rgb24Destination)) {
					_rgb24Destinations.Add(rgb24Destination);
				}
				if (resizableDestinations != null && !_resizableDestinations.Contains(resizableDestinations)) {
					_resizableDestinations.Add(resizableDestinations);
				}
			});

			if (renderGraph.Converter != null && !_converters.Contains(renderGraph.Converter)) {
				_converters.Add(renderGraph.Converter);
			}
		}

		public RenderGraphCollection Init()
		{
			_resizableDestinations.ForEach(dest => _dimensions.Subscribe(dim => dest.SetDimensions(dim.Width, dim.Height)));
			_converters.ForEach(converter => converter.Init());
			return this;
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
