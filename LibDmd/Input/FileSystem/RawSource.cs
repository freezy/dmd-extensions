using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input.FileSystem
{
	public class RawSource : IRawSource
	{
		public string Name { get; } = "Binary File";

		private readonly string _filename;

		public RawSource(string filename)
		{
			_filename = filename;
		}

		public IObservable<byte[]> GetRawdata()
		{
			return new FileStream(_filename, FileMode.Open, FileAccess.Read).ToObservable(4096);
		}
	}

	public static class Ext
	{
		public static IObservable<byte[]> ToObservable(this Stream stream, int bufferSize)
		{
			// to hold read data
			var buffer = new byte[bufferSize];
			// Step 1: async signature => observable factory
			var asyncRead = Observable.FromAsyncPattern<byte[], int, int, int>(stream.BeginRead, stream.EndRead);
			return Observable.While(
				// while there is data to be read
				() => stream.CanRead,
				// iteratively invoke the observable factory, which will
				// "recreate" it such that it will start from the current
				// stream position - hence "0" for offset
				Observable.Defer(() => asyncRead(buffer, 0, bufferSize))
					.Select(readBytes => buffer.Take(readBytes).ToArray()));
		}
	}
}
