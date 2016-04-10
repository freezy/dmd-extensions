using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
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
using PinDmd.Input;

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
			IsVisibleChanged += ToggleGrabbing;

			_grabber = new ScreenGrabber();
			_grabber.FramesPerSecond = 30;
			_dmd = PinDmd.PinDmd.GetInstance();
		}

		private void ToggleGrabbing(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (IsVisible) {
				Console.WriteLine("Starting grabbing...");
				_dmd.StartRenderingSequence(_grabber);
			} else {
				Console.WriteLine("Stopping grabbing...");
				_dmd.StopRenderingSequence();
			}
		}

		private void Window_LocationChanged(object sender, EventArgs e)
		{
			_grabber.Left = (int)Left;
			_grabber.Top = (int)Top;
			_grabber.Width = (int)Width;
			_grabber.Height = (int)Height;
		}

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left) {
				DragMove();
			}
		}
	}
}
