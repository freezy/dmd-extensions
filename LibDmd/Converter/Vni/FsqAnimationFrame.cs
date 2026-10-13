using System.Collections.Generic;
using System.IO;
using LibDmd.Common;

namespace LibDmd.Converter.Vni
{
	/// <summary>
	/// A frame of an FSQ animation.
	/// </summary>
	///
	/// <remarks>
	/// Unlike VNI, the planes are stored with the leftmost pixel in the least
	/// significant bit, so they don't need to be reversed.
	/// </remarks>
	public class FsqAnimationFrame : AnimationFrame
	{
		/// <summary>
		/// Marks the plane of a frame that contains the replace mask.
		/// </summary>
		private const byte MaskMarker = 0x01;

		/// <summary>
		/// Size of each bit plane in bytes.
		/// </summary>
		public int PlaneSize { get; }

		public FsqAnimationFrame(BinaryReader reader, int fileVersion, uint time) : base(time)
		{
			Delay = reader.ReadUInt32BE();
			BitLength = reader.ReadUInt16BE();
			PlaneSize = reader.ReadUInt16BE();
			Planes = new List<AnimationPlane>(BitLength);

			// v2+ have a hash and a marker per plane, older versions only the planes.
			if (fileVersion >= 2) {
				Hash = reader.ReadUInt32BE();
			}
			for (var i = 0; i < BitLength; i++) {
				var marker = fileVersion >= 2 ? reader.ReadByte() : (byte)0;
				var plane = reader.ReadBytesRequired(PlaneSize);
				if (marker == MaskMarker) {
					Mask = plane;
				} else {
					Planes.Add(new FsqAnimationPlane(plane, marker));
				}
			}
		}

		/// <summary>
		/// Puts empty planes in front of the frame's planes.
		/// </summary>
		///
		/// <remarks>
		/// Color mask animations only store the planes that are added to the
		/// incoming frame, while the renderer expects all planes, like in VNI.
		/// </remarks>
		/// <param name="count">Number of planes to insert</param>
		public void PrependEmptyPlanes(int count)
		{
			for (var i = 0; i < count; i++) {
				Planes.Insert(0, new FsqAnimationPlane(new byte[PlaneSize], 0));
			}
		}
	}

	public class FsqAnimationPlane : AnimationPlane
	{
		public FsqAnimationPlane(byte[] plane, byte marker)
		{
			Marker = marker;
			Plane = plane;
		}
	}
}
