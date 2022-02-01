using System.Windows.Media;

namespace LibDmd.DmdDevice
{
	public interface IDmdDevice
	{
		void Close();
		void SetColorize(bool colorize);
		void SetGameName(string gameName);
		void SetColor(Color color);
		void Init();
		int getAniWidth();
		int getAniHeight();
		void LoadPalette(uint palIndex);
		void SetPalette(Color[] colors);
		void RenderRgb24(DMDFrame frame);
		void RenderGray4(DMDFrame frame);
		void RenderGray2(DMDFrame frame);
		void RenderAlphaNumeric(NumericalLayout numericalLayout, ushort[] readUInt16Array, ushort[] ushorts);
	}
}
