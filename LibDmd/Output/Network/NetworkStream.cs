using System;
using System.Windows.Media;
using NLog;
using WebSocketSharp;

namespace LibDmd.Output.Network
{
	public class NetworkStream : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IResizableDestination
	{
		public string Name { get; } = "Network Stream";
		public bool IsAvailable { get; private set; } = false;

		private WebSocket _client;
		private readonly Uri _uri;
		private readonly WebsocketSerializer _serializer = new WebsocketSerializer();
		private readonly string _gameName;

		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public NetworkStream(Uri uri, string romName = null)
		{
			_uri = uri;
			_gameName = romName;
			Init();
		}

		public void Init()
		{
			Logger.Info("Connecting to Websocket at {0}", _uri.ToString());
			_client = new WebSocket(_uri.ToString());
			_client.OnMessage += OnMessage;
			_client.OnError += OnError;
			_client.OnOpen += OnOpen;
			_client.Connect();
		}

		private void OnOpen(object sender, EventArgs e)
		{
			IsAvailable = true;
			Logger.Info("Connected to Websocket at {0}", _uri.ToString());

			if (_gameName != null) {
				_client.Send(_serializer.SerializeGameName(_gameName));
			}
			_client.Send(_serializer.SerializeDimensions(_serializer.Width, _serializer.Height));
			_client.Send(_serializer.SerializeColor(_color));
			if (_palette != null) {
				_client.Send(_serializer.SerializePalette(_palette));
			}
		}

		private void OnMessage(object sender, MessageEventArgs e)
		{
			Logger.Info("Message from server: " + e.Data);
		}

		private void OnError(object sender, ErrorEventArgs e)
		{
			Logger.Error("Network stream disconnected: " + e.Message);
			IsAvailable = false;
		}

		private void SendGray(byte[] frame, int bitlength)
		{
			if (frame.Length < _serializer.Width * _serializer.Height) {
				Logger.Info("SendGray: invalid frame received frame.length={0} bitlength={1} width={2} height={3}", frame.Length, bitlength, _serializer.Width, _serializer.Height);
				return;
			}
			_client?.Send(_serializer.SerializeGray(frame, bitlength));
		}

		public void RenderGray2(byte[] frame)
		{
			SendGray(frame, 2);
		}

		public void RenderGray4(byte[] frame)
		{
			SendGray(frame, 4);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			_client?.Send(_serializer.SerializeColoredGray2(frame.Planes, frame.Palette));
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			_client?.Send(_serializer.SerializeColoredGray4(frame.Planes, frame.Palette));
		}

		public void RenderRgb24(byte[] frame)
		{
			_client?.Send(_serializer.SerializeRgb24(frame));
		}

		public void SetDimensions(int width, int height)
		{
			_client?.Send(_serializer.SerializeDimensions(width, height));
		}

		public void SetColor(Color color)
		{
			_color = color;
			_client?.Send(_serializer.SerializeColor(color));
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			_palette = colors;
			_client?.Send(_serializer.SerializePalette(colors));
		}

		public void ClearPalette()
		{
			_client?.Send(_serializer.SerializeClearPalette());
		}

		public void ClearColor()
		{
			_client?.Send(_serializer.SerializeClearColor());
		}

		public void ClearDisplay()
		{
			// ignore
		}
		
		public void Dispose()
		{
			((IDisposable)_client)?.Dispose();
		}

	}

}
