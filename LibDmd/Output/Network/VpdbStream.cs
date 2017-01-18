using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using NLog;
using Quobject.SocketIoClientDotNet.Client;

namespace LibDmd.Output.Network
{
	public class VpdbStream : IGray2Destination, IColoredGray2Destination, IResizableDestination
	{
		public string Name { get; } = "VPDB Stream";
		public bool IsAvailable { get; } = true;

		public string ApiKey { get; set; }
		public string EndPoint { get; set; } = "https://api-test.vpdb.io/";
		//public string EndPoint { get; set; } = "http://127.0.0.1:3000/";
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }

		private Socket _socket;
		private bool _connected;
		private int _width;
		private int _height;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VpdbStream()
		{
			Logger.Info("Connecting to VPDB...");
			_socket = IO.Socket(EndPoint);
			_socket.On(Socket.EVENT_CONNECT, () => {
				_connected = true;
				Logger.Info("Connected to VPDB.");
				_socket.Emit("produce", new JObject { { "width", _width }, { "height", _height } });
			});
			_socket.On(Socket.EVENT_RECONNECT, () => {
				_connected = true;
				Logger.Info("Reconnected to VPDB.");
				_socket.Emit("produce", new JObject { { "width", _width }, { "height", _height } });
			});
			_socket.On(Socket.EVENT_DISCONNECT, () => {
				_connected = false;
				Logger.Info("Disconnected from VPDB.");
			});
		}

		public void Init()
		{
		}

		public void SetDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			if (_connected) {
				try {
					_socket.Emit("dimensions", new JObject { { "width", width }, { "height", height } });
				} catch (Exception e) {
					Logger.Error(e, "Error sending frame to socket.");
					_connected = false;
				}
			}
		}

		public void RenderGray2(byte[] frame)
		{
			if (_connected) {
				try {
					_socket.Emit("gray2frame", frame);
				} catch (Exception e) {
					Logger.Error(e, "Error sending frame to socket.");
					_connected = false;
				}
			}
		}

		public void RenderRgb24(byte[] frame)
		{
		}

		public void RenderColoredGray2(byte[][] planes, Color[] palette)
		{
		}

		public void SetColor(Color color)
		{
			// ignore
		}

		public void SetPalette(Color[] colors)
		{
			// ignore
		}

		public void ClearPalette()
		{
			// ignore
		}

		public void ClearColor()
		{
			// ignore
		}

		public void Dispose()
		{
			_socket?.Emit("stop");
			_socket?.Close();
		}
	}
}
