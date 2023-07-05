using System;
using System.IO;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.FileOutput
{
	public class RawOutput : IGray2Destination, IGray4Destination, IResizableDestination
	{
		public string Name => "Raw Output";
		public bool IsAvailable => !string.IsNullOrEmpty(_gameName);

		private string _gameName;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private TextWriter _writer;

		public RawOutput()
		{
			Logger.Info($"[rawoutput] Waiting for game name...");
		}

		public RawOutput(string gameName)
		{
			SetGameName(gameName);
		}

		public void SetGameName(string gameName)
		{
			_gameName = gameName;
			var folder = PathUtil.GetVpmFolder("dmddump", "[rawoutput]");
			if (folder == null) {
				folder = Path.Combine(Directory.GetCurrentDirectory(), "dmddump");
				if (!Directory.Exists(folder)) {
					Directory.CreateDirectory(folder);
				}
			}
			var path = Path.Combine(folder, $"{_gameName}.txt");
			_writer?.Dispose();
			_writer = new StreamWriter(File.Open(path, FileMode.Append));
			Logger.Info($"[rawoutput] Dumping frames to {path}.");
		}

		public void RenderGray4(DmdFrame frame) => WriteFrame(frame);

		public void RenderGray2(DmdFrame frame) => WriteFrame(frame);

		private void WriteFrame(DmdFrame frame)
		{
			if (_writer == null) {
				return;
			}

			const string nl = "\r\n";
			_writer.Write("0x");
			_writer.WriteLine(Environment.TickCount.ToString("X8"));
			int idx = 0;
			for (int j = 0; j < frame.Dimensions.Height; j++) {
				for (int i = 0; i < frame.Dimensions.Width; i++) {
					_writer.Write(frame.Data[idx++].ToString("X"));
				}
				_writer.Write(nl);
			}
			_writer.Write(nl);
			_writer.Flush();
		}

		public void SetDimensions(Dimensions newDimensions) { }
		public void ClearDisplay() { }

		public void Dispose()
		{
			_writer?.Dispose();
		}
	}
}
