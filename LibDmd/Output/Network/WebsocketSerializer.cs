using System;
using System.Linq;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using NLog;
using WebSocketSharp;

namespace LibDmd.Output.Network
{
	internal class WebsocketSerializer
	{
		public int Width = 128;
		public int Height = 32;

		private readonly long _startedAt = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public byte[] SerializeGray(byte[] frame, int bitlength)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var data = Encoding.ASCII
				.GetBytes("gray" + bitlength + "Planes")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes((uint)(timestamp - _startedAt)))
				.Concat(FrameUtil.Split(Width, Height, bitlength, frame).SelectMany(p => p));

			return data.ToArray();
		}

		public byte[] SerializeColoredGray(string name, byte[][] planes, Color[] palette)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var data = Encoding.ASCII
				.GetBytes(name)
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes((uint)(timestamp - _startedAt)))
				.Concat(BitConverter.GetBytes(palette.Length))
				.Concat(ColorUtil.ToIntArray(palette).SelectMany(BitConverter.GetBytes))
				.Concat(planes.SelectMany(p => p));
			return data.ToArray();
		}

		public byte[] SerializeRgb24(byte[] frame)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var data = Encoding.ASCII
				.GetBytes("rgb24")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes((uint)(timestamp - _startedAt)))
				.Concat(frame);
			return data.ToArray();
		}

		public byte[] SerializeGameName(string gameName)
		{
			var data = Encoding.ASCII
				.GetBytes("gameName")
				.Concat(new byte[] { 0x0 })
				.Concat(Encoding.ASCII.GetBytes(gameName));
			Logger.Info("Sent game name to socket.");
			return data.ToArray();
		}

		public byte[] SerializeDimensions(int width, int height)
		{
			Width = width;
			Height = height;
			var data = Encoding.ASCII
				.GetBytes("dimensions")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes(width))
				.Concat(BitConverter.GetBytes(height));
			Logger.Info("Sent dimensions to socket {0}x{1}.", width, height);
			return data.ToArray();
		}

		public byte[] SerializeColor(Color color)
		{
			var data = Encoding.ASCII
				.GetBytes("color")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes(ColorUtil.ToInt(color)));
			return data.ToArray();
		}

		public byte[] SerializePalette(Color[] colors)
		{
			var data = Encoding.ASCII
				.GetBytes("palette")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes(colors.Length))
				.Concat(ColorUtil.ToIntArray(colors).SelectMany(BitConverter.GetBytes));
			return data.ToArray();
		}

		public byte[] SerializeClearColor()
		{
			var data = Encoding.ASCII
				.GetBytes("clearColor")
				.Concat(new byte[] { 0x0 });
			return data.ToArray();
		}

		public byte[] SerializeClearPalette()
		{
			var data = Encoding.ASCII
				.GetBytes("clearPalette")
				.Concat(new byte[] { 0x0 });
			return data.ToArray();
		}
	}
}
