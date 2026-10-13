using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using LibDmd.Converter.Vni;

namespace LibDmd.Test
{
	/// <summary>
	/// Writes PAL, FSQ and VNI files for tests.
	/// </summary>
	internal class BigEndianWriter
	{
		private readonly MemoryStream _stream = new MemoryStream();

		public long Position => _stream.Position;

		public BigEndianWriter U8(int value)
		{
			_stream.WriteByte((byte)value);
			return this;
		}

		public BigEndianWriter U16(int value) => U8(value >> 8).U8(value);

		public BigEndianWriter U32(uint value) => U16((int)(value >> 16)).U16((int)(value & 0xffff));

		public BigEndianWriter Bytes(byte[] bytes)
		{
			_stream.Write(bytes, 0, bytes.Length);
			return this;
		}

		public BigEndianWriter Ascii(string text) => Bytes(Encoding.ASCII.GetBytes(text));

		public byte[] ToArray() => _stream.ToArray();
	}

	internal class PalBuilder
	{
		private readonly List<(int Index, Color[] Colors, int Type)> _palettes = new List<(int, Color[], int)>();
		private readonly List<(uint Checksum, SwitchMode Mode, int PaletteIndex, uint Value)> _mappings = new List<(uint, SwitchMode, int, uint)>();
		private readonly List<byte[]> _masks = new List<byte[]>();
		private readonly int _version;

		public PalBuilder(int version)
		{
			_version = version;
		}

		public PalBuilder Palette(int index, int type, params Color[] colors)
		{
			_palettes.Add((index, colors, type));
			return this;
		}

		public PalBuilder Mapping(uint checksum, SwitchMode mode, int paletteIndex, uint offsetOrDuration)
		{
			_mappings.Add((checksum, mode, paletteIndex, offsetOrDuration));
			return this;
		}

		public PalBuilder Mask(byte[] mask)
		{
			_masks.Add(mask);
			return this;
		}

		public byte[] Build()
		{
			var w = new BigEndianWriter().U8(_version).U16(_palettes.Count);
			foreach (var palette in _palettes) {
				w.U16(palette.Index).U16(palette.Colors.Length).U8(palette.Type);
				foreach (var color in palette.Colors) {
					w.U8(color.R).U8(color.G).U8(color.B);
				}
			}
			w.U16(_mappings.Count);
			foreach (var mapping in _mappings) {
				w.U32(mapping.Checksum).U8((int)mapping.Mode).U16(mapping.PaletteIndex).U32(mapping.Value);
			}
			w.U8(_masks.Count);
			foreach (var mask in _masks) {
				w.Bytes(mask);
			}
			return w.ToArray();
		}

		public PalFile BuildFile() => new PalFile(Build(), "test.pal");
	}

	internal class FsqFrame
	{
		public uint Delay;
		public uint Hash;
		public byte[] Mask;
		public byte[][] Planes;
	}

	internal class FsqBuilder
	{
		private readonly BigEndianWriter _writer = new BigEndianWriter();
		private readonly int _version;

		public FsqBuilder(int version)
		{
			_version = version;
			if (version > 0) {
				_writer.Ascii("FSQ ").U8(version);
			} else {
				_writer.U16(0);
			}
		}

		/// <returns>The offset of the animation</returns>
		public uint Animation(params FsqFrame[] frames)
		{
			var offset = (uint)_writer.Position;
			_writer.U16(frames.Length);
			foreach (var frame in frames) {
				var planeSize = frame.Planes.Length > 0 ? frame.Planes[0].Length : frame.Mask.Length;
				_writer.U32(frame.Delay).U16(frame.Planes.Length + (frame.Mask != null ? 1 : 0)).U16(planeSize);
				if (_version >= 2) {
					_writer.U32(frame.Hash);
				}
				if (frame.Mask != null) {
					_writer.U8(1).Bytes(frame.Mask);
				}
				foreach (var plane in frame.Planes) {
					if (_version >= 2) {
						_writer.U8(0);
					}
					_writer.Bytes(plane);
				}
			}
			return offset;
		}

		public byte[] Build() => _writer.ToArray();
	}

	internal static class Planes
	{
		public const int Size = 512;

		/// <summary>
		/// A 128x32 bit plane with the given pixels set.
		/// </summary>
		public static byte[] With(params int[] pixels)
		{
			var plane = new byte[Size];
			foreach (var pixel in pixels) {
				plane[pixel / 8] |= (byte)(1 << (pixel % 8));
			}
			return plane;
		}

		public static byte[] Empty() => new byte[Size];

		/// <summary>
		/// VNI stores the leftmost pixel in the most significant bit.
		/// </summary>
		public static byte[] Reversed(byte[] plane) => plane.Select(VniAnimationPlane.Reverse).ToArray();
	}
}
