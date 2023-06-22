using System.Collections.Generic;
using System.Linq;
using NLog;

namespace LibDmd.Converter.Vni
{
	public abstract class AnimationFrame
	{
		/// <summary>
		/// When the frame within its frame sequence is played
		/// </summary>
		public uint Time { get; }

		/// <summary>
		/// Duration of the frame
		/// </summary>
		public uint Delay { get; protected set; }

		/// <summary>
		/// Bit length of the frames (and hence number of planes of each frame). Either 2 or 4.
		/// </summary>
		public int BitLength { get; protected set; }

		public List<AnimationPlane> Planes { get; protected set; }
		
		public byte[] Mask { get; protected set; }

		//public bool HasMask { get; protected set; }

		public byte[][] PlaneData => Planes.Select(p => p.Plane).ToArray();

		public uint Hash;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected AnimationFrame(uint time)
		{
			Time = time;
		}
	}
}
