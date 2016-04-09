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

namespace App
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private PinDmd.PinDmd _dmd;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void button_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				_dmd = new PinDmd.PinDmd();
				Console.Text += $"Display initialized. Connected: {_dmd.DeviceConnected}.\n";

				if (_dmd.DeviceConnected) {
					Console.Text += $"Display detected at {_dmd.Width}x{_dmd.Height}\n";
					Console.Text += $"Firmware: {_dmd.Firmware}\n";

				} else {
					Console.Text += "Device not connected.";
				}
				

			} catch (Exception err) {
				Console.Text = err.StackTrace;
			}
		}

		private void BitmapButton_Click(object sender, RoutedEventArgs e)
		{
			try {
				_dmd.RenderImage("rgb-128x32.png");

			} catch (Exception err) {
				Console.Text = err.Message + "\n" + err.StackTrace;
			}
		}
	}
}
