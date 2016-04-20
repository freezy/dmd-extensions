using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using App;
using LibDmd.Output;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using NLog;
using static System.Windows.Threading.Dispatcher;
using static Console.Common.BaseOptions.DestinationType;

namespace Console.Common
{
	abstract class BaseCommand : IDisposable
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public abstract void Execute();

		protected List<IFrameDestination> GetRenderers(BaseOptions options)
		{
			var renderers = new List<IFrameDestination>();
			switch (options.Destination) {
				case Auto:
					renderers = GetAvailableRenderers(options);
					break;

				case PinDMDv1:
					var pinDmd1 = PinDmd1.GetInstance();
					if (pinDmd1.IsAvailable) {
						renderers.Add(pinDmd1);
						Logger.Info("Added PinDMDv1 renderer.");
					} else {
						throw new DeviceNotAvailableException(PinDMDv1.ToString());
					}
					break;

				case PinDMDv2:
					var pinDmd2 = PinDmd2.GetInstance();
					if (pinDmd2.IsAvailable) {
						renderers.Add(pinDmd2);
						Logger.Info("Added PinDMDv2 renderer.");
					} else {
						throw new DeviceNotAvailableException(PinDMDv2.ToString());
					}
					break;

				case PinDMDv3:
					var pinDmd3 = PinDmd3.GetInstance();
					if (pinDmd3.IsAvailable) {
						renderers.Add(pinDmd3);
						Logger.Info("Added PinDMDv3 renderer.");
					} else {
						throw new DeviceNotAvailableException(PinDMDv3.ToString());
					}
					break;
					
				case PIN2DMD:
					var pin2Dmd = Pin2Dmd.GetInstance();
					if (pin2Dmd.IsAvailable) {
						renderers.Add(pin2Dmd);
						Logger.Info("Added PIN2DMD renderer.");
					} else {
						throw new DeviceNotAvailableException(PIN2DMD.ToString());
					}
					break;

				case Virtual:
					renderers.Add(ShowVirtualDmd());
					Logger.Info("Added VirtualDMD renderer.");
					
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
			if (renderers.Count == 0) {
				throw new NoRenderersAvailableException();
			}
			return renderers;
		}

		protected List<IFrameDestination> GetAvailableRenderers(BaseOptions options)
		{
			var renderers = new List<IFrameDestination>();
			try {
				var pinDmd1 = PinDmd1.GetInstance();
				var pinDmd2 = PinDmd2.GetInstance();
				var pinDmd3 = PinDmd3.GetInstance();
				var pin2Dmd = Pin2Dmd.GetInstance();

				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Logger.Info("Added PinDMDv1 renderer.");
				}
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Logger.Info("Added PinDMDv2 renderer.");
				}
				if (pinDmd3.IsAvailable) {
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
				}
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");
				}
				if (!options.NoVirtualDmd) {
					renderers.Add(ShowVirtualDmd());
					Logger.Info("Added VirtualDMD renderer.");
				} else {
					Logger.Debug("VirtualDMD disabled.");
				}

			} catch (DllNotFoundException e) {
				Logger.Error(e, "Error loading DLL.");
				return renderers;
			}
			return renderers;
		}

		private static IFrameDestination ShowVirtualDmd()
		{
			var dmd = new VirtualDmd();
			var thread = new Thread(() => {
				
				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));

				// When the window closes, shut down the dispatcher
				dmd.Closed += (s, e) => CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
				dmd.Dispatcher.Invoke(() => {
					dmd.Show();
				});

				// Start the Dispatcher Processing
				Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			return dmd.Dmd;
		}

		public virtual void Dispose()
		{
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
