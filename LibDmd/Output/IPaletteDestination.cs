using System.Windows.Media;

namespace LibDmd.Output
{
	public interface IPaletteDestination : IDestination
	{
		/// <summary>
		/// Sets the palette for rendering grayscale images.
		/// </summary>
		/// <param name="colors"></param>
		void SetPalette(Color[] colors);

		/// <summary>
		/// Removes a previously set palette
		/// </summary>
		void ClearPalette();
	}
}
