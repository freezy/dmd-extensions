using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using LibDmd.Common;
using LibDmd.DmdDevice;
using NLog;
using SkiaSharp.Extended.Svg;

namespace LibDmd.Output.Virtual
{
	public class VirtualAlphanumericDestination : IAlphaNumericDestination
	{
		public string Name => "Virtual Alphanumeric Renderer";
		public bool IsAvailable => true;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static VirtualAlphanumericDestination _instance;

		private readonly Dispatcher _dispatcher;

		private readonly Dictionary<int, VirtualAlphaNumericDisplay> _displays = new Dictionary<int, VirtualAlphaNumericDisplay>();
		private readonly Dictionary<int, ushort[]> _droppedData = new Dictionary<int, ushort[]>();
		private NumericalLayout _currentLayout = NumericalLayout.None;

		private VirtualAlphanumericDestination(Dispatcher dispatcher)
		{
			_dispatcher = dispatcher;
		}

		public static VirtualAlphanumericDestination GetInstance(Dispatcher dispatcher)
		{
			return _instance ?? (_instance = new VirtualAlphanumericDestination(dispatcher));
		}

		public void Init()
		{
			Logger.Info("{0} initialized.", Name);
		}

		public void RenderAlphaNumeric(AlphaNumericFrame frame)
		{
			if (_currentLayout == NumericalLayout.None || _currentLayout != frame.SegmentLayout) {
				ShowDisplays(frame.SegmentLayout);
				_currentLayout = frame.SegmentLayout;
			}
			//Logger.Info("New frame type {0}", frame.SegmentLayout);

			switch (frame.SegmentLayout) {

				case NumericalLayout.__2x6Num_2x6Num_4x1Num:
					SendToDisplay(0, new ArraySegment<ushort>(frame.SegmentData, 0, 6).ToArray());
					SendToDisplay(1, new ArraySegment<ushort>(frame.SegmentData, 6, 6).ToArray());
					/*SendToDisplay(2, new ArraySegment<ushort>(frame.SegmentData, 12, 6).ToArray());
					SendToDisplay(3, new ArraySegment<ushort>(frame.SegmentData, 18, 6).ToArray());
					SendToDisplay(5, new ArraySegment<ushort>(frame.SegmentData, 24, 2).ToArray());
					SendToDisplay(6, new ArraySegment<ushort>(frame.SegmentData, 26, 2).ToArray());*/
					break;

				case NumericalLayout.__2x20Alpha:
					SendToDisplay(0, new ArraySegment<ushort>(frame.SegmentData, 0, 20).ToArray());
					//SendToDisplay(1, new ArraySegment<ushort>(frame.SegmentData, 20, 20).ToArray());
					break;
			}
		}

		private void SendToDisplay(int displayNumber, ushort[] data)
		{
			if (_displays.ContainsKey(displayNumber)) {
				Logger.Debug("Sending data to display {0}...", displayNumber);
				_displays[displayNumber].AlphaNumericDisplay.RenderSegments(data);

			} else {
				Logger.Debug("Got data for display {0}, but it's now created yet, so saving for later.", displayNumber);
				_droppedData[displayNumber] = data;
			}
		}

		private void ShowDisplays(NumericalLayout layout)
		{
			// todo teardown current if open
			var res = AlphaNumericResources.GetInstance();
			switch (layout) {
				case NumericalLayout.__2x6Num_2x6Num_4x1Num:
					ShowDisplay(0, 6, 1, SegmentType.Numeric);
					ShowDisplay(1, 6, 1, SegmentType.Numeric);
					/*ShowDisplay(2, 6, 1, SegmentType.Numeric);
					ShowDisplay(3, 6, 1, SegmentType.Numeric);
					ShowDisplay(4, 2, 1, SegmentType.Numeric);
					ShowDisplay(5, 2, 1, SegmentType.Numeric);*/
					break;

				case NumericalLayout.__2x20Alpha:
					ShowDisplay(0, 20, 1, SegmentType.Alphanumeric);
					//ShowDisplay(1, 20, 1, SegmentType.Alphanumeric);
					break;
			}
		}

		public void ClearDisplay()
		{
			Logger.Info("Clearing Display");
		}

		public void Dispose()
		{
			try {
				foreach (var display in _displays.Values) {
					display.Dispatcher.Invoke(() => {
						display.Hide();
					});
				}
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Could not hide DMD because task was already canceled.");
			}
		}

		private void ShowDisplay(int displayNumber, int numChars, int numLines, SegmentType type)
		{
			_dispatcher.Invoke(delegate {
				var display = new VirtualAlphaNumericDisplay(numChars, numLines, type);
				_displays[displayNumber] = display;
				var thread = new Thread(() => {
					SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(_dispatcher));
					display.Dispatcher.Invoke(() => {
						display.AlphaNumericDisplay.Init();
						display.Show();

						if (_droppedData.ContainsKey(displayNumber)) {
							Logger.Debug("Sending dropped frame to freshly created display {0}...", displayNumber);
							display.AlphaNumericDisplay.RenderSegments(_droppedData[displayNumber]);
							_droppedData.Remove(displayNumber);
						}
					});
					Dispatcher.Run();
				});
				display.Closed += (s, e) => Dispatcher.FromThread(thread).BeginInvokeShutdown(DispatcherPriority.Background);
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
			});
		}
	}

}
