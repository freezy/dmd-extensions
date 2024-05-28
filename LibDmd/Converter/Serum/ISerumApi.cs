using System.Windows.Media;

namespace LibDmd.Converter.Serum
{
	public interface ISerumApi
	{
		uint NumColors { get; }
		bool Convert(ref SerumFrame serumFrame, uint rotations);
		void UpdateRotations(ref SerumFrame serumFrame, Color[] palette);
	}
}
