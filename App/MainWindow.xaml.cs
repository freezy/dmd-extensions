using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
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
			var monochromeProcessor = new MonochromeProcessor {
				Enabled = true,
				PixelFormat = PixelFormats.Gray16,
				Color = System.Windows.Media.Color.FromRgb(255, 155, 0)
			};

			// chain them up
			_graph = new RenderGraph {
				Source = grabber,
				Destinations = renderers,
				Processors = new List<AbstractProcessor> { gridProcessor, resizeProcessor, monochromeProcessor }
			};

			_grabberWindow = new GrabberWindow(_graph) {
				Left = Properties.Settings.Default.GrabLeft,
				Top = Properties.Settings.Default.GrabTop,
				Width = Properties.Settings.Default.GrabWidth,
				Height = Properties.Settings.Default.GrabHeight,
			};
			_grabberWindow.WhenPositionChanges.Subscribe(rect =>
			{
				grabber.Move(rect);
				Properties.Settings.Default.GrabLeft = rect.X;
				Properties.Settings.Default.GrabTop = rect.Y;
				Properties.Settings.Default.GrabWidth = rect.Width;
				Properties.Settings.Default.GrabHeight = rect.Height;
				Properties.Settings.Default.Save();
			});

			PreviewKeyDown += _grabberWindow.HotKey;
			PreviewKeyUp += _grabberWindow.HotKey;

			// grid preview images
			_graph.BeforeProcessed.Subscribe(bmp => {
				OriginalCapture.Dispatcher.Invoke(() => {
					OriginalCapture.Source = new WriteableBitmap(bmp); // freezes if bmp is used for some reason..
				});
			});
			gridProcessor.WhenProcessed.Subscribe(bmp => {
				ProcessedGrid.Dispatcher.Invoke(() => {
					ProcessedGrid.Source = bmp;
				});
			});
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
			Environment.Exit(0);
		}
	}
}
