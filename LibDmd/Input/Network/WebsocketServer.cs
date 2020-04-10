using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace LibDmd.Input.Network
{
	public class WebsocketServer
	{
		private readonly WebSocketServer _server;
		private readonly List<DmdSocket> _sockets = new List<DmdSocket>();

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public WebsocketServer(string host, string path) {
			_server = new WebSocketServer(host);
			_server.AddWebSocketService(path, () => {
				var socket = new DmdSocket(this);
				_sockets.Add(socket);
				return socket;
			});
			_server.Start();
			if (_server.IsListening) {
				Logger.Info("Server listening at {0}{1}...", host, path);
			}
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
	}

	public class DmdSocket : WebSocketBehavior {
		
		private readonly WebsocketServer _src;

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public DmdSocket(WebsocketServer src) {
			_src = src;
		}
	
		protected override void OnMessage(MessageEventArgs e)
		{
			
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
