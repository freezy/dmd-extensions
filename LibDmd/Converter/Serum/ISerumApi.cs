using System.Windows.Media;

namespace LibDmd.Converter.Serum
{
	public interface ISerumApi
	{
		uint NumColors { get; }
		void Convert(ref SerumFrame serumFrame);
		void UpdateRotations(ref SerumFrame serumFrame, Color[] palette, uint changed);
	}
}
