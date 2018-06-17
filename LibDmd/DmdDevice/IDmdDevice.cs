using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
		void LoadPalette(uint palIndex);
		void SetPalette(Color[] colors);
		void RenderRgb24(int width, int height, byte[] frame);
		void RenderGray4(int width, int height, byte[] frame);
		void RenderGray2(int width, int height, byte[] frame);
		void RenderAlphaNumeric(NumericalLayout numericalLayout, ushort[] readUInt16Array, ushort[] ushorts);
	}
}
