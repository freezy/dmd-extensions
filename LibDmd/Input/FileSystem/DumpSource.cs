using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibDmd.Frame;

namespace LibDmd.Input.FileSystem
{
	public class DumpSource : AbstractSource, IGray2Source
	{
		public override string Name => "Dump File";
		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly string _filename;

		public DumpSource(string filename)
		{
			_filename = filename;
			if (!File.Exists(_filename)) {
				throw new ArgumentException($"File {_filename} does not exist.");
			}
		}

		public IObservable<DmdFrame> GetGray2Frames(bool dedupe)
		{
			return GetObserver();
		}

		public IObservable<DmdFrame> GetObserver()
		{
			const Int32 BufferSize = 128;
			return Observable.Create<DmdFrame>(async (subject, token) =>
			{
				try {

					using (var fileStream = File.OpenRead(_filename))
					using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize)) {

						var lastTimestamp = 0L;
						var frame = new DmdFrame(128, 32, 2);
						var data = new List<byte>();
						var line = await streamReader.ReadLineAsync();
						while (line != null) {

							var timestamp = long.Parse(line.Substring(2), NumberStyles.HexNumber);
							if (lastTimestamp == 0) {
								lastTimestamp = timestamp;
							}

							line = await streamReader.ReadLineAsync();
							if (line == null) {
								break;
							}

							data.Clear();
							var height = 0;
							int width;
							do {
								data.AddRange(line.ToCharArray().Select(c => (byte)(c - 48)));
								width = line.Length;
								line = await streamReader.ReadLineAsync();
								height++;
							} while (!string.IsNullOrEmpty(line));

							var wait = timestamp - lastTimestamp;
							if (wait < 0) {
								wait = 0;
							}
							await Task.Delay((int)wait, token);
							frame.Update(new Dimensions(width, height), data.ToArray());
							subject.OnNext(frame);

							line = await streamReader.ReadLineAsync();
							lastTimestamp = timestamp;
						}
					}
				}
				catch (Exception ex) {
					subject.OnError(ex);
				}
				finally {
					subject.OnCompleted();
				}
				return Disposable.Empty;

			});
		}
	}
}
