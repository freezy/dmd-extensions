using System;
using System.Collections.Generic;
using System.Drawing;
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
using PinDmd;

namespace App
{
	/// <summary>
	/// Interaction logic for ScreenGrabber.xaml
	/// </summary>
	public partial class GrabberWindow : Window
	{
		private readonly ScreenGrabber _grabber;
		private readonly PinDmd.PinDmd _dmd;

		public GrabberWindow()
		{
			InitializeComponent();
			MouseDown += Window_MouseDown;
			LocationChanged += Window_LocationChanged;

			_grabber = new ScreenGrabber();
			_dmd = PinDmd.PinDmd.GetInstance();
		}

		private void Window_LocationChanged(object sender, EventArgs e)
		{
			var bmp = _grabber.CaptureImage((int)Left, (int)Top, (int)Width, (int)Height);
			if (_dmd.DeviceConnected) {
				_dmd.RenderImage(bmp);
			}
		}

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left) {
				DragMove();
			}
		}
	}
}
