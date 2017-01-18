using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using NLog;
using Quobject.SocketIoClientDotNet.Client;

namespace LibDmd.Output.Network
{
	public class VpdbStream : IGray2Destination, IColoredGray2Destination, IResizableDestination
	{
		public string Name { get; } = "VPDB Streamer";
		public bool IsAvailable { get; } = true;

		public string ApiKey { get; set; }
		public string EndPoint { get; set; } = "http://127.0.0.1:3000/";
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }

		private Socket _socket;
		private bool _connected;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VpdbStream()
		{
			Logger.Info("Connecting to VPDB...");
			_socket = IO.Socket(EndPoint);
			_socket.On(Socket.EVENT_CONNECT, () => {
				_connected = true;
				Logger.Info("Connected to VPDB.");
				_socket.Emit("produce");
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
			if (_connected) {
				_socket.Emit("dimensions", new []{ width, height });
			}
		}

		public void RenderGray2(byte[] frame)
		{
			if (_connected) {
				_socket.Emit("gray2frame", frame);
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
			_socket?.Emit("end");
			_socket?.Close();
		}
	}
}
