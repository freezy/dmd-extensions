using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PinDmd.Output;

namespace App
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly GrabberWindow _grabberWindow;
		private readonly List<IFrameDestination> _renderers = new List<IFrameDestination>();

		public MainWindow()
		{
			InitializeComponent();
			Closing += OnWindowClosing;

			// output to both virtual dmd and pindmd if connected
			_renderers.Add(VirtualDmd);
			Console.Text += "Added VirtualDMD renderer.\n";

			var pinDmd = PinDmd.PinDmd.GetInstance();
			if (pinDmd.DeviceConnected) {
				_renderers.Add(pinDmd);
				Console.Text += $"Added PinDMD3 renderer.\n";
				Console.Text += $"PinDMD3 detected at {pinDmd.Width}x{pinDmd.Height}\n";
				Console.Text += $"Firmware: {pinDmd.Firmware}\n";
			} else {
				Console.Text += "PinDMD3 not connected.\n";
			}
			_grabberWindow = new GrabberWindow(_renderers);
		}

		private void BitmapButton_Click(object sender, RoutedEventArgs e)
		{
			try {
				var bmp = new Bitmap("rgb-128x32.png");
				foreach (var renderer in _renderers) {
					renderer.RenderBitmap(bmp);
				}
			} catch (Exception err) {
				Console.Text = err.Message + "\n" + err.StackTrace;
			}
		}

		private void ScreenButton_Click(object sender, RoutedEventArgs e)
		{
			if (_grabberWindow.IsVisible) {
				_grabberWindow.Hide();
			} else {
				_grabberWindow.Show();
			}
		}

		public void OnWindowClosing(object sender, CancelEventArgs cancelEventArgs)
		{
			_grabberWindow.Close();
		}
	}
}
