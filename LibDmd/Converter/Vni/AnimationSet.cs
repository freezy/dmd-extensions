using System.Collections.Generic;
using System.Linq;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Converter.Vni
{
	public abstract class AnimationSet
	{
		public Dimensions Dimensions { get; protected set; } = new Dimensions(128, 32);

		protected int Version;
		protected List<FrameSeq> Animations;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Findet än Animazion wo anärä gegäbänä Steu im Feil gsi isch.
		/// </summary>
		/// <param name="offset">D Steu im Feil</param>
		/// <returns>Diä gfundini Animazion odr sisch null</returns>
		public FrameSeq Find(uint offset)
		{
			return Animations.FirstOrDefault(animation => animation.Offset == offset);
		}

		public override string ToString()
		{
			return $"VPIN v{Version}, {Animations.Count} animation(s)";
		}
	}
}
