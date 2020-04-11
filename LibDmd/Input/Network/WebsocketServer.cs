using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Output;
using LibDmd.Output.Network;
using NLog;
using WebSocketSharp;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;
using HttpStatusCode = WebSocketSharp.Net.HttpStatusCode;

namespace LibDmd.Input.Network
{
	public class WebsocketServer : ISocketAction
	{
		internal WebsocketGray2Source Gray2Source = new WebsocketGray2Source();

		private readonly HttpServer _server;
		private readonly List<DmdSocket> _sockets = new List<DmdSocket>();

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		private const string Html = "<!DOCTYPE html><html><head><title>DmdExt Websocket Server</title></head><body><center style=\"margin-top=50px\"><h1>DmdExt Websocket Server</h1><p>Nothing to see here. Send frames to {ws_url} and you'll see them on your display.</p></center></body></html>";

		public WebsocketServer(string ip, int port, string path) {
			
			Logger.Info("Starting server at http://{0}:{1}{2}...", ip, port, path);
			_server = new HttpServer(IPAddress.Parse(ip), port);
			var html = Html.Replace("{ws_url}", ip + path);
			_server.OnGet += (sender, e) => {
				var res = e.Response;
				var data = Encoding.UTF8.GetBytes(html);
				res.StatusCode = (int)HttpStatusCode.OK;
				res.ContentType = "text/html";
				res.ContentEncoding = Encoding.UTF8;
				res.ContentLength64 = data.Length;
				res.OutputStream.Write(data, 0, data.Length);
			};
			_server.AddWebSocketService(path, () => {
				var socket = new DmdSocket(this);
				_sockets.Add(socket);
				return socket;
			});
			_server.Start();
			if (_server.IsListening) {
				Logger.Info("Server listening, connect to ws://{0}:{1}{2}...", ip, port, path);
			}
		}

		public void SetupGraphs(RenderGraphCollection graphs, List<IDestination> renderers)
		{
			graphs.Add(new RenderGraph {
				Name = "2-bit Websocket Graph",
				Source = Gray2Source,
				Destinations = renderers,
			});
		}

		public void Dispose()
		{
			_server.Stop();
		}

		internal void Closed(DmdSocket socket)
		{
			_sockets.Remove(socket);
			Logger.Debug("Socket {0} closed", socket.ID);
		}

		public void OnColor(Color color)
		{
			Logger.Info("OnColor {0}", color);
		}

		public void OnPalette(Color[] palette)
		{
			Logger.Info("OnPalette {0}", palette);
		}

		public void OnClearColor()
		{
			Logger.Info("OnClearColor");
		}

		public void OnClearPalette()
		{
			Logger.Info("OnClearPalette");
		}

		public void OnDimensions(int width, int height)
		{
			Logger.Info("OnDimensions: {0}x{1}", width, height);
		}
	}

	public class DmdSocket : WebSocketBehavior
	{
		private readonly WebsocketServer _src;
		private readonly WebsocketSerializer _serializer = new WebsocketSerializer();

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public DmdSocket(WebsocketServer src) {
			_src = src;
		}
	
		protected override void OnMessage(MessageEventArgs e)
		{
			_serializer.Unserialize(e.RawData, _src);
		}

		protected override void OnClose(CloseEventArgs e)
		{
			_src.Closed(this);
		}

		protected override void OnError(ErrorEventArgs e)
		{
			Logger.Error(e.Exception, "Websock error: {0}", e.Message);
			_src.Closed(this);
		}

		protected override void OnOpen()
		{
			Logger.Info("Websocket opened.");
		}
	}
}
