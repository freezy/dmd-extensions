using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
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
using PinDmd;
using PinDmd.Input;
using PinDmd.Input.ScreenGrabber;
using PinDmd.Output;
using PinDmd.Processor;

namespace App
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly GrabberWindow _grabberWindow;
		private readonly RenderGraph _graph;

		public MainWindow()
		{
			InitializeComponent();
			Closing += OnWindowClosing;

			// define renderers
			var renderers = new List<IFrameDestination> { VirtualDmd };
			Console.Text += "Added VirtualDMD renderer.\n";
			var pinDmd = PinDmd.PinDmd.GetInstance();
			if (pinDmd.DeviceConnected) {
				renderers.Add(pinDmd);
				Console.Text += $"Added PinDMD3 renderer.\n";
				Console.Text += $"PinDMD3 detected at {pinDmd.Width}x{pinDmd.Height}\n";
				Console.Text += $"Firmware: {pinDmd.Firmware}\n";
			} else {
				Console.Text += "PinDMD3 not connected.\n";
			}
			
			// define sources
			var grabber = new ScreenGrabber { FramesPerSecond = 15 };

			// define processors
			var gridProcessor = new GridProcessor { Enabled = true, Padding = 1 };
			var resizeProcessor = new ResizeProcessor { Enabled = true };

			// chain them up
			_graph = new RenderGraph {
				Source = grabber,
				Destinations = renderers,
				Processors = new List<IProcessor> { gridProcessor, resizeProcessor }
			};

			_grabberWindow = new GrabberWindow(_graph) { Width = 256, Height = 64 };
			_grabberWindow.WhenPositionChanges.Subscribe(grabber.Move);
		}

		private void BitmapButton_Click(object sender, RoutedEventArgs e)
		{
			try {
				var bmp = new BitmapImage(new Uri("rgb-128x32.png", UriKind.Relative));
				_graph.Render(bmp);
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
