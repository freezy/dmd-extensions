﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using LibDmd;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Output;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Output.Virtual.AlphaNumeric;
using LibDmd.Output.Virtual.SkiaDmd;
using NLog;
using static System.Windows.Threading.Dispatcher;
using static DmdExt.Common.BaseOptions.DestinationType;

namespace DmdExt.Common
{
	internal abstract class BaseCommand : IDisposable
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private IRenderer _graph;
		private IConfiguration _config;

		protected abstract IRenderer CreateRenderGraph();

		public IRenderer GetRenderGraph()
		{
			return _graph ?? (_graph = CreateRenderGraph());
		}

		protected List<IDestination> GetRenderers(IConfiguration config)
		{
			var renderers = new List<IDestination>();
			if (config.PinDmd1.Enabled) {
				var pinDmd1 = PinDmd1.GetInstance();
				if (pinDmd1.IsAvailable) {
					renderers.Add(pinDmd1);
					Logger.Info("Added PinDMDv1 renderer.");
				} else {
					Logger.Warn("Device {0} is not available.", PinDMDv1);
				}
			}

			if (config.PinDmd2.Enabled) {
				var pinDmd2 = PinDmd2.GetInstance();
				if (pinDmd2.IsAvailable) {
					renderers.Add(pinDmd2);
					Logger.Info("Added PinDMDv2 renderer.");
				} else {
					Logger.Warn("Device {0} is not available.", PinDMDv2);
				}
			}

			if (config.PinDmd3.Enabled) {
				var pinDmd3 = PinDmd3.GetInstance(config.PinDmd3.Port);
				if (pinDmd3.IsAvailable) {
					renderers.Add(pinDmd3);
					Logger.Info("Added PinDMDv3 renderer.");
				} else {
					Logger.Warn("Device {0} is not available.", PinDMDv3);
				}
			}

			if (config.Pin2Dmd.Enabled) {
				var pin2Dmd = Pin2Dmd.GetInstance(config.Pin2Dmd.Delay);
				if (pin2Dmd.IsAvailable) {
					renderers.Add(pin2Dmd);
					Logger.Info("Added PIN2DMD renderer.");
				} else {
					Logger.Warn("Device {0} is not available.", PIN2DMD);
				}
			}

			if (config.VirtualDmd.Enabled) {
				renderers.Add(ShowVirtualDmd(config));
				Logger.Info("Added virtual DMD renderer.");
			}

			if (config.VirtualAlphaNumericDisplay.Enabled) {
				renderers.Add(VirtualAlphanumericDestination.GetInstance(CurrentDispatcher, config.VirtualAlphaNumericDisplay.Style, config as Configuration));
				Logger.Info("Added virtual Alphanumeric renderer.");
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
			//var dmd = new SkiaDmdControl(new DmdStyleDefinition(), config as Configuration);
			var dmd = new VirtualSkiaDmd(new DmdStyleDefinition(), config as Configuration) {
				AlwaysOnTop = config.VirtualDmd.StayOnTop,
				GripColor = config.VirtualDmd.HideGrip ? Brushes.Transparent : Brushes.White,
				Left = config.VirtualDmd.Left,
				Top = config.VirtualDmd.Top,
				Width = config.VirtualDmd.Width,
				Height = config.VirtualDmd.Height,
				IgnoreAspectRatio = config.VirtualDmd.IgnoreAr,
				//DotSize = config.DotSize
			};
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

		

		public void Execute(Action onCompleted, Action<Exception> onError)
		{
			GetRenderGraph().Init().StartRendering(onCompleted, onError);
		}

		public void Dispose()
		{
			if (_config == null || !_config.Global.NoClear) {
				_graph?.ClearDisplay();
			}
			_graph?.Dispose();
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
