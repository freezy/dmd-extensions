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
		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => true;

		private string _gameName;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private TextWriter _writer;
		private readonly object _writeLock = new object();

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
			lock (_writeLock) {
				_writer?.Dispose();
				_writer = new StreamWriter(File.Open(path, FileMode.Append));
			}

			Logger.Info($"[rawoutput] Dumping frames to {path}.");
		}

		public void RenderGray4(DmdFrame frame) => WriteFrame(frame);

		public void RenderGray2(DmdFrame frame) => WriteFrame(frame);

		private void WriteFrame(DmdFrame frame)
		{
			// Build outside lock so we don't block other work while formatting.
			var sb = new System.Text.StringBuilder(frame.Dimensions.Width * frame.Dimensions.Height + 64);
			sb.Append("0x").AppendLine(Environment.TickCount.ToString("X8"));

			var idx = 0;
			for (var j = 0; j < frame.Dimensions.Height; j++) {
				for (var i = 0; i < frame.Dimensions.Width; i++) {
					sb.Append(frame.Data[idx++].ToString("X"));
				}
				sb.Append("\r\n");
			}
			sb.Append("\r\n");

			lock (_writeLock) {
				if (_writer == null) {
					return;
				}
				_writer.Write(sb.ToString());
				_writer.Flush();
			}
		}

		public void SetDimensions(Dimensions newDimensions) { }
		public void ClearDisplay() { }

		public void Dispose()
		{
			lock (_writeLock) {
				_writer?.Dispose();
			}
		}
	}
}