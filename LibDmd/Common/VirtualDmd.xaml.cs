using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual.Dmd;

namespace LibDmd.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualDmd : VirtualDisplay
	{
		public override IVirtualControl VirtualControl => Dmd;

		private Configuration _config;
		private VirtualDmdConfig _dmdConfig;
		private IDisposable _settingSubscription;

		public VirtualDmd() : base()
		{
			InitializeComponent();
			Initialize();
		}

		public void Setup(Configuration config = null, string gameName = null)
		{
			_config = config;
			_dmdConfig = _config?.VirtualDmd as VirtualDmdConfig;

			if (_dmdConfig != null) {

				ParentGrid.ContextMenu = new ContextMenu();

				var saveGlobalPos = new MenuItem();
				saveGlobalPos.Click += SavePositionGlobally;
				saveGlobalPos.Header = "Save position globally";
				ParentGrid.ContextMenu.Items.Add(saveGlobalPos);

				if (gameName != null) {
					var saveGamePosItem = new MenuItem();
					saveGamePosItem.Click += SavePositionGame;
					saveGamePosItem.Header = "Save position for \"" + gameName + "\"";
					ParentGrid.ContextMenu.Items.Add(saveGamePosItem);
				}

				ParentGrid.ContextMenu.Items.Add(new Separator());

				var toggleAspect = new MenuItem();
				toggleAspect.Click += ToggleAspectRatio;
				toggleAspect.Header = "Ignore Aspect Ratio";
				toggleAspect.IsCheckable = true;
				ParentGrid.ContextMenu.Items.Add(toggleAspect);

				ParentGrid.ContextMenu.Items.Add(new Separator());

				var openSettings = new MenuItem();
				openSettings.Click += OpenSettings;
				openSettings.Header = "Customize Style";
				ParentGrid.ContextMenu.Items.Add(openSettings);

			} else {
				ParentGrid.ContextMenu = null;
			}

			if (_config != null) {
				Dmd.SetStyle(_config.VirtualDmd.Style, _config.DataPath);
				IgnoreAspectRatio = _config.VirtualDmd.IgnoreAr;
				AlwaysOnTop = _config.VirtualDmd.StayOnTop;
			}
		}


		public void Dispose()
		{
			try
			{
				_settingSubscription?.Dispose();
			}
			catch (TaskCanceledException e)
			{
				Logger.Warn(e, "Could not hide DMD because task was already canceled.");
			}
		}


		private void SavePositionGlobally(object sender, RoutedEventArgs e)
		{
			_dmdConfig?.SetPosition(new VirtualDisplayPosition(Left, Top, Width, Height), false);
		}

		private void SavePositionGame(object sender, RoutedEventArgs e)
		{
			_dmdConfig?.SetPosition(new VirtualDisplayPosition(Left, Top, Width, Height), true);
		}

		private void OpenSettings(object sender, RoutedEventArgs e)
		{
			var settingWindow = new DmdSettings(_dmdConfig.Style, _config);
			_settingSubscription = settingWindow.OnConfigUpdated.Subscribe(style => {
				Dmd.SetStyle(style, _config.DataPath);
			});
			settingWindow.Show();
		}

		private void ToggleAspectRatio(object sender, RoutedEventArgs e)
		{
			IgnoreAspectRatio = (sender as MenuItem).IsChecked;
			_dmdConfig?.SetIgnoreAspectRatio(IgnoreAspectRatio);
		}
	}
}
