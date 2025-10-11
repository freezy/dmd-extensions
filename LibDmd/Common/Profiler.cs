using System;
#if DEBUG
using System.Collections.Generic;
using NLog;
#endif

namespace LibDmd.Common
{
	public static class Profiler
	{
#if DEBUG

		private static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
		private static List<Profile> RootProfiles { get; } = new List<Profile>();
		private static Profile _parent;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
#endif
		public static IDisposable Start(string key)
		{
#if DEBUG
			lock (Profiles) {
				if (Profiles.TryGetValue(key, out var profile)) {
					profile.Start();
				} else {
					_parent = new Profile(key, _parent);
					Profiles.Add(key, _parent);
					if (_parent.Parent == null) {
						RootProfiles.Add(_parent);
					}
				}
			}
			return new ProfilerSpan(key);
#else
			return null;
#endif
		}

		public static void Stop(string key)
		{
#if DEBUG
			Profiles[key].Stop();
			_parent = Profiles[key].Parent;
#endif
		}

		public static void Print()
		{
#if DEBUG
			Logger.Debug("Profiling data:");
			RootProfiles.ForEach(p => p.Print());
#endif
		}

		public static void Reset()
		{
#if DEBUG
			Profiles.Clear();
			RootProfiles.Clear();
			_parent = null;
#endif
		}
	}

#if DEBUG
	public readonly struct ProfilerSpan : IDisposable
	{
		private readonly string _key;
		public ProfilerSpan(string key)
		{
			_key = key;
		}
		public void Dispose()
		{
			Profiler.Stop(_key);
		}
	}

	class Profile
	{
		public string Name { get; }
		public Profile Parent { get; }
		public List<Profile> Children { get; } = new List<Profile>();
		public bool IsRunning;

		private readonly int _level;
		private long _started;
		private long _totalTicks;
		private long _count;
		private long _minTicks;
		private long _maxTicks;
		private int _drops;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Profile(string name, Profile parent)
		{
			Name = name;
			Parent = parent;
			_level = parent?._level + 1 ?? 0;
			parent?.Children.Add(this);
			Start();
		}

		public void Start()
		{
			if (IsRunning) {
				_drops++;
				// don't measure if caused drop
				IsRunning = false;
				return;
			}
			_started = DateTime.Now.Ticks;
			IsRunning = true;
		}

		public void Stop()
		{
			if (!IsRunning) {
				return;
			}
			Children.ForEach(c => c.Stop());

			var ticks = DateTime.Now.Ticks - _started;
			_totalTicks += ticks;
			_count++;
			_maxTicks = Math.Max(_maxTicks, ticks);
			_minTicks = _minTicks == 0 ? ticks : Math.Min(_minTicks, ticks);
			IsRunning = false;
		}

		public void Print()
		{
			Logger.Debug(this);
			Children.ForEach(c => c.Print());
		}

		public override string ToString()
		{ 
			const int precision = 1000;
			var avgMs = Math.Round((double)_totalTicks / _count / TimeSpan.TicksPerMillisecond * precision) / precision;
			var minMs = Math.Round((double)_minTicks / TimeSpan.TicksPerMillisecond * precision) / precision;
			var maxMs = Math.Round((double)_maxTicks / TimeSpan.TicksPerMillisecond * precision) / precision;
			var str = $"{Name}: {avgMs}ms ({minMs}/{maxMs}), {_count}x, {_drops} drop(s)";
			return str.PadLeft(str.Length + _level * 3);
		}
	}
#endif
}