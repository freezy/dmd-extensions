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
using System.Threading.Tasks;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Input.FileSystem
{
	public class DumpSource : AbstractSource, IGray2Source, IGameNameSource
	{
		public override string Name => "Dump File";
		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		public IObservable<string> GetGameName() => _gameName;

		private readonly string _filename;

		private readonly Subject<string> _gameName = new Subject<string>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public DumpSource(string filename)
		{
			_filename = filename;
			if (!File.Exists(_filename)) {
				throw new ArgumentException($"File {_filename} does not exist.");
			}
		}

		public IObservable<DmdFrame> GetGray2Frames(bool dedupe, bool skipIdentificationFrames)
		{
			const Int32 bufferSize = 128;
			_gameName.OnNext(Path.GetFileNameWithoutExtension(_filename).TrimEnd('-'));
			return Observable.Create<DmdFrame>(async (subject, token) =>
			{
				try {

					using (var fileStream = File.OpenRead(_filename))
					using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, bufferSize)) {

						Logger.Info($"[dump] Starting to stream frames from {_filename}");
						var lastTimestamp = 0L;
						var frame = new DmdFrame(128, 32, 2);
						var data = new List<byte>();
						var line = await streamReader.ReadLineAsync();
						while (line != null) {

							if (string.IsNullOrWhiteSpace(line)) {
								line = await streamReader.ReadLineAsync();
								continue;
							}

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
							if (wait < 0 || wait > 2000) {
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
