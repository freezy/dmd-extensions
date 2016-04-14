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
using PinDmd.Input.PBFX2Grabber;
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
		private readonly RenderGraph _screenGraph;
		private readonly RenderGraph _pbfxGraph;
		private readonly GridProcessor _gridProcessor;

		private IDisposable _currentSource;
		private RenderGraph _currentGraph;

		public MainWindow()
		{
			InitializeComponent();
			Closing += OnWindowClosing;

			// define renderers
			var renderers = new List<IFrameDestination> { VirtualDmd };
			Console.Text += "Added VirtualDMD renderer.\n";
			var pinDmd = PinDmd.Output.PinDmd3.PinDmd.GetInstance();
			if (pinDmd.IsAvailable) {
				renderers.Add(pinDmd);
				Console.Text += $"Added PinDMD3 renderer.\n";
				Console.Text += $"PinDMD3 detected at {pinDmd.Width}x{pinDmd.Height}\n";
				Console.Text += $"Firmware: {pinDmd.Firmware}\n";
			} else {
				Console.Text += "PinDMD3 not connected.\n";
			}
			var pin2Dmd = PinDmd.Output.Pin2Dmd.Pin2Dmd.GetInstance();
			if (pin2Dmd.IsAvailable) {
				renderers.Add(pin2Dmd);
				Console.Text += $"Added PIN2DMD renderer.\n";
			} else {
				Console.Text += "PIN2DMD not connected.\n";
			}

			// define sources
			var grabber = new ScreenGrabber { FramesPerSecond = 15 };
			var pin2DmdGrabber = new PBFX2Grabber { FramesPerSecond = 15 };
				
			// define processors
			_gridProcessor = new GridProcessor { Enabled = true, Spacing = 1 };
			var resizeProcessor = new TransformationProcessor { Enabled = true, FlipVertically = false, FlipHorizontally = false };
			var monochromeProcessor = new MonochromeProcessor {
				Enabled = true,
				PixelFormat = PixelFormats.Gray16,
				Tint = System.Windows.Media.Color.FromRgb(255, 155, 0)
			};

			// chain them up
			_screenGraph = new RenderGraph {
				Source = grabber,
				Destinations = renderers,
				Processors = new List<AbstractProcessor> { _gridProcessor, resizeProcessor, monochromeProcessor }
			};
			_pbfxGraph = new RenderGraph {
				Source = pin2DmdGrabber,
				Destinations = renderers,
				Processors = new List<AbstractProcessor> { _gridProcessor, resizeProcessor }
			};

			// init grabber window and link it to grabber
			_grabberWindow = new GrabberWindow() {
				Left = Properties.Settings.Default.GrabLeft,
				Top = Properties.Settings.Default.GrabTop,
				Width = Properties.Settings.Default.GrabWidth,
				Height = Properties.Settings.Default.GrabHeight,
			};
			_grabberWindow.WhenPositionChanges.Subscribe(rect => {
				grabber.Move(rect);
				Properties.Settings.Default.GrabLeft = rect.X;
				Properties.Settings.Default.GrabTop = rect.Y;
				Properties.Settings.Default.GrabWidth = rect.Width;
				Properties.Settings.Default.GrabHeight = rect.Height;
				Properties.Settings.Default.Save();
			});

			PreviewKeyDown += _grabberWindow.HotKey;
			PreviewKeyUp += _grabberWindow.HotKey;

			PreviewKeyDown += HotKey;

			// grid preview images
			_gridProcessor.WhenProcessed.Subscribe(bmp => {
				ProcessedGrid.Dispatcher.Invoke(() => {
					ProcessedGrid.Source = bmp;
				});
			});
		}

		private void HotKey(object sender, KeyEventArgs e)
		{
			if (e.IsDown) {
				switch (e.Key) {
					case Key.PageUp:
						_gridProcessor.Spacing += 0.1;
						Console.Text += "Grid padding: " + _gridProcessor.Spacing + "\n";
						break;

					case Key.PageDown:
						if (_gridProcessor.Spacing > 0) {
							_gridProcessor.Spacing -= 0.1;
							Console.Text += "Grid padding: " + _gridProcessor.Spacing + "\n";
						}
						break;
				}
			}
		}

		private void BitmapButton_Click(object sender, RoutedEventArgs e)
		{
			try {
				var bmp = new BitmapImage(new Uri("rgb-128x32.png", UriKind.Relative));
				_screenGraph.Render(bmp);
			} catch (Exception err) {
				Console.Text = err.Message + "\n" + err.StackTrace;
			}
		}

		private void PbfxButton_Click(object sender, RoutedEventArgs e)
		{
			// ignore if already runnong
			if (_pbfxGraph.IsRendering) {
				Console.Text += "Already capturing Pinball FX2. Launch a game if you don't see anything!\n";
				return;
			}
			SwitchGraph(_pbfxGraph);
			Console.Text += "Started pulling frames from Pinball FX2.\n";
		}

		private void ScreenButton_Click(object sender, RoutedEventArgs e)
		{
			// this one we stop if it's running and button was clicked again
			if (_screenGraph.IsRendering) {
				_grabberWindow.Hide();
				_screenGraph.StopRendering();
				_currentSource.Dispose();
				Console.Text += "Stopped pulling frames from desktop.\n";
				return;
			}

			_grabberWindow.Show();
			SwitchGraph(_screenGraph);
			Console.Text += "Started pulling frames from desktop.\n";
		}

		private void SwitchGraph(RenderGraph graph)
		{
			if (_currentGraph != null && _currentGraph.IsRendering) {
				_currentGraph.StopRendering();
				_currentSource.Dispose();
			}
			_currentGraph = graph;
			_currentSource = graph.BeforeProcessed.Subscribe(bmp => {
				OriginalCapture.Dispatcher.Invoke(() => {
					OriginalCapture.Source = new WriteableBitmap(bmp); // freezes if bmp is used for some reason..
				});
			});
			graph.StartRendering();
		}

		public void OnWindowClosing(object sender, CancelEventArgs cancelEventArgs)
		{
			_grabberWindow.Close();
			Environment.Exit(0);
		}

	}
}
