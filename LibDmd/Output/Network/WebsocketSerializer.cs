using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Routing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.Network
{
	internal interface ISocketAction
	{
		void OnColor(Color color);
		void OnPalette(Color[] palette);
		void OnClearColor();
		void OnClearPalette();
		void OnGameName(string gameName);
		void OnRgb24(uint timestamp, byte[] frame);
		void OnColoredGray6(uint timestamp, Color[] palette, byte[] data); //, byte[] rotations); 
		void OnColoredGray4(uint timestamp, Color[] palette, byte[] data);
		void OnColoredGray2(uint timestamp, Color[] palette, byte[] data);
		void OnGray4(uint timestamp, byte[] frame);
		void OnGray2(uint timestamp, byte[] frame);
	}

	internal class WebsocketSerializer
	{
		public Dimensions Dimensions = Dimensions.Standard;

		private readonly long _startedAt = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void Unserialize(byte[] data, ISocketAction action) {
			var start = 0;
			using (var memoryStream = new MemoryStream(data))
			using (var reader = new BinaryReader(memoryStream)) {
				while (data[start++] != 0x0) ;

				var name = Encoding.ASCII.GetString(reader.ReadBytes(start - 1));
				reader.BaseStream.Seek(1, SeekOrigin.Current);
				
				switch (name) {
					case "color": {
						action.OnColor(ColorUtil.FromInt(reader.ReadInt32()));
						break;
					}
					case "palette": {
						var len = reader.ReadInt32();
						var palette = new Color[len];
						for (var i = 0; i < len; i++) {
							palette[i] = ColorUtil.FromInt(reader.ReadInt32());
						}
						action.OnPalette(palette);
						break;
					}
					case "clearColor": {
						action.OnClearColor();
						break;
					}	
					case "clearPalette": {
						action.OnClearPalette();
						break;
					}
					case "dimensions": {
						Dimensions = new Dimensions(reader.ReadInt32(), reader.ReadInt32());
						break;
					}
					case "gameName": {
						char c;
						var gameName = "";
						do {
							c = reader.ReadChar();
							gameName += c;
						} while (c != 0x0) ;
						action.OnGameName(gameName);
						break;
					}
					case "rgb24": {
						action.OnRgb24(reader.ReadUInt32(), reader.ReadBytes(data.Length - (int)reader.BaseStream.Position));
						break;
					}
					case "coloredGray6": {
							// 13 name
							var timestamp = reader.ReadUInt32(); 
							var numColors = reader.ReadInt32();  
							var palette = new Color[numColors]; 
							for (var i = 0; i < numColors; i++) {
								palette[i] = ColorUtil.FromInt(reader.ReadInt32());
							}
							var planes = new byte[6][];  
							var planeSize = (data.Length - (int)reader.BaseStream.Position) / 6;
							for (var i = 0; i < 6; i++) {
								planes[i] = reader.ReadBytes(planeSize);
							}
							action.OnColoredGray6(timestamp, palette, FrameUtil.Join(Dimensions, planes));
							// missing - rotations!!!!
							break;
						}
					case "coloredGray4": {
						var timestamp = reader.ReadUInt32();
						var numColors = reader.ReadInt32();
						var palette = new Color[numColors];
						for (var i = 0; i < numColors; i++) {
							palette[i] = ColorUtil.FromInt(reader.ReadInt32());
						}
						var planes = new byte[4][];
						var planeSize = (data.Length - (int)reader.BaseStream.Position) / 4;
						for (var i = 0; i < 4; i++) {
							planes[i] = reader.ReadBytes(planeSize);
						}
						action.OnColoredGray4(timestamp, palette, FrameUtil.Join(Dimensions, planes));
						break;
					}
					case "coloredGray2": {
						var timestamp = reader.ReadUInt32();
						var numColors = reader.ReadInt32();
						var palette = new Color[numColors];
						for (var i = 0; i < numColors; i++) {
							palette[i] = ColorUtil.FromInt(reader.ReadInt32());
						}
						var planes = new byte[2][];
						var planeSize = (data.Length - (int)reader.BaseStream.Position) / 2;
						for (var i = 0; i < 2; i++) {
							planes[i] = reader.ReadBytes(planeSize);
						}
						action.OnColoredGray2(timestamp, palette, FrameUtil.Join(Dimensions, planes));
						break;
					}
					case "gray4Planes": {
						var timestamp = reader.ReadUInt32();
						var planes = new byte[4][];
						var planeSize = (data.Length - (int)reader.BaseStream.Position) / 4;
						for (var i = 0; i < 4; i++) {
							planes[i] = reader.ReadBytes(planeSize);
						}
						action.OnGray4(timestamp, FrameUtil.Join(Dimensions, planes));
						break;
					}
					case "gray2Planes": {
						var timestamp = reader.ReadUInt32();
						var planes = new byte[2][];
						var planeSize = (data.Length - (int)reader.BaseStream.Position) / 2;
						for (var i = 0; i < 2; i++) {
							planes[i] = reader.ReadBytes(planeSize);
						}
						action.OnGray2(timestamp, FrameUtil.Join(Dimensions, planes));
						break;
					}
				}
			}
		}

		public byte[] SerializeGray(byte[] frame, int bitLength)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var data = Encoding.ASCII
				.GetBytes("gray" + bitLength + "Planes")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes((uint)(timestamp - _startedAt)))
				.Concat(FrameUtil.Split(Dimensions, bitLength, frame).SelectMany(p => p));

			return data.ToArray();
		}

		public byte[] SerializeColoredGray2(byte[][] planes, Color[] palette)
		{
			return SerializeColoredGray("coloredGray2", planes, palette);
		}

		public byte[] SerializeColoredGray4(byte[][] planes, Color[] palette)
		{
			return SerializeColoredGray("coloredGray4", planes, palette);
		}

		public byte[] SerializeColoredGray6(byte[][] planes, Color[] palette, byte[] rotations, bool RotateColors)
		{
			var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var buffer = new byte[24];
			if (!RotateColors) {
				for (var i = 0; i< 24; i++) {
					buffer[i] = 255;
				}
			}
			else
				for (var i = 0; i< 24; i++) {
					buffer[i] = rotations[i];
				}
			var data = Encoding.ASCII
				.GetBytes("coloredGray6")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes((uint)(timestamp - _startedAt)))
				.Concat(BitConverter.GetBytes(palette.Length))
				.Concat(ColorUtil.ToIntArray(palette).SelectMany(BitConverter.GetBytes))
				.Concat(buffer)
				.Concat(planes.SelectMany(p => p));
			return data.ToArray();
		}

		private byte[] SerializeColoredGray(string name, byte[][] planes, Color[] palette)
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

		public byte[] SerializeDimensions(Dimensions dim)
		{
			Dimensions = dim;
			var data = Encoding.ASCII
				.GetBytes("dimensions")
				.Concat(new byte[] { 0x0 })
				.Concat(BitConverter.GetBytes(Dimensions.Width))
				.Concat(BitConverter.GetBytes(Dimensions.Height));
			Logger.Info($"Sent dimensions to socket {Dimensions}.");
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
