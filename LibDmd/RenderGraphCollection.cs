using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Converter;
using LibDmd.Frame;
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
		public ICollection<RenderGraph> Graphs => _graphs;
		private readonly List<RenderGraph> _graphs = new List<RenderGraph>(); 
		private readonly List<IRgb24Destination> _rgb24Destinations = new List<IRgb24Destination>();
		private readonly List<IResizableDestination> _resizableDestinations = new List<IResizableDestination>();
		private readonly List<AbstractConverter> _converters = new List<AbstractConverter>();
		private readonly BehaviorSubject<Dimensions> _dimensions = new BehaviorSubject<Dimensions>(new Dimensions(128, 32));

		/// <summary>
		/// Special case: We have no graphs, only one IRenderer
		/// </summary>
		private IRenderer _renderer;

		public void Add(IRenderer renderer) {
			_renderer = renderer;
		}

		public void Add(RenderGraph renderGraph)
		{
			_graphs.Add(renderGraph);

			renderGraph.Destinations.ForEach(dest => {
				var resizableDestinations = dest as IResizableDestination;
				if (dest is IRgb24Destination rgb24Destination && !_rgb24Destinations.Contains(rgb24Destination)) {
					_rgb24Destinations.Add(rgb24Destination);
				}
				if (resizableDestinations != null && !_resizableDestinations.Contains(resizableDestinations)) {
					_resizableDestinations.Add(resizableDestinations);
				}
			});
		}

		public RenderGraphCollection Init()
		{
			if (_renderer != null) {
				_renderer.Init();
				return this;
			}
			_resizableDestinations.ForEach(dest => _dimensions.Subscribe(dest.SetDimensions));
			
			foreach (var renderGraph in _graphs)
			{
				if (renderGraph.Converter != null && !_converters.Contains(renderGraph.Converter)) {
					_converters.Add(renderGraph.Converter);
				}
			}
			return this;
		}

		public void StartRendering(Action onCompleted, Action<Exception> onError = null)
		{
			if (_renderer != null) {
				_renderer.StartRendering(onCompleted, onError);

			} else {
				_graphs.ForEach(graph => graph.StartRendering(onCompleted, onError));
			}
		}

		public void StartRendering()
		{
			if (_renderer != null) {
				throw new InvalidOperationException("Must use a callback when using IRenderer.");
			}
			_graphs.ForEach(graph => graph.StartRendering());
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

		public void SetPalette(Color[] palette, int index)
		{
			_graphs.ForEach(graph => graph.SetPalette(palette, index));
			_rgb24Destinations.ForEach(dest => dest.SetPalette(palette));
		}

		public void ClearPalette()
		{
			_graphs.ForEach(graph => graph.ClearPalette());
			_rgb24Destinations.ForEach(dest => dest.ClearPalette());
		}

		public void ClearDisplay()
		{
			if (_renderer != null) {
				_renderer.ClearDisplay();

			} else {
				_graphs.ForEach(graph => graph.ClearDisplay());
			}
		}

		public void Dispose()
		{
			if (_renderer != null) {
				_renderer.Dispose();

			} else {
				_rgb24Destinations.Clear();
				_graphs.ForEach(graph => graph.Dispose());
				_graphs.Clear();
			}
		}

		public void AddDestination(IDestination dest)
		{
			_graphs.ForEach(graph => graph.Destinations.Add(dest));
		}
	}
}
