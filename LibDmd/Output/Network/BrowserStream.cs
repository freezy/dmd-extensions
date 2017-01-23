using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using MimeTypes;
using NLog;
using WebSocketSharp.Server;

namespace LibDmd.Output.Network
{
	public class BrowserStream : WebSocketBehavior, IGray2Destination, IResizableDestination
	{
		public string Name { get; } = "Browser Stream";
		public bool IsAvailable { get; } = true;

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		private readonly Dictionary<string, string> _www = new Dictionary<string, string>(); 
		private readonly HttpServer _server;

		private int _width;
		private int _height;
		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;
		private readonly long _startedAt = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static readonly byte Dimensions = 0x1;
		public static readonly byte Gray2Planes = 0x10;

		public BrowserStream()
		{
			// map embedded www resources to _www
			const string prefix = "LibDmd.Output.Network.www.";
			Logger.Info("Resource names = {0}", string.Join(", ", _assembly.GetManifestResourceNames())); // LibDmd.Output.Network.www.index.html
			_assembly.GetManifestResourceNames()
				.Where(res => res.StartsWith(prefix))
				.ToList()
				.ForEach(res => _www["/" + res.Substring(prefix.Length)] = res);
			_www["/"] = prefix + "index.html";
			
			Logger.Debug("Allowed Paths: {0}", string.Join(", ", _www.Keys));
				
			_server = new HttpServer(9090);
			_server.OnGet += (sender, e) => {

				var req = e.Request;
				var res = e.Response;

				var path = req.RawUrl;
				var output = res.OutputStream;

				if (_www.ContainsKey(path)) {
					res.StatusCode = (int)HttpStatusCode.OK;
					res.ContentType = GetMimeType(Path.GetExtension(path));
					res.ContentEncoding = Encoding.UTF8;
					using (var input = _assembly.GetManifestResourceStream(_www[path])) {
						res.ContentLength64 = input.Length;
						CopyStream(input, output);
					}
				} else {
					Logger.Warn("Path {0} not found in assembly.", path);
					res.StatusCode = (int)HttpStatusCode.NotFound;
				}
			};
			_server.Start();
			if (_server.IsListening) {
				Logger.Info("Listening on port {0}, and providing WebSocket services:", _server.Port);
				foreach (var path in _server.WebSocketServices.Paths) {
					Logger.Info ("- {0}", path);
				}
			}
		}

		public void RenderGray2(byte[] frame)
		{
			//EmitTimestampedData(Gray2Planes, frame.Length / 4, (data, offset) => FrameUtil.Copy(FrameUtil.Split(_width, _height, 2, frame), data, offset));
		}

		
		public void SetDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			var data = new[] { Dimensions }.Concat(BitConverter.GetBytes(width)).Concat(BitConverter.GetBytes(height));
			Send(data.ToArray());
			Logger.Info("Sent dimensions to socket.");
		}
		
		/// <summary>
		/// Adds a timestamp to a byte array and sends it to the socket.
		/// </summary>
		/// <param name="eventType">Name of the event</param>
		/// <param name="dataLength">Length of the payload to send (without time stamp)</param>
		/// <param name="copy">Function that copies data to the provided array. Input: array with 8 bytes of timestamp and dataLength bytes to write</param>
		private void EmitTimestampedData(byte eventType, int dataLength, Action<byte[], int> copy)
		{
			try {
				var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
				var data = new byte[dataLength + 9];
				data[0] = eventType;
				Buffer.BlockCopy(BitConverter.GetBytes(timestamp - _startedAt), 0, data, 0, 9);
				copy(data, 8);
				Send(data);

			} catch (Exception e) {
				Logger.Error(e, "Error sending event " + eventType + " to socket.");
			}
		}

		private static string GetMimeType(string ext)
		{
			return string.IsNullOrEmpty(ext) ? "text/html" : MimeTypeMap.GetMimeType(ext);
		}

		private static void CopyStream(Stream input, Stream output)
		{
			// Insert null checking here for production
			var buffer = new byte[8192];

			int bytesRead;
			while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0) {
				output.Write(buffer, 0, bytesRead);
			}
		}

		public void Dispose()
		{
			_server.Stop();
		}
		
		public void Init()
		{
			// no init
		}
	}
}
