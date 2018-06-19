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
	public class BrowserStream : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IResizableDestination
	{
		public string Name { get; } = "Browser Stream";
		public bool IsAvailable { get; } = true;

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		private readonly Dictionary<string, string> _www = new Dictionary<string, string>(); 
		private readonly HttpServer _server;
		private readonly List<DmdSocket>  _sockets = new List<DmdSocket>();

		private int _width;
		private int _height;
		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public BrowserStream(int port)
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
				var socket = new DmdSocket(this);
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

		public void Init(DmdSocket socket)
		{
			Logger.Debug("Init socket");
			socket.SendDimensions(_width, _height);
			socket.SendColor(_color);
			if (_palette != null) {
				socket.SendPalette(_palette);
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
			_sockets.ForEach(s => s.SendColoredGray("coloredGray2", frame.Planes, frame.Palette));
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			_sockets.ForEach(s => s.SendColoredGray("coloredGray4", frame.Planes, frame.Palette));
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

		public void Closed(DmdSocket socket)
		{
			_sockets.Remove(socket);
			Logger.Debug("Socket closed");
		}

		public void ClearDisplay()
		{
			// ignore
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

	public class DmdSocket : WebSocketBehavior
	{
		private readonly BrowserStream _dest;
		private readonly long _startedAt = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
		private int _width = 128;
		private int _height = 32;

		public DmdSocket(BrowserStream dest)
		{
			_dest = dest;
		}

		public void SendGray(byte[] frame, int bitlength)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var data = Encoding.ASCII
				.GetBytes("gray" + bitlength + "Planes")
				.Concat(new byte[] {0x0})
				.Concat(BitConverter.GetBytes((uint) (timestamp - _startedAt)))
				.Concat(FrameUtil.Split(_width, _height, bitlength, frame).SelectMany(p => p));

			Send(data.ToArray());
		}

		public void SendColoredGray(string name, byte[][] planes, Color[] palette)
		{
			if (planes.Length == 0) {
				return;
			}
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var data = Encoding.ASCII
				.GetBytes(name)
				.Concat(new byte[] {0x0})
				.Concat(BitConverter.GetBytes((uint) (timestamp - _startedAt)))
				.Concat(BitConverter.GetBytes(palette.Length))
				.Concat(ColorUtil.ToIntArray(palette).SelectMany(BitConverter.GetBytes))
				.Concat(planes.SelectMany(p => p));
			Send(data.ToArray());
		}

		public void SendRgb24(byte[] frame)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var data = Encoding.ASCII
				.GetBytes("rgb24")
				.Concat(new byte[] {0x0})
				.Concat(BitConverter.GetBytes((uint) (timestamp - _startedAt)))
				.Concat(frame);
			Send(data.ToArray());
		}

		public void SendDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			var data = Encoding.ASCII
				.GetBytes("dimensions")
				.Concat(new byte[] {0x0})
				.Concat(BitConverter.GetBytes(width))
				.Concat(BitConverter.GetBytes(height));
			Send(data.ToArray());
			Logger.Info("Sent dimensions to socket.");
		}

		public void SendColor(Color color)
		{
			var data = Encoding.ASCII
				.GetBytes("color")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes(ColorUtil.ToInt(color)));
			Send(data.ToArray());
		}

		public void SendPalette(Color[] colors)
		{
			var data = Encoding.ASCII
				.GetBytes("palette")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes(colors.Length))
				.Concat(ColorUtil.ToIntArray(colors).SelectMany(BitConverter.GetBytes));
			Send(data.ToArray());
		}

		public void SendClearColor()
		{
			var data = Encoding.ASCII
				.GetBytes("clearColor")
				.Concat(new byte[] {0x0});
			Send(data.ToArray());
		}

		public void SendClearPalette()
		{
			var data = Encoding.ASCII
				.GetBytes("clearPalette")
				.Concat(new byte[] { 0x0 });
			Send(data.ToArray());
		}

		protected override void OnMessage(MessageEventArgs e)
		{
			if (e.Data == "init") {
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
