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
using LibDmd.Input.TPAGrabber;
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
		private readonly RenderGraph _tpaGraph;
		private readonly GridProcessor _pbfxGridProcessor;
		private readonly GridProcessor _tpaGridProcessor;
		private readonly ShadeProcessor _pbfxShadeProcessor;
		private readonly ShadeProcessor _tpaShadeProcessor;
		private readonly PBFX2Grabber _pbfxGrabber;
		private readonly TPAGrabber _tpaGrabber;

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
			Console.AppendText("Added VirtualDMD renderer.\n");

			try {
				var pinDmd = PinDmd3.GetInstance();
				if (pinDmd.IsAvailable) {
					renderers.Add(pinDmd);
					Console.AppendText($"Added PinDMDv3 renderer.\n");
					Console.AppendText($"PinDMDv3 detected at {pinDmd.Width}x{pinDmd.Height}\n");
					Console.AppendText($"Firmware: {pinDmd.Firmware}\n");
				} else {
					Console.AppendText("PinDMDv3 not connected.\n");
				}
				var pin2Dmd = Pin2Dmd.GetInstance();
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Console.AppendText($"Added PIN2DMD renderer.\n");
				} else {
					Console.AppendText("PIN2DMD not connected.\n");
				}
				var pinDmd2 = PinDmd2.GetInstance();
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Console.AppendText($"Added PinDMDv2 renderer.\n");
				} else {
					Console.AppendText("PinDMDv2 not connected.\n");
				}
				var pinDmd1 = PinDmd1.GetInstance();
				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Console.AppendText($"Added PinDMDv1 renderer.\n");
				} else {
					Console.AppendText("PinDMDv1 not connected.\n");
				}

			} catch (DllNotFoundException e) {
				Console.AppendText("A DLL was not found. It's possible that Windows blocked it.\n");
				Console.AppendText("Go look for it in the installation folder. If it's there, right-click, \"Properties\" and \"unblock\".\n");
				Console.AppendText("Then restart the app.\n");
				Console.AppendText("Message: " + e.Message + "\n");
			}

			// define sources
			var grabber = new ScreenGrabber { FramesPerSecond = 15 };
			_pbfxGrabber = new PBFX2Grabber { FramesPerSecond = 25 };
			_tpaGrabber = new TPAGrabber { FramesPerSecond = 15 };
				
			// define processors
			_pbfxGridProcessor = new GridProcessor { Enabled = true, Spacing = 1 };
			_tpaGridProcessor = new GridProcessor { Enabled = true, Spacing = 0, CropRight = 0, CropBottom = 1 };
			var transformationProcessor = new TransformationProcessor { Enabled = true, FlipVertically = false, FlipHorizontally = false };
			var monochromeProcessor = new MonochromeProcessor {
				Enabled = true,
				PixelFormat = PixelFormats.Gray16,
				Tint = System.Windows.Media.Color.FromRgb(255, 155, 0)
			};
			_pbfxShadeProcessor = new ShadeProcessor { NumShades = 4, Intensity = 2.5, Brightness = 0 };
			_tpaShadeProcessor = new ShadeProcessor {
				NumShades = 4,
				Intensity = 1.9,
				Brightness = 0,
				Shades = new[] { 0d, 0.2, 0.35, 0.55 }
			};

			// chain them up
			_screenGraph = new RenderGraph {
				Source = grabber,
				Destinations = renderers,
				Processors = new List<AbstractProcessor> { transformationProcessor, monochromeProcessor }
			};
			_pbfxGraph = new RenderGraph {
				Source = _pbfxGrabber,
				Destinations = renderers,
				Processors = new List<AbstractProcessor> { _pbfxGridProcessor, transformationProcessor, _pbfxShadeProcessor }
			};
			_tpaGraph = new RenderGraph {
				Source = _tpaGrabber,
				Destinations = renderers,
				Processors = new List<AbstractProcessor> { transformationProcessor, _tpaShadeProcessor }
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
			_pbfxGridProcessor.WhenProcessed.Subscribe(bmp => {
				ProcessedGrid.Dispatcher.Invoke(() => {
					ProcessedGrid.Source = bmp;
				});
			});
			transformationProcessor.WhenProcessed.Subscribe(bmp => {
				ProcessedGrid.Dispatcher.Invoke(() => {
					ProcessedGrid.Source = bmp;
				});
			});
		}

		private void HotKey(object sender, KeyEventArgs e)
		{
			var gridProcessor = _tpaGraph.IsRendering ? _tpaGridProcessor : _pbfxGridProcessor;
			var shadingProcessor = _tpaGraph.IsRendering ? _tpaShadeProcessor : _pbfxShadeProcessor;

			if (e.IsDown) {
				switch (e.Key) {
					case Key.PageUp:
						gridProcessor.Spacing += 0.1;
						Console.AppendText("Grid padding: " + gridProcessor.Spacing + "\n");
						break;

					case Key.PageDown:
						if (gridProcessor.Spacing > 0) {
							gridProcessor.Spacing -= 0.1;
							Console.AppendText("Grid padding: " + gridProcessor.Spacing + "\n");
						}
						break;

					case Key.Q:
						shadingProcessor.Intensity -= 0.1;
						Console.AppendText("Intensity: " + shadingProcessor.Intensity + "\n");
						break;
					case Key.W:
						shadingProcessor.Intensity += 0.1;
						Console.AppendText("Intensity: " + shadingProcessor.Intensity + "\n");
						break;

					case Key.E:
						shadingProcessor.Brightness -= 0.1;
						Console.AppendText("Lightness: " + shadingProcessor.Brightness + "\n");
						break;
					case Key.R:
						shadingProcessor.Brightness += 0.1;
						Console.AppendText("Lightness: " + shadingProcessor.Brightness + "\n");
						break;

					case Key.A:
						_pbfxGrabber.CropLeft -= 1;
						Console.AppendText("Crop left: " + _pbfxGrabber.CropLeft + "\n");
						break;
					case Key.S:
						_pbfxGrabber.CropLeft += 1;
						Console.AppendText("Crop left: " + _pbfxGrabber.CropLeft + "\n");
						break;
					case Key.D:
						_pbfxGrabber.CropRight += 1;
						Console.AppendText("Crop right: " + _pbfxGrabber.CropRight + "\n");
						break;
					case Key.F:
						_pbfxGrabber.CropRight -= 1;
						Console.AppendText("Crop right: " + _pbfxGrabber.CropRight + "\n");
						break;

					case Key.Y:
						_pbfxGrabber.CropTop -= 1;
						Console.AppendText("Crop top: " + _pbfxGrabber.CropTop + "\n");
						break;
					case Key.X:
						_pbfxGrabber.CropTop += 1;
						Console.AppendText("Crop left: " + _pbfxGrabber.CropTop + "\n");
						break;
					case Key.C:
						_pbfxGrabber.CropBottom += 1;
						Console.AppendText("Crop bottom: " + _pbfxGrabber.CropBottom + "\n");
						break;
					case Key.V:
						_pbfxGrabber.CropBottom -= 1;
						Console.AppendText("Crop bottom: " + _pbfxGrabber.CropBottom + "\n");
						break;
				}
			}
		}

		private void BitmapButton_Click(object sender, RoutedEventArgs e)
		{
			if (_currentRenderer != null) {
				_currentRenderer.Dispose();;
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
				Console.AppendText("Already capturing Pinball FX2. Launch a game if you don't see anything!\n");
				return;
			}
			_grabberWindow.Hide();
			Console.AppendText("Starting pulling frames from Pinball FX2.\n");
			SwitchGraph(_pbfxGraph);
		}

		private void TpaButton_Click(object sender, RoutedEventArgs e)
		{
			// ignore if already runnong
			if (_tpaGraph.IsRendering) {
				Console.AppendText("Already capturing the Pinball Arcade. Launch a game if you don't see anything!\n");
				return;
			}
			_grabberWindow.Hide();
			Console.AppendText("Starting pulling frames from the Pinball Arcade.\n");
			SwitchGraph(_tpaGraph);
		}

		private void ScreenButton_Click(object sender, RoutedEventArgs e)
		{
			// this one we stop if it's running and button was clicked again
			if (_screenGraph.IsRendering) {
				_grabberWindow.Hide();
				_currentRenderer.Dispose();
				_currentSource.Dispose();
				_currentRenderer = null;
				Console.AppendText("Stopped pulling frames from desktop.\n");
				return;
			}

			_grabberWindow.Show();
			SwitchGraph(_screenGraph);
			Console.AppendText("Started pulling frames from desktop.\n");
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
			_currentRenderer = graph.StartRendering(err => {
				Console.Text = err.Message + "\n";
				_currentRenderer?.Dispose();
				_currentSource?.Dispose();
				_currentRenderer = null;
			});
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
