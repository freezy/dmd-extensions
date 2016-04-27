using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
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
using LibDmd;
using LibDmd.Input.PBFX2Grabber;
using LibDmd.Input.ScreenGrabber;
using LibDmd.Output;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Processor;

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
		private readonly ShadeProcessor _shadeProcessor;
		private readonly PBFX2Grabber _pin2DmdGrabber;

		private IDisposable _currentSource;
		private IDisposable _currentRenderer;
		private RenderGraph _currentGraph;

		public MainWindow()
		{
			InitializeComponent();
			Closing += OnWindowClosing;

			var title = (AssemblyTitleAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false).FirstOrDefault();
			var version = (AssemblyInformationalVersionAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).FirstOrDefault();
			Title = $"{title?.Title} v{version?.InformationalVersion}";


			// define renderers
			var renderers = new List<IFrameDestination> { VirtualDmd };
			Console.Text += "Added VirtualDMD renderer.\n";

			try {
				var pinDmd = PinDmd3.GetInstance();
				if (pinDmd.IsAvailable) {
					renderers.Add(pinDmd);
					Console.Text += $"Added PinDMDv3 renderer.\n";
					Console.Text += $"PinDMDv3 detected at {pinDmd.Width}x{pinDmd.Height}\n";
					Console.Text += $"Firmware: {pinDmd.Firmware}\n";
				} else {
					Console.Text += "PinDMDv3 not connected.\n";
				}
				var pin2Dmd = Pin2Dmd.GetInstance();
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Console.Text += $"Added PIN2DMD renderer.\n";
				} else {
					Console.Text += "PIN2DMD not connected.\n";
				}
				var pinDmd2 = PinDmd2.GetInstance();
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Console.Text += $"Added PinDMDv2 renderer.\n";
				} else {
					Console.Text += "PinDMDv2 not connected.\n";
				}
				var pinDmd1 = PinDmd1.GetInstance();
				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Console.Text += $"Added PinDMDv1 renderer.\n";
				} else {
					Console.Text += "PinDMDv1 not connected.\n";
				}

			} catch (DllNotFoundException e) {
				Console.Text += "A DLL was not found. It's possible that Windows blocked it.\n";
				Console.Text += "Go look for it in the installation folder. If it's there, right-click, \"Properties\" and \"unblock\".\n";
				Console.Text += "Then restart the app.\n";
				Console.Text += "Message: " + e.Message + "\n";
			}


			// define sources
			var grabber = new ScreenGrabber { FramesPerSecond = 15 };
			_pin2DmdGrabber = new PBFX2Grabber { FramesPerSecond = 25 };
				
			// define processors
			_gridProcessor = new GridProcessor { Enabled = true, Spacing = 1 };
			var transformationProcessor = new TransformationProcessor { Enabled = true, FlipVertically = false, FlipHorizontally = false };
			var monochromeProcessor = new MonochromeProcessor {
				Enabled = true,
				PixelFormat = PixelFormats.Gray16,
				Tint = System.Windows.Media.Color.FromRgb(255, 155, 0)
			};
			_shadeProcessor = new ShadeProcessor { NumShades = 4, Intensity = 2.5, Brightness = 0 };

			// chain them up
			_screenGraph = new RenderGraph {
				Source = grabber,
				Destinations = renderers,
				Processors = new List<BaseProcessor> { transformationProcessor, monochromeProcessor }
			};
			_pbfxGraph = new RenderGraph {
				Source = _pin2DmdGrabber,
				Destinations = renderers,
				Processors = new List<BaseProcessor> { _gridProcessor, transformationProcessor, _shadeProcessor }
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

					case Key.Q:
						_shadeProcessor.Intensity -= 0.1;
						Console.Text += "Intensity: " + _shadeProcessor.Intensity + "\n";
						break;
					case Key.W:
						_shadeProcessor.Intensity += 0.1;
						Console.Text += "Intensity: " + _shadeProcessor.Intensity + "\n";
						break;

					case Key.E:
						_shadeProcessor.Brightness -= 0.1;
						Console.Text += "Lightness: " + _shadeProcessor.Brightness + "\n";
						break;
					case Key.R:
						_shadeProcessor.Brightness += 0.1;
						Console.Text += "Lightness: " + _shadeProcessor.Brightness + "\n";
						break;

					case Key.A:
						_pin2DmdGrabber.CropLeft -= 1;
						Console.Text += "Crop left: " + _pin2DmdGrabber.CropLeft + "\n";
						break;
					case Key.S:
						_pin2DmdGrabber.CropLeft += 1;
						Console.Text += "Crop left: " + _pin2DmdGrabber.CropLeft + "\n";
						break;
					case Key.D:
						_pin2DmdGrabber.CropRight += 1;
						Console.Text += "Crop right: " + _pin2DmdGrabber.CropRight + "\n";
						break;
					case Key.F:
						_pin2DmdGrabber.CropRight -= 1;
						Console.Text += "Crop right: " + _pin2DmdGrabber.CropRight + "\n";
						break;

					case Key.Y:
						_pin2DmdGrabber.CropTop -= 1;
						Console.Text += "Crop top: " + _pin2DmdGrabber.CropTop + "\n";
						break;
					case Key.X:
						_pin2DmdGrabber.CropTop += 1;
						Console.Text += "Crop left: " + _pin2DmdGrabber.CropTop + "\n";
						break;
					case Key.C:
						_pin2DmdGrabber.CropBottom += 1;
						Console.Text += "Crop bottom: " + _pin2DmdGrabber.CropBottom + "\n";
						break;
					case Key.V:
						_pin2DmdGrabber.CropBottom -= 1;
						Console.Text += "Crop bottom: " + _pin2DmdGrabber.CropBottom + "\n";
						break;
				}
			}
		}

		private void BitmapButton_Click(object sender, RoutedEventArgs e)
		{
			if (_currentRenderer != null) {
				_currentRenderer.Dispose();
				_currentSource.Dispose();
				_currentRenderer = null;
			}
			try {
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.UriSource = new Uri("pack://application:,,,/App;component/rgb-128x32.png");
				bmp.EndInit();

				_pbfxGraph.Render(bmp);
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
			_grabberWindow.Hide();
			SwitchGraph(_pbfxGraph);
			Console.Text += "Started pulling frames from Pinball FX2.\n";
		}

		private void ScreenButton_Click(object sender, RoutedEventArgs e)
		{
			// this one we stop if it's running and button was clicked again
			if (_screenGraph.IsRendering) {
				_grabberWindow.Hide();
				_currentRenderer.Dispose();
				_currentSource.Dispose();
				_currentRenderer = null;
				Console.Text += "Stopped pulling frames from desktop.\n";
				return;
			}

			_grabberWindow.Show();
			SwitchGraph(_screenGraph);
			Console.Text += "Started pulling frames from desktop.\n";
		}

		private void SwitchGraph(RenderGraph graph)
		{
			if (_currentRenderer != null) {
				_currentRenderer.Dispose();
				_currentSource.Dispose();
				_currentRenderer = null;
			}
			_currentGraph = graph;
			_currentSource = graph.BeforeProcessed.Subscribe(bmp => {
				OriginalCapture.Dispatcher.Invoke(() => {
					OriginalCapture.Source = new WriteableBitmap(bmp); // freezes if bmp is used for some reason..
				});
			});
			_currentRenderer = graph.StartRendering();
		}

		public void OnWindowClosing(object sender, CancelEventArgs cancelEventArgs)
		{
			_grabberWindow.Close();
			_currentRenderer?.Dispose();
			_screenGraph.Dispose();
			_pbfxGraph.Dispose();

			Environment.Exit(0);
		}
	}
}
