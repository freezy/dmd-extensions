﻿using System;
using System.Timers;
using System.Windows.Media;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using NLog;
using WebSocketSharp;

namespace LibDmd.Output.Network
{
	public class NetworkStream : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IResizableDestination
	{
		public string Name { get; } = "Network Stream";
		public bool IsAvailable { get; private set; }
		public bool NeedsDuplicateFrames => false;

		private WebSocket _client;
		private Uri _uri;
		private bool _retry;
		private int _retryInterval;
		private Timer _retryTimer;
		private bool _disposed;
		private string _gameName;
		private readonly WebsocketSerializer _serializer = new WebsocketSerializer();

		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;

		private static NetworkStream _instance;
		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public static NetworkStream GetInstance(INetworkConfig config, string romName = null) {
			if (_instance == null) {
				_instance = new NetworkStream();
			}
			_instance.Init(config, romName);
			return _instance;
		}

		public void Init(INetworkConfig config, string romName = null)
		{
			_uri = new Uri(config.Url);
			_retry = config.Retry;
			// Convert interval to ms, make 1s the shortest retry interval
			_retryInterval = config.RetryInterval < 1 ? 1000 : config.RetryInterval * 1000;
			_gameName = romName;
			Logger.Info("Attempting to connect to WebSocket at {0}", _uri.ToString());
			_client = new WebSocket(_uri.ToString());
			_client.Log.Level = WebSocketSharp.LogLevel.Fatal;
			_client.OnMessage += OnMessage;
			_client.OnError += OnError;
			_client.OnOpen += OnOpen;
			_client.OnClose += OnClose;
			_client.Connect();
		}

		private void OnReconnect(object source, ElapsedEventArgs e)
		{
			// No point in retrying a connection if we have been disposed since timer was started.
			if (!_disposed)
			{
				Logger.Info("Retrying connection to WebSocket at {0}", _uri.ToString());
				_client.Connect();
			}
		}

		private void OnOpen(object sender, EventArgs e)
		{
			IsAvailable = true;
			Logger.Info("Connected to WebSocket at {0}", _uri.ToString());

			if (_gameName != null) {
				_client.Send(_serializer.SerializeGameName(_gameName));
			}
			_client.Send(_serializer.SerializeDimensions(_serializer.Dimensions));
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

		private void OnClose(object sender, EventArgs e)
		{
			Logger.Info("WebSocket connection was closed or could not be establised.");
			IsAvailable = false;
			if (!_disposed && _retry)
			{
				if (_retryTimer == null)
				{
					// Create a one-shot timer set to go off after the _retryInterval has passed.
					_retryTimer = new Timer(_retryInterval);
					_retryTimer.Elapsed += OnReconnect;
					_retryTimer.AutoReset = false;
				}
				_retryTimer.Start();
			}
		}

		private void SendGray(byte[] frame, int bitLength)
		{
			if (frame.Length < _serializer.Dimensions.Surface) {
				Logger.Info("SendGray: invalid frame received frame.length={0} bitlength={1} dim={2}", frame.Length, bitLength, _serializer.Dimensions);
				return;
			}
			if (IsAvailable) {
				_client.Send(_serializer.SerializeGray(frame, bitLength));
			}

		}

		public void RenderGray2(DmdFrame frame)
		{
			SendGray(frame.Data, 2);
		}

		public void RenderGray4(DmdFrame frame)
		{
			SendGray(frame.Data, 4);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			if (IsAvailable) {
				_client.Send(_serializer.SerializeColoredGray2(frame.BitPlanes, frame.Palette));
			}
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			if (IsAvailable) {
				_client.Send(_serializer.SerializeColoredGray4(frame.BitPlanes, frame.Palette));
			}
		}

		public void RenderRgb24(DmdFrame frame)
		{
			if (IsAvailable) {
				_client.Send(_serializer.SerializeRgb24(frame.Data));
			}
		}

		public void SetDimensions(Dimensions dim)
		{
			if (IsAvailable) {
				_client.Send(_serializer.SerializeDimensions(dim));
			}
		}

		public void SetColor(Color color)
		{
			_color = color;
			if (IsAvailable) {
				_client.Send(_serializer.SerializeColor(color));
			}
		}

		public void SetPalette(Color[] colors)
		{
			_palette = colors;
			if (IsAvailable) {
				_client.Send(_serializer.SerializePalette(colors));
			}
		}

		public void ClearPalette()
		{
			if (IsAvailable) {
				_client.Send(_serializer.SerializeClearPalette());
			}
		}

		public void ClearColor()
		{
			if (IsAvailable) {
				_client.Send(_serializer.SerializeClearColor());
			}
		}

		public void ClearDisplay()
		{
			// ignore
		}

		public void Dispose()
		{
			_disposed = true;
			_retryTimer?.Dispose();
			((IDisposable)_client)?.Dispose();
		}

	}

}
