using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Common
{
	/// <summary>Reusable snapshots for asynchronous ownership boundaries.</summary>
	internal sealed class FrameSnapshot<T> where T : class, ICloneable
	{
		private readonly DmdFrame _frame = new DmdFrame();
		private readonly ColoredFrame _colored = new ColoredFrame();
		private readonly RawFrame _raw = new RawFrame();
		private readonly AlphaNumericFrame _alpha = new AlphaNumericFrame();
		private readonly FrameEvent _event = new FrameEvent();
		private RetainedBuffer<Color> _palette;
		private RetainedBuffer<byte[]> _rawPlanes, _extraPlanes;
		private RetainedBuffer<RetainedBuffer<byte>> _rawData, _extraData;
		public T Value { get; private set; }

		public T Copy(T source)
		{
			if (source is ColoredFrame colored) {
				_colored.Update(colored);
				Value = (T)(object)_colored;
			} else if (source is RawFrame raw) {
				_raw.CopyFrom(raw);
				CopyPlanes(raw.RawPlanes, ref _raw.RawPlanes, ref _rawPlanes, ref _rawData);
				CopyPlanes(raw.ExtraRawPlanes, ref _raw.ExtraRawPlanes, ref _extraPlanes, ref _extraData);
				Value = (T)(object)_raw;
			} else if (source is DmdFrame frame) {
				Value = (T)(object)_frame.CopyFrom(frame);
			} else if (source is AlphaNumericFrame alpha) {
				_alpha.CopyFrom(alpha);
				Value = (T)(object)_alpha;
			} else if (source is Color[] palette) {
				var copy = _palette.Get(palette.Length);
				Array.Copy(palette, copy, palette.Length);
				Value = (T)(object)copy;
			} else if (source is FrameEvent frameEvent) {
				Value = (T)(object)_event.Update(frameEvent.EventId);
			} else if (source is FrameEventInit) {
				Value = source; // immutable
			} else {
				Value = (T)source.Clone();
			}
			return Value;
		}

		private static void CopyPlanes(byte[][] source, ref byte[][] target,
			ref RetainedBuffer<byte[]> containers, ref RetainedBuffer<RetainedBuffer<byte>> data)
		{
			if (source == null) { target = null; return; }
			target = containers.Get(source.Length);
			var buffers = data.Get(source.Length);
			for (var i = 0; i < source.Length; i++) {
				target[i] = buffers[i].Get(source[i].Length);
				Buffer.BlockCopy(source[i], 0, target[i], 0, source[i].Length);
			}
		}
	}

	/// <summary>
	/// A borrowed-frame rendering subscription. Async subscriptions have a persistent
	/// consumer and recycle queue nodes and snapshots instead of scheduling a new
	/// work item per frame. Each format subscription owns a worker (and FIFO); format
	/// changes keep the same independent scheduling as ObserveOn. Queue growth allocates
	/// only when a new peak is reached.
	/// </summary>
	internal sealed class BufferedFrameObserver<TIn, TOut> : IObserver<TIn>, IDisposable where TIn : class, ICloneable
	{
		private readonly object _gate = new object();
		private readonly object _renderGate = new object();
		private readonly Stack<Node> _free = new Stack<Node>();
		private readonly FrameBufferPool _buffers = new FrameBufferPool();
		private readonly Func<TIn, TOut> _processor;
		private readonly Action<TOut> _render;
		private readonly Action _stopIdle, _startIdle;
		private readonly int _idleAfter;
		private readonly Timer _idleTimer;
		private readonly Thread _worker;
		private Node _head, _tail;
		private volatile bool _disposed;
		private volatile bool _completed;
		private long _lastRenderTicks;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public BufferedFrameObserver(Func<TIn, TOut> processor, Action<TOut> render, bool synchronous,
			int idleAfter, Action stopIdle, Action startIdle)
		{
			_processor = processor;
			_render = render;
			_idleAfter = idleAfter;
			_stopIdle = stopIdle;
			_startIdle = startIdle;
			if (idleAfter > 0) _idleTimer = new Timer(OnIdle, null, Timeout.Infinite, Timeout.Infinite);
			if (!synchronous) {
				_worker = new Thread(Consume) { IsBackground = true, Name = "DMD frame renderer" };
				_worker.Start();
			}
		}

		public void OnNext(TIn frame)
		{
			if (_worker == null) {
				if (_completed) return;
				Process(frame);
				return;
			}
			lock (_gate) {
				if (_disposed || _completed) return;
				var node = _free.Count == 0 ? new Node() : _free.Pop();
				node.Snapshot.Copy(frame); // copy before the producer may reuse its buffer
				if (_tail == null) _head = node;
				else _tail.Next = node;
				_tail = node;
				Monitor.Pulse(_gate);
			}
		}

		private void Consume()
		{
			while (true) {
				Node node;
				lock (_gate) {
					while (!_disposed && !_completed && _head == null) Monitor.Wait(_gate);
					if (_disposed || _head == null) return;
					node = _head;
					_head = node.Next;
					if (_head == null) _tail = null;
					node.Next = null;
				}
				try {
					Process(node.Snapshot.Value);
				} catch (Exception error) {
					Logger.Error(error, "Dropping a frame that could not be rendered.");
				} finally {
					lock (_gate) _free.Push(node);
				}
			}
		}

		private void Process(TIn frame)
		{
			lock (_renderGate) {
				if (_disposed) return;
				if (_idleAfter > 0) _stopIdle();
				using (_buffers.Enter()) _render(_processor(FrameBufferPool.CloneForRendering(frame)));
				_lastRenderTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				ScheduleIdle(_idleAfter);
			}
		}

		private void OnIdle(object state)
		{
			lock (_renderGate) {
				if (_disposed) return;
				var elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - _lastRenderTicks) * 1000 / System.Diagnostics.Stopwatch.Frequency;
				if (elapsed < _idleAfter) {
					ScheduleIdle((int)(_idleAfter - elapsed));
				} else {
					_startIdle();
				}
			}
		}

		private void ScheduleIdle(int milliseconds)
		{
			lock (_gate) {
				if (!_disposed) _idleTimer?.Change(milliseconds, Timeout.Infinite);
			}
		}

		public void OnError(Exception error)
		{
			Logger.Error(error, "Frame rendering stopped.");
			Dispose();
		}
		public void OnCompleted()
		{
			lock (_gate) {
				_completed = true;
				Monitor.PulseAll(_gate);
			}
		}
		public void Dispose()
		{
			lock (_gate) {
				_disposed = true;
				_head = _tail = null;
				_idleTimer?.Dispose();
				Monitor.PulseAll(_gate);
			}
			// Do not join: disposal may be requested by a destination on the worker.
			// An in-flight callback owns its workspace until it returns.
		}
		private sealed class Node
		{
			public readonly FrameSnapshot<TIn> Snapshot = new FrameSnapshot<TIn>();
			public Node Next;
		}
	}
}
