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
using LibDmd.Output.ZeDMD;
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

		protected List<IDestination> GetRenderers(IConfiguration config, HashSet<string> reportingTags)
		{
			var renderers = new List<IDestination>();
			if (config.PinDmd1.Enabled) {
				var pinDmd1 = PinDmd1.GetInstance();
				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Logger.Info("Added PinDMDv1 renderer.");
					reportingTags.Add("Out:PinDMDv1");
					Analytics.AddDestination(pinDmd1);
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
					Analytics.AddDestination(pinDmd2);
				} else {
					Logger.Warn("Device {0} is not available.", PinDMDv2);
				}
			}

			if (config.PinDmd3.Enabled)
			{
				var pinDmd3 = PinDmd3.GetInstance(config.PinDmd3.Port);
				if (pinDmd3.IsAvailable)
				{
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
					reportingTags.Add("Out:PinDMDv3");
					Analytics.AddDestination(pinDmd3);
				}
				else
				{
					Logger.Warn("Device {0} is not available.", PinDMDv3);
				}
			}

			if (config.ZeDMD.Enabled)
			{
				var zeDMD = ZeDMD.GetInstance();
				if (zeDMD.IsAvailable)
				{
					renderers.Add(zeDMD);
					Logger.Info("Added ZeDMD renderer.");
					reportingTags.Add("Out:ZeDMD");
					Analytics.AddDestination(zeDMD);
				}
				else
				{
					Logger.Warn("Device {0} is not available.", zeDMD);
				}
			}

			if (config.Pin2Dmd.Enabled) {
				var pin2Dmd = Pin2Dmd.GetInstance(config.Pin2Dmd.Delay);
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");
					reportingTags.Add("Out:PIN2DMD");
					Analytics.AddDestination(pin2Dmd);
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
					Analytics.AddDestination(pin2DmdXl);
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
					Analytics.AddDestination(pin2DmdHd);
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
					Analytics.AddDestination(pixelcade);
				} else {
					Logger.Warn("Device Pixelcade is not available.");
				}
			}

			if (config.VirtualDmd.Enabled) {
				var virtualDmd = ShowVirtualDmd(config);
				renderers.Add(virtualDmd);
				Logger.Info("Added virtual DMD renderer.");
				reportingTags.Add("Out:VirtualDmd");
				Analytics.AddDestination(virtualDmd);
			}

			if (config.VirtualAlphaNumericDisplay.Enabled) {
				var virtualAlphaNum = VirtualAlphanumericDestination.GetInstance(CurrentDispatcher,
					config.VirtualAlphaNumericDisplay.Style, config as Configuration);
				renderers.Add(virtualAlphaNum);
				Logger.Info("Added virtual Alphanumeric renderer.");
				reportingTags.Add("Out:VirtualAlphaNum");
				Analytics.AddDestination(virtualAlphaNum);
			}

			if (config.NetworkStream.Enabled) {
				try {
					var networkStream = NetworkStream.GetInstance(config.NetworkStream);
					renderers.Add(networkStream);
					Logger.Info("Added websocket client renderer.");
					reportingTags.Add("Out:NetworkStream");
					Analytics.AddDestination(networkStream);

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

		private static IDestination ShowVirtualDmd(IConfiguration config)
		{
			var dmd = new VirtualDmd {
				Left = config.VirtualDmd.Left,
				Top = config.VirtualDmd.Top,
				Width = config.VirtualDmd.Width,
				Height = config.VirtualDmd.Height,
				IgnoreAspectRatio = config.VirtualDmd.IgnoreAr
			};
			dmd.Setup(config as Configuration, config is Configuration iniConfig ? iniConfig.GameName : null);
			var thread = new Thread(() => {

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));

				dmd.Dispatcher.Invoke(() => {
					dmd.Dmd.Init();
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

		public virtual void Dispose()
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
