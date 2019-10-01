using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using MimeTypes;
using NLog;
using WebSocketSharp;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;
using HttpStatusCode = WebSocketSharp.Net.HttpStatusCode;

namespace LibDmd.Output.Network
{
	public class WebsocketServer : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IResizableDestination
	{
		public string Name { get; } = "Browser Stream";
		public bool IsAvailable { get; } = true;

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		private readonly Dictionary<string, string> _www = new Dictionary<string, string>(); 
		private readonly HttpServer _server;
		private readonly List<DmdBehavior>  _sockets = new List<DmdBehavior>();

		private int _width;
		private int _height;
		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public WebsocketServer(int port)
		{
			// map embedded www resources to _www
			const string prefix = "LibDmd.Output.Network.www.";
			_assembly.GetManifestResourceNames()
				.Where(res => res.StartsWith(prefix))
				.ToList()
				.ForEach(res => _www["/" + res.Substring(prefix.Length)] = res);
			_www["/"] = prefix + "index.html";

			_server = new HttpServer(port);
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
			_server.AddWebSocketService("/dmd", () => {
				var socket = new DmdBehavior(this);
				_sockets.Add(socket);
				return socket;
			});
			_server.Start();
			if (_server.IsListening) {
				Logger.Info("Listening on port {0}, and providing WebSocket services: [ {1} ]", _server.Port, string.Join(", ", _server.WebSocketServices.Paths));
			}
		}
				
		public void Init()
		{
			// nothing to init
		}

		public void Init(DmdBehavior behavior)
		{
			Logger.Debug("Init socket");
			behavior.SendDimensions(_width, _height);
			behavior.SendColor(_color);
			if (_palette != null) {
				behavior.SendPalette(_palette);
			}
		}

		public void RenderGray2(byte[] frame)
		{
			_sockets.ForEach(s => s.SendGray(frame, 2));
		}

		public void RenderGray4(byte[] frame)
		{
			_sockets.ForEach(s => s.SendGray(frame, 4));
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			_sockets.ForEach(s => s.SendColoredGray(frame.Planes, frame.Palette));
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			_sockets.ForEach(s => s.SendColoredGray(frame.Planes, frame.Palette));
		}

		public void RenderRgb24(byte[] frame)
		{
			_sockets.ForEach(s => s.SendRgb24(frame));
		}

		public void SetDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			_sockets.ForEach(s => s.SendDimensions(width, height));
		}

		public void SetColor(Color color)
		{
			_color = color;
			_sockets.ForEach(s => s.SendColor(color));
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			_palette = colors;
			_sockets.ForEach(s => s.SendPalette(colors));
		}

		public void ClearPalette()
		{
			_sockets.ForEach(s => s.SendClearPalette());
		}

		public void ClearColor()
		{
			_sockets.ForEach(s => s.SendClearColor());
		}

		public void Closed(DmdBehavior behavior)
		{
			_sockets.Remove(behavior);
			Logger.Debug("Socket closed");
		}

		public void ClearDisplay()
		{
			_sockets.ForEach(s => s.SendClearDisplay());
		}
		
		public void Dispose()
		{
			_server.Stop();
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
	}

	public class DmdBehavior : WebSocketBehavior
	{
		private readonly WebsocketServer _dest;
		private readonly Message _message = new Message();

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
		private int _width = 128;
		private int _height = 32;

		public DmdBehavior(WebsocketServer dest)
		{
			_dest = dest;
		}

		public void SendRgb24(byte[] frame)
		{
			Send(_message.GetRgb24Frame(frame));
		}

		public void SendDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			Send(_message.GetDimensions(width, height));
			Logger.Info("Sent dimensions to socket.");
		}

		public void SendColor(Color color)
		{
			Send(_message.GetColor(color));
		}

		public void SendPalette(Color[] colors)
		{
			Send(_message.GetPalette(colors));
		}

		public void SendGray(byte[] frame, int bitLength)
		{
			Send(_message.GetGrayFrame(frame, _width, _height, bitLength));
		}

		public void SendColoredGray(byte[][] planes, Color[] palette)
		{
			if (planes.Length == 0) {
				return;
			}
			Send(_message.GetColoredGrayFrame(planes, palette));
		}

		public void SendClearDisplay()
		{
			Send(_message.GetClearDisplay());
		}

		public void SendClearColor()
		{
			Send(_message.GetClearColor());
		}

		public void SendClearPalette()
		{
			Send(_message.GetClearPalette());
		}

		protected override void OnMessage(MessageEventArgs e)
		{
			if (e.RawData.Length == 1 && e.RawData[0] == (byte)MessageType.Init) {
				_dest.Init(this);
			}
			Logger.Info("Got message from client: {0}", e.Data);
		}

		protected override void OnError(ErrorEventArgs e)
		{
			Logger.Error(e.Exception, "Websock error: {0}", e.Message);
			_dest.Closed(this);
		}

		protected override void OnOpen()
		{
			Logger.Info("Websocket opened.");
		}

		protected override void OnClose(CloseEventArgs e)
		{
			_dest.Closed(this);
		}
	}
}
