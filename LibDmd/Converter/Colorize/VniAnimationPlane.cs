using System.IO;
using System.Linq;

namespace LibDmd.Converter.Colorize
{
	public class VniAnimationPlane : AnimationPlane
	{
		public VniAnimationPlane(BinaryReader reader, int planeSize, byte marker)
		{
			Marker = marker;
			Plane = reader.ReadBytes(planeSize).Select(Reverse).ToArray();
		}

		public static byte Reverse(byte a)
		{
			return (byte)(((a & 0x1) << 7) | ((a & 0x2) << 5) |
				((a & 0x4) << 3) | ((a & 0x8) << 1) |
				((a & 0x10) >> 1) | ((a & 0x20) >> 3) |
				((a & 0x40) >> 5) | ((a & 0x80) >> 7));
		}
	}
}
