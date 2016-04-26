using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Input;
using LibDmd.Output;
using LibDmd.Processor;

namespace LibDmd.Common
{
	public class Profiler
	{
		private readonly ProfilerFrameBuffer _frames = new ProfilerFrameBuffer(100);

		public void AddFrame(ProfilerFrame frame)
		{
			_frames.Add(frame);
		}
	}

	public class ProfilerFrame
	{
		private readonly Stopwatch _sw;
		private long _last;
		private readonly List<ProfilerEntry> _data = new List<ProfilerEntry>();

		public ProfilerFrame()
		{
			_sw = new Stopwatch();
			_sw.Start();
			_last = 0;
		}

		public IFrameSource Next(IFrameSource src)
		{
			Add(src.Name);
			return src;
		}

		public AbstractProcessor Next(AbstractProcessor proc)
		{
			Add(proc.Name);
			return proc;
		}

		public IFrameDestination Next(IFrameDestination dest)
		{
			Add(dest.Name);
			return dest;
		}

		public void Next()
		{
			_sw.Stop();
			_last = _sw.ElapsedTicks;
		}

		private void Add(string name)
		{
			_data.Add(new ProfilerEntry(name, _sw.ElapsedTicks - _last));
			_last = _sw.ElapsedTicks;
		}

		public override string ToString()
		{
			var us = 1000000d/Stopwatch.Frequency;
			var frameLength = _last * us;
			var sb = new StringBuilder();
			sb.AppendFormat("Total: {0}μs ", Math.Round(frameLength));
			foreach (var entry in _data)
			{
				sb.AppendFormat("| {0}: {1}μs ", entry.Name, Math.Round(entry.Ticks * us));
			}
			sb.Append("|");
			return sb.ToString();
		}
	}

	public class ProfilerEntry
	{
		public readonly string Name;
		public readonly long Ticks;

		public ProfilerEntry(string name, long ticks)
		{
			Name = name;
			Ticks = ticks;
		}
	}

	public class ProfilerFrameBuffer : Queue<ProfilerFrame>
	{
		private readonly int _size;
		private readonly object _syncRoot = new object();

		public ProfilerFrameBuffer(int size)
		{
			_size = size;
		}

		public void Add(ProfilerFrame item)
		{
			lock (_syncRoot) {
				Enqueue(item);
				if (Count > _size) {
					Dequeue();
				}

			}
		}
	}
}
