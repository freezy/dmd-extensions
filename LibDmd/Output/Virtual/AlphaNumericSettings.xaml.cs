using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibDmd.Output.Virtual;

namespace LibDmd.Common
{
	/// <summary>
	/// Interaction logic for AlphaNumericSettings.xaml
	/// </summary>
	public partial class VirtualAlphaNumericSettings : Window
	{

		private readonly AlphanumericControl _control;

		public VirtualAlphaNumericSettings(AlphanumericControl control, double top, double left)
		{
			Top = top;
			Left = left;
			InitializeComponent();

			_control = control;

			ForegroundStyle.RasterizeStyle = _control.RasterizeStyle.Foreground;
			InnerGlowStyle.RasterizeStyle = _control.RasterizeStyle.InnerGlow;
			OuterGlowStyle.RasterizeStyle = _control.RasterizeStyle.OuterGlow;
			UnlitStyle.RasterizeStyle = _control.RasterizeStyle.Background;

		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
