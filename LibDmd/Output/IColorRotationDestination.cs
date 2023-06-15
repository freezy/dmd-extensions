using System.Windows.Media;

namespace LibDmd.Output
{
	/// <summary>
	/// A destination that supports color rotation.
	/// </summary>
	public interface IColorRotationDestination : IDestination
	{
		//int MaxBitLength { get; }

		void UpdatePalette(Color[] palette);
	}
}
