using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Input.Passthrough;
using LibDmd.Output;
using NLog;

namespace LibDmd.Converter
{
	public abstract class AbstractConverter : AbstractSource, IDisposable
	{
		/// <summary>
		/// How many frames per second to send to converters with <see cref="_clockAndDedupe"/> enabled.
		/// </summary>
		private const int ClockFps = 60;

		/// <summary>
		/// Source bit length
		/// </summary>
		public abstract IEnumerable<FrameFormat> From { get; }

		public IObservable<Unit> OnResume { get; } = null;
		public IObservable<Unit> OnPause { get; } = null;

		// use these sources when de-duping
		protected readonly PassthroughColoredGray2Source DedupedColoredGray2Source;
		protected readonly PassthroughColoredGray4Source DedupedColoredGray4Source;
		protected readonly PassthroughColoredGray6Source DedupedColoredGray6Source;
		protected readonly PassthroughRgb24Source DedupedRgb24Source;

		/// <summary>
		/// If set, the converter will get frames at 60Hz, independently whether they are
		/// duplicated or not. The *output* of the converter will de-duplicated.
		/// </summary>
		private readonly bool _clockAndDedupe;
		private DmdFrame _lastDmdFrame;
		private readonly IDisposable _clock;
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat = new BehaviorSubject<FrameFormat>(FrameFormat.AlphaNumeric);

		/// <summary>
		/// Remembers to which destinations this converter is already connected to, so we can skip connecting them
		/// again when using multiple render graphs.
		/// </summary>
		private readonly Dictionary<IDestination, List<(FrameFormat, FrameFormat)>> _connections = new Dictionary<IDestination, List<(FrameFormat, FrameFormat)>>();

		private readonly HashSet<IColorRotationDestination> _connectedColorRotationDestinations = new HashSet<IColorRotationDestination>();
		private readonly HashSet<IFrameEventDestination> _connectedFrameEventDestinations = new HashSet<IFrameEventDestination>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected AbstractConverter(bool clockAndDedupe)
		{
			DedupedColoredGray2Source = new PassthroughColoredGray2Source(_lastFrameFormat, dedupe: clockAndDedupe);
			DedupedColoredGray4Source = new PassthroughColoredGray4Source(_lastFrameFormat, dedupe: clockAndDedupe);
			DedupedColoredGray6Source = new PassthroughColoredGray6Source(_lastFrameFormat, dedupe: clockAndDedupe);
			DedupedRgb24Source = new PassthroughRgb24Source(_lastFrameFormat, deDupe: clockAndDedupe);
			_clockAndDedupe = clockAndDedupe;

			if (clockAndDedupe) {
				_clock = Observable
					.Interval(TimeSpan.FromSeconds(1d / ClockFps))
					.Subscribe(Tick);
			}
		}

		/// <summary>
		/// Receives de-duped frames and outputs them to the output sources the converter implements.<br/>
		///
		/// For converters that need frames at 60Hz, this method should not be overridden,
		/// but <see cref="ConvertClocked"/> instead. For converters that work with de-duped frames,
		/// override this as usual.
		/// </summary>
		///
		/// <remarks>
		/// Note that if your converter doesn't implement any ISource interface,
		/// frames will just be dropped.
		///
		/// If this method doesn't send anything to its output sources, the frame is
		/// equally dropped.
		/// </remarks>
		///
		/// <param name="frame">Source frame</param>
		public virtual void Convert(DmdFrame frame)
		{
			#if DEBUG
			if (!_clockAndDedupe) {
				throw new InvalidOperationException("Convert() must be overridden if convertor doesn't receive clocked frames.");
			}
			#endif
			_lastDmdFrame = frame;
		}

		/// <summary>
		/// Receives frames at 60Hz and sends them to the output sources, where they are de-duped. In this case,
		/// <see cref="DedupedColoredGray2Source"/>, <see cref="DedupedColoredGray4Source"/>, <see cref="DedupedColoredGray6Source"/> and
		/// <see cref="DedupedRgb24Source"/> should be used, otherwise, de-duping can't be applied.
		/// </summary>
		/// <remarks>
		/// If your converter doesn't need frames at 60Hz, override <see cref="Convert(LibDmd.Frame.DmdFrame)"/> and
		/// ignore this method.
		/// </remarks>
		/// <param name="frame">Source frame</param>
		protected virtual void ConvertClocked(DmdFrame frame)
		{
		}

		/// <summary>
		/// Receives alphanumeric frames and converts them into colored DMD frames (if supported).
		/// </summary>
		/// <param name="frame">Source frame</param>
		public virtual void Convert(AlphaNumericFrame frame)
		{
		}

		private void Tick(long _)
		{
			if (_lastDmdFrame == null) {
				return;
			}

			ConvertClocked(_lastDmdFrame);
		}

		public void Dispose()
		{
			_lastFrameFormat?.Dispose();
			_clock?.Dispose();
		}

		#region Connection Handling

		public void SetConnected(IDestination dest, FrameFormat from, FrameFormat to)
		{
			if (!_connections.ContainsKey(dest)) {
				_connections[dest] = new List<(FrameFormat, FrameFormat)>();
			}
			_connections[dest].Add((from, to));
		}
		public void SetConnected(IColorRotationDestination dest) => _connectedColorRotationDestinations.Add(dest);
		public void SetConnected(IFrameEventDestination dest) => _connectedFrameEventDestinations.Add(dest);

		public bool IsConnected(IDestination dest) => _connections.ContainsKey(dest);
		public bool IsConnected(IDestination dest, FrameFormat from)
			=> _connections.ContainsKey(dest) && _connections[dest].Any(t => t.Item1 == from);
		public bool IsConnected(IDestination dest, FrameFormat from, FrameFormat to)
			=> _connections.ContainsKey(dest) && _connections[dest].Any(t => t.Item1 == from && t.Item2 == to);
		public bool IsConnected(IColorRotationDestination dest) => _connectedColorRotationDestinations.Contains(dest);
		public bool IsConnected(IFrameEventDestination dest) => _connectedFrameEventDestinations.Contains(dest);

		#endregion
	}
}
