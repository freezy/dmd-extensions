using System;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd
{
	public class RawRenderer : IRenderer
	{
		private readonly IRawSource _source;
		private readonly IRawOutput _output;

		public RawRenderer(IRawSource source, IRawOutput output)
		{
			_source = source;
			_output = output;
		}

		public IRenderer Init()
		{
			// nothing to init
			return this;
		}

		public IDisposable StartRendering(Action onCompleted, Action<Exception> onError = null)
		{
			return _source.GetRawdata().Subscribe(_output.RenderRaw, onCompleted);
		}

		public void ClearDisplay()
		{
			// nothing to clear
		}

		public void Dispose()
		{
			_output.Dispose();
		}

	}
}
