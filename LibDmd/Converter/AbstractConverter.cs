using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Converter
{
	public abstract class AbstractConverter : AbstractSource
	{
		/// <summary>
		/// Source bit length
		/// </summary>
		public abstract IEnumerable<FrameFormat> From { get; }

		/// <summary>
		/// Receives frames and outputs them to the output sources the converter implements.
		/// </summary>
		///
		/// <remarks>
		/// Note that if your convertor doesn't implement any ISource interface,
		/// frames will just be dropped.
		///
		/// If this method doesn't send anything to its output sources, the frame is
		/// equally dropped.
		/// </remarks>
		///
		/// <param name="frame">Source frame</param>
		public abstract void Convert(DmdFrame frame);

		/// <summary>
		/// Receives alphanumeric frames and converts them into colored DMD frames (if supported).
		/// </summary>
		/// <param name="frame">Source frame</param>
		public virtual void Convert(AlphaNumericFrame frame)
		{
		}

		public IObservable<Unit> OnResume { get; } = null;
		public IObservable<Unit> OnPause { get; } = null;

		private readonly Dictionary<IDestination, List<(FrameFormat, FrameFormat)>> _connections = new Dictionary<IDestination, List<(FrameFormat, FrameFormat)>>();

		public void SetConnected(IDestination dest, FrameFormat from, FrameFormat to)
		{
			if (!_connections.ContainsKey(dest)) {
				_connections[dest] = new List<(FrameFormat, FrameFormat)>();
			}
			_connections[dest].Add((from, to));
		}
		public bool IsConnected() => _connections.Any();
		public bool IsConnected(IDestination dest, FrameFormat from)
			=> _connections.ContainsKey(dest) && _connections[dest].Any(t => t.Item1 == from);
		public bool IsConnected(IDestination dest, FrameFormat from, FrameFormat to)
			=> _connections.ContainsKey(dest) && _connections[dest].Any(t => t.Item1 == from && t.Item2 == to);


	}
}
