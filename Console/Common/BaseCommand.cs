using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using LibDmd;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Output;
using LibDmd.Output.Network;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Output.Pixelcade;
using LibDmd.Output.Virtual.AlphaNumeric;
using NLog;
using static System.Windows.Threading.Dispatcher;
using static DmdExt.Common.BaseOptions.DestinationType;

namespace DmdExt.Common
{
	internal abstract class BaseCommand : IDisposable
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private RenderGraphCollection _graphs;
		private IConfiguration _config;

		protected abstract void CreateRenderGraphs(RenderGraphCollection graphs, HashSet<string> reportingTags);

		public RenderGraphCollection GetRenderGraphs(HashSet<string> reportingTags)
		{
			if (_graphs == null) {
				_graphs = new RenderGraphCollection();
				CreateRenderGraphs(_graphs, reportingTags);
			}
			return _graphs;
		}

		protected List<IDestination> GetRenderers(IConfiguration config, HashSet<string> reportingTags, int[] position = null)
		{
			var renderers = new List<IDestination>();
			if (config.PinDmd1.Enabled) {
				var pinDmd1 = PinDmd1.GetInstance();
				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Logger.Info("Added PinDMDv1 renderer.");
					reportingTags.Add("Out:PinDMDv1");
				} else {
					Logger.Warn("Device {0} is not available.", PinDMDv1);
				}
			}

			if (config.PinDmd2.Enabled) {
				var pinDmd2 = PinDmd2.GetInstance();
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Logger.Info("Added PinDMDv2 renderer.");
					reportingTags.Add("Out:PinDMDv2");
				} else {
					Logger.Warn("Device {0} is not available.", PinDMDv2);
				}
			}

			if (config.PinDmd3.Enabled) {
				var pinDmd3 = PinDmd3.GetInstance(config.PinDmd3.Port);
				if (pinDmd3.IsAvailable) {
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
					reportingTags.Add("Out:PinDMDv3");
				} else {
					Logger.Warn("Device {0} is not available.", PinDMDv3);
				}
			}

			if (config.Pin2Dmd.Enabled) {
				var pin2Dmd = Pin2Dmd.GetInstance(config.Pin2Dmd.Delay);
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");
					reportingTags.Add("Out:PIN2DMD");
				} else {
					Logger.Warn("Device {0} is not available.", PIN2DMD);
				}
			}

			if (config.Pin2Dmd.Enabled)
			{
				var pin2DmdXl = Pin2DmdXl.GetInstance(config.Pin2Dmd.Delay);
				if (pin2DmdXl.IsAvailable)
				{
					renderers.Add(pin2DmdXl);
					Logger.Info("Added PIN2DMD XL renderer.");
					reportingTags.Add("Out:PIN2DMDXL");
				}
				else
				{
					Logger.Warn("Device {0} is not available.", PIN2DMDXL);
				}
			}

			if (config.Pin2Dmd.Enabled)
			{
				var pin2DmdHd = Pin2DmdHd.GetInstance(config.Pin2Dmd.Delay);
				if (pin2DmdHd.IsAvailable)
				{
					renderers.Add(pin2DmdHd);
					Logger.Info("Added PIN2DMD HD renderer.");
					reportingTags.Add("Out:PIN2DMDHD");
				}
				else
				{
					Logger.Warn("Device {0} is not available.", PIN2DMDHD);
				}
			}

			if (config.Pixelcade.Enabled) {
				var pixelcade = Pixelcade.GetInstance(config.Pixelcade.Port, config.Pixelcade.ColorMatrix);
				if (pixelcade.IsAvailable) {
					renderers.Add(pixelcade);
					Logger.Info("Added Pixelcade renderer.");
					reportingTags.Add("Out:Pixelcade");
				} else {
					Logger.Warn("Device Pixelcade is not available.");
				}
			}

			if (config.VirtualDmd.Enabled) {
				renderers.Add(ShowVirtualDmd(config, position));
				Logger.Info("Added virtual DMD renderer.");
				reportingTags.Add("Out:VirtualDmd");
			}

			if (config.VirtualAlphaNumericDisplay.Enabled) {
				renderers.Add(VirtualAlphanumericDestination.GetInstance(CurrentDispatcher, config.VirtualAlphaNumericDisplay.Style, config as Configuration));
				Logger.Info("Added virtual Alphanumeric renderer.");
				reportingTags.Add("Out:VirtualAlphaNum");
			}

			if (config.NetworkStream.Enabled) {
				try {
					renderers.Add(NetworkStream.GetInstance(config.NetworkStream));
					Logger.Info("Added websocket client renderer.");
					reportingTags.Add("Out:NetworkStream");

				} catch (Exception e) {
					Logger.Warn("Network stream disabled: {0}", e.Message);
				}
			}

			if (renderers.Count == 0) {
				throw new NoRenderersAvailableException();
			}

			foreach (var renderer in renderers) {
				var rgb24 = renderer as IRgb24Destination;
				rgb24?.SetColor(config.Global.DmdColor);
			}
			_config = config;
			return renderers;
		}

		private static IDestination ShowVirtualDmd(IConfiguration config, int[] position)
		{
			var dmd = new VirtualDmd {
				Left = config.VirtualDmd.Left,
				Top = config.VirtualDmd.Top,
				Width = config.VirtualDmd.Width,
				Height = config.VirtualDmd.Height
			};
			dmd.Setup(config as Configuration, config is Configuration iniConfig ? iniConfig.GameName : null);
			var thread = new Thread(() => {

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));

				dmd.Dispatcher.Invoke(() => {
					dmd.Dmd.Init();
					if(position != null)
						dmd.Dmd.SetDimensions(position[2]- position[0], position[3]-position[1]);
					dmd.Show();
				});

				// Start the Dispatcher Processing
				Run();
			});

			// On closing the window, shut down the dispatcher associated with the thread.
			// This will allow the thread to exit after the window is closed and all of
			// the events resulting from the window close have been processed.
			dmd.Closed += (s, e) => Dispatcher.FromThread(thread).BeginInvokeShutdown(DispatcherPriority.Background);

			// run the thread
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			return dmd.VirtualControl;
		}

		public void Execute(HashSet<string> reportingTags, Action onCompleted, Action<Exception> onError)
		{
			GetRenderGraphs(reportingTags).Init().StartRendering(onCompleted, onError);
		}

		public void Dispose()
		{
			if (_config == null || !_config.Global.NoClear) {
				_graphs?.ClearDisplay();
			}
			_graphs?.Dispose();
		}
	}

	public class DeviceNotAvailableException : Exception
	{
		public DeviceNotAvailableException(string message) : base(message)
		{
		}
	}

	public class NoRenderersAvailableException : Exception
	{
	}
}
