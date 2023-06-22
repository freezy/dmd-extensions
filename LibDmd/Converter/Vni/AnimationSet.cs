using System.Collections.Generic;
using System.Linq;
using NLog;

namespace LibDmd.Converter.Vni
{
	public abstract class AnimationSet
	{
		protected int Version;
		protected List<Animation> Animations;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Findet än Animazion wo anärä gegäbänä Steu im Feil gsi isch.
		/// </summary>
		/// <param name="offset">D Steu im Feil</param>
		/// <returns>Diä gfundini Animazion odr sisch null</returns>
		public Animation Find(uint offset)
		{
			return Animations.FirstOrDefault(animation => animation.Offset == offset);
		}

		public override string ToString()
		{
			return $"VPIN v{Version}, {Animations.Count} animation(s)";
		}
	}
}
