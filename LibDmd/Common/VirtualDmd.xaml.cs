using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual;
using LibDmd.Output.Virtual.Dmd;
using NLog;

namespace LibDmd.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualDmd : VirtualDisplay
	{
		public override IVirtualControl VirtualControl => Dmd;

		private readonly VirtualDmdConfig _config;
		private readonly MenuItem _saveGamePosItem;
		private IDisposable _settingSubscription;

		public double DotSize
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.DotSize = value;
				}
			}
		}

		public double Brightness
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.Brightness = value;
				}
			}
		}

		public double DotGlow
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.DotGlow = value;
				}
			}
		}

		public double BackGlow
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.BackGlow = value;
				}
			}
		}

		public string GlassTexture
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.Glass = value;
				}
			}
		}

		public System.Windows.Thickness GlassPadding
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.GlassPad = value;
				}
			}
		}

		public Color GlassColor
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.GlassColor = value;
				}
			}
		}

		public string FrameTexture
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.Frame = value;
				}
			}
		}

		public System.Windows.Thickness FramePadding
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.FramePad = value;
				}
			}
		}


		public VirtualDmd(VirtualDmdConfig config = null, string gameName = null) : base()
		{
			InitializeComponent();
			Initialize();
			if (config != null)
			{
				_config = config;

				ParentGrid.ContextMenu = new ContextMenu();

				var saveGlobalPos = new MenuItem();
				saveGlobalPos.Click += SavePositionGlobally;
				saveGlobalPos.Header = "Save position globally";
				ParentGrid.ContextMenu.Items.Add(saveGlobalPos);

				if (gameName != null)
				{
					_saveGamePosItem = new MenuItem();
					_saveGamePosItem.Click += SavePositionGame;
					_saveGamePosItem.Header = "Save position for \"" + gameName + "\"";
					ParentGrid.ContextMenu.Items.Add(_saveGamePosItem);
				}

				ParentGrid.ContextMenu.Items.Add(new Separator());

				var openSettings = new MenuItem();
				openSettings.Click += OpenSettings;
				openSettings.Header = "Open display settings";
				ParentGrid.ContextMenu.Items.Add(openSettings);

				ParentGrid.ContextMenu.Items.Add(new Separator());

				var toggleAspect = new MenuItem();
				toggleAspect.Click += ToggleAspectRatio;
				toggleAspect.Header = "Ignore Aspect Ratio";
				toggleAspect.IsCheckable = true;
				ParentGrid.ContextMenu.Items.Add(toggleAspect);

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
			_config.SetPosition(new VirtualDisplayPosition(Left, Top, Width, Height), false);
		}

		private void SavePositionGame(object sender, RoutedEventArgs e)
		{
			_config.SetPosition(new VirtualDisplayPosition(Left, Top, Width, Height), true);
		}

		private void OpenSettings(object sender, RoutedEventArgs e)
		{
			var settingWindow = new DmdSettings(_config);
			_settingSubscription = settingWindow.OnConfigUpdated.Subscribe(config =>
			{
				Logger.Info("Applying new config to DMD.");
				DotSize = config.DotSize;
				IgnoreAspectRatio = config.IgnoreAr;
				Brightness = config.Brightness;
				DotGlow = config.DotGlow;
				BackGlow = config.BackGlow;
				GlassTexture = config.GlassTexture;
				GlassPadding = config.GlassPadding;
				GlassColor = config.GlassColor;
				FrameTexture = config.FrameTexture;
				FramePadding = config.FramePadding;
				GlassPadding = config.GlassPadding;
			});
			settingWindow.Show();
		}

		private void ToggleAspectRatio(object sender, RoutedEventArgs e)
		{
			IgnoreAspectRatio = (sender as MenuItem).IsChecked;
			_config.SetIgnoreAspectRatio(IgnoreAspectRatio);
		}

		internal void SetGameName(string gameName)
		{
			if (_saveGamePosItem != null)
			{
				_saveGamePosItem.Header = "Save position for \"" + gameName + "\"";
			}
		}
	}
}
