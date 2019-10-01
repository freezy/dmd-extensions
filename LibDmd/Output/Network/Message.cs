using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Common;
using System.Windows.Media;

namespace LibDmd.Output.Network
{
	class Message
	{
		private readonly long _startedAt = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

		public byte[] GetDimensions(int width, int height)
		{
			return GetHeader(MessageType.Dimensions)
				.Concat(BitConverter.GetBytes((ushort)width))
				.Concat(BitConverter.GetBytes((ushort)height))
				.ToArray();
		}

		public byte[] GetColor(Color color)
		{
			return GetHeader(MessageType.Color)
				.Concat(BitConverter.GetBytes(ColorUtil.ToInt(color)))
				.ToArray();
		}
		
		public byte[] GetPalette(Color[] colors)
		{
			return GetHeader(MessageType.Palette)
				.Concat(BitConverter.GetBytes((ushort)colors.Length))
				.Concat(ColorUtil.ToIntArray(colors).SelectMany(BitConverter.GetBytes))
				.ToArray();
		}

		public byte[] GetGrayFrame(byte[] frame, int width, int height, int bitLength)
		{
			return GetHeader(MessageType.GrayFrame)
				.Concat(new[] { (byte)bitLength })
				.Concat(FrameUtil.Split(width, height, bitLength, frame).SelectMany(p => p))
				.ToArray();
		}

		public byte[] GetColoredGrayFrame(byte[][] planes, Color[] palette)
		{
			return GetHeader(MessageType.ColoredGrayFrame)
				.Concat(new[] { (byte)planes.Length })
				.Concat(ColorUtil.ToIntArray(palette).SelectMany(BitConverter.GetBytes))
				.Concat(planes.SelectMany(p => p))
				.ToArray();
		}	
		
		public byte[] GetRgb24Frame(byte[] frame)
		{
			return GetHeader(MessageType.Rgb24Frame)
				.Concat(frame)
				.ToArray();
		}

		public byte[] GetClearDisplay()
		{
			return GetHeader(MessageType.ClearDisplay).ToArray();
		}

		public byte[] GetClearColor()
		{
			return GetHeader(MessageType.ClearColor).ToArray();
		}	
		
		public byte[] GetClearPalette()
		{
			return GetHeader(MessageType.ClearPalette).ToArray();
		}

		private IEnumerable<byte> GetHeader(MessageType type)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			return new[] { (byte)type }
				.Concat(BitConverter.GetBytes((uint)(timestamp - _startedAt)));
		}
	}

	enum MessageType
	{
		Init = 0x00,

		Dimensions = 0x01,
		Color = 0x02,
		Palette = 0x03,

		GrayFrame = 0x10,
		ColoredGrayFrame = 0x11,
		Rgb24Frame = 0x12,

		ClearDisplay = 0x20,
		ClearColor = 0x21,
		ClearPalette = 0x22,
	}
}
