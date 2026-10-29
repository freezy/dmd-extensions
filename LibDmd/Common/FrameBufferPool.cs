using System;
using System.Collections.Generic;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Common
{
	/// <summary>Owned snapshot storage, cached by size so switching known formats does not allocate.</summary>
	internal struct RetainedBuffer<T>
	{
		private T[] _current;
		private Dictionary<int, T[]> _sizes;
		public T[] Get(int length)
		{
			if (_current != null && _current.Length == length) return _current;
			if (_sizes == null) _sizes = new Dictionary<int, T[]>();
			if (!_sizes.TryGetValue(length, out _current)) {
				_current = new T[length];
				_sizes.Add(length, _current);
			}
			return _current;
		}
	}

	/// <summary>
	/// Scratch storage for an explicitly borrowed-frame callback. A scope owns its
	/// workspace until all synchronous consumers return. Nested callbacks and
	/// concurrent producers rent separate workspaces; no in-flight buffer is reused.
	/// Outside a scope, allocation helpers retain their usual owning semantics.
	/// </summary>
	internal sealed class FrameBufferPool
	{
		[ThreadStatic] private static Workspace _current;
		private readonly object _gate = new object();
		private readonly Stack<Workspace> _available = new Stack<Workspace>();

		// Owning subscribers must not inherit a borrowed producer's scratch storage.
		public static Scope Suspend()
		{
			var previous = _current;
			_current = null;
			return new Scope(null, null, previous);
		}

		public Scope Enter()
		{
			Workspace workspace;
			lock (_gate) workspace = _available.Count == 0 ? new Workspace() : _available.Pop();
			workspace.Reset();
			var previous = _current;
			_current = workspace;
			return new Scope(this, workspace, previous);
		}

		internal readonly struct Scope : IDisposable
		{
			private readonly FrameBufferPool _pool;
			private readonly Workspace _workspace;
			private readonly Workspace _previous;
			internal Scope(FrameBufferPool pool, Workspace workspace, Workspace previous)
			{
				_pool = pool;
				_workspace = workspace;
				_previous = previous;
			}
			public void Dispose()
			{
				_current = _previous;
				if (_pool != null) lock (_pool._gate) _pool._available.Push(_workspace);
			}
		}

		internal sealed class Workspace
		{
			private readonly Dictionary<(Type, int), Bucket> _buckets = new Dictionary<(Type, int), Bucket>();
			private readonly List<DmdFrame> _frames = new List<DmdFrame>();
			private readonly List<ColoredFrame> _coloredFrames = new List<ColoredFrame>();
			private readonly List<FrameEvent> _events = new List<FrameEvent>();
			private readonly List<AlphaNumericFrame> _alphaFrames = new List<AlphaNumericFrame>();
			private int _alphaIndex;
			private int _eventIndex;
			private int _frameIndex, _coloredFrameIndex;

			public void Reset()
			{
				foreach (var bucket in _buckets.Values) bucket.Used = 0;
				_frameIndex = _coloredFrameIndex = 0;
				_eventIndex = 0;
				_alphaIndex = 0;
			}
			public T[] Array<T>(int length)
			{
				var key = (typeof(T), length);
				if (!_buckets.TryGetValue(key, out var bucket)) {
					bucket = new Bucket();
					_buckets.Add(key, bucket);
				}
				if (bucket.Used == bucket.Arrays.Count) bucket.Arrays.Add(new T[length]);
				var array = (T[])bucket.Arrays[bucket.Used++];
				System.Array.Clear(array, 0, array.Length);
				return array;
			}
			public DmdFrame Frame(Dimensions dimensions, byte[] data, int bits)
			{
				if (_frameIndex == _frames.Count) _frames.Add(new DmdFrame());
				var frame = _frames[_frameIndex++].Update(dimensions, data, bits);
				frame.IsBorrowed = true;
				return frame;
			}
			public ColoredFrame Colored(Dimensions dimensions, byte[] data, Color[] palette)
			{
				if (_coloredFrameIndex == _coloredFrames.Count) _coloredFrames.Add(new ColoredFrame());
				var frame = _coloredFrames[_coloredFrameIndex++];
				frame.IsBorrowed = true;
				frame.Update(dimensions, data, palette.Length.GetBitLength());
				return frame.Update(data, palette);
			}
			private sealed class Bucket
			{
				public int Used;
				public readonly List<Array> Arrays = new List<Array>();
			}
			public FrameEvent Event(ushort id)
			{
				if (_eventIndex == _events.Count) _events.Add(new FrameEvent());
				return _events[_eventIndex++].Update(id);
			}
			public AlphaNumericFrame Alpha(AlphaNumericFrame source)
			{
				if (_alphaIndex == _alphaFrames.Count) _alphaFrames.Add(new AlphaNumericFrame());
				var frame = _alphaFrames[_alphaIndex++];
				frame.Update(source);
				return frame;
			}
		}

		public static T[] Allocate<T>(int length) => _current == null ? new T[length] : _current.Array<T>(length);
		public static DmdFrame Create(Dimensions dimensions, byte[] data, int bits)
			=> _current == null ? new DmdFrame(dimensions, data, bits) : _current.Frame(dimensions, data, bits);
		public static ColoredFrame Create(Dimensions dimensions, byte[] data, Color[] palette)
			=> _current == null ? new ColoredFrame(dimensions, data, palette) : _current.Colored(dimensions, data, palette);

		public static T CloneForRendering<T>(T frame) where T : class, ICloneable
		{
			// Copy the mutable wrapper, but share read-only source pixels/palettes. Processors
			// must replace Data when transforming it, never write into an input buffer.
			// Each Allocate call gets distinct storage until the whole callback returns.
			if (_current == null) return (T)frame.Clone();
			if (frame is Color[] palette) {
				var copy = Allocate<Color>(palette.Length);
				Array.Copy(palette, copy, palette.Length);
				return (T)(object)copy;
			}
			if (frame is FrameEvent frameEvent) return (T)(object)_current.Event(frameEvent.EventId);
			if (frame is FrameEventInit) return frame;
			if (frame is AlphaNumericFrame alpha) return (T)(object)_current.Alpha(alpha);
			if (frame is ColoredFrame colored) {
				var copy = Create(colored.Dimensions, colored.Data, colored.Palette);
				copy.Update(colored.Dimensions, colored.Data, colored.BitLength, colored.IsIdentifyFrame);
				return (T)(object)copy;
			}
			if (frame is DmdFrame dmd) {
				return (T)(object)Create(dmd.Dimensions, dmd.Data, dmd.BitLength).Update(dmd);
			}
			return (T)frame.Clone();
		}
	}
}
