using NLog;
using System;
using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using WebSocketSharp;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;
using Logger = NLog.Logger;

namespace LibDmd.Output.Network
{
	public class WebsocketClient : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IResizableDestination
	{
		public string Name { get; } = "Websocket Client";
		public bool IsAvailable { get; } = true;

		public string ApiKey { get; set; }
		public string EndPoint { get; set; } = "wss://api.vpdb.io/v1/dmd";
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }

		private WebSocket _ws;
		private bool _connected;
		private bool _streaming;

		private int _width;
		private int _height;
		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;

		private readonly Message _message = new Message();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void Init()
		{
			_ws = new WebSocket(EndPoint);
			_ws.OnOpen += OnSocketOpen;
			_ws.OnMessage += OnSocketMessage;
			_ws.OnError += OnSocketError;
			_ws.OnClose += OnSocketClose;

			Logger.Info($"Connecting to {EndPoint}...");
			_ws.Connect();
		}

		private void OnSocketOpen(object sender, EventArgs e)
		{
			_connected = true;
			Logger.Info($"Connected to {EndPoint}.");
			//_socket.Emit("produce", Welcome);
		}

		private void OnSocketMessage(object sender, MessageEventArgs e)
		{
			if (e.RawData.Length == 1 && e.RawData[0] == (byte)MessageType.Init) {
				SetDimensions(_width, _height);
				SetColor(_color);
				if (_palette != null) {
					SetPalette(_palette);
				}
			}
			/*
			_socket.On("resume", () => {
				_streaming = true;
				Logger.Info("Resuming streaming frames...");
			});
			_socket.On("pause", () => {
				_streaming = false;
				Logger.Info("Pausing streaming frames...");
			});*/


			Logger.Info("Got message from client: {0}", e.Data);

			//e.RawData;
		}

		private void OnSocketError(object sender, ErrorEventArgs e)
		{
			Logger.Warn("Streaming error: {0}", e.Message);
		}

		private void OnSocketClose(object sender, CloseEventArgs e)
		{
			_connected = false;
			Logger.Info($"Disconnected from {EndPoint}.");
		}

		public void SetDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			_ws.Send(_message.GetDimensions(width, height));
		}

		public void RenderGray2(byte[] frame)
		{
			_ws.Send(_message.GetGrayFrame(frame, _width, _height, 2));
		}

		public void RenderGray4(byte[] frame)
		{
			_ws.Send(_message.GetGrayFrame(frame, _width, _height, 4));
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			if (frame.Planes.Length == 0) {
				return;
			}
			_ws.Send(_message.GetColoredGrayFrame(frame.Planes, frame.Palette));
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			if (frame.Planes.Length == 0) {
				return;
			}
			_ws.Send(_message.GetColoredGrayFrame(frame.Planes, frame.Palette));
		}

		public void RenderRgb24(byte[] frame)
		{
			_ws.Send(_message.GetRgb24Frame(frame));
		}

		public void SetColor(Color color)
		{
			_color = color;
			_ws.Send(_message.GetColor(color));
		}

		public void SetPalette(Color[] colors , int index = -1)
		{
			_palette = colors;
			_ws.Send(_message.GetPalette(colors));
		}

		public void ClearPalette()
		{
			_ws.Send(_message.GetClearPalette());
		}

		public void ClearColor()
		{
			_ws.Send(_message.GetClearColor());
		}

		public void ClearDisplay()
		{
			_ws.Send(_message.GetClearDisplay());
		}

		public void Dispose()
		{
			_ws?.Close();
			/*_socket?.Emit("stop");
			_socket?.Close();*/
		}

		public static byte[] Compress(byte[] raw)
		{
			using (var memory = new MemoryStream()) {
				using (var gzip = new GZipStream(memory, CompressionMode.Compress, true)) { gzip.Write(raw, 0, raw.Length);
				}
				return memory.ToArray();
			}
		}
	}
}
