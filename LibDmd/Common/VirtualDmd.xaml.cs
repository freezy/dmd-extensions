using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual.Dmd;
using Microsoft.Win32;
using NLog;

namespace LibDmd.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualDmd : VirtualDisplay
	{
		public override IVirtualControl VirtualControl => Dmd;

		private IVirtualDmdConfig _config;
		private string _gameName;
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

		public double DotRounding
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.DotRounding = value;
				}
			}
		}

		public Color UnlitDot
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.UnlitDot = value;
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

		public Thickness GlassPadding
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

		public Thickness FramePadding
		{
			set
			{
				if (Dmd != null)
				{
					Dmd.FramePad = value;
				}
			}
		}


		public VirtualDmd(IVirtualDmdConfig config = null, string gameName = null) : base()
		{
			InitializeComponent();
			Initialize();
			Setup(config, gameName);
		}

		public void Setup(IVirtualDmdConfig config = null, string gameName = null)
		{
			_config = config;
			_gameName = gameName;

			if (config != null)
			{
				ParentGrid.ContextMenu = new ContextMenu();

				if (config is VirtualDmdConfig)
				{
					var saveGlobalPos = new MenuItem();
					saveGlobalPos.Click += SavePositionGlobally;
					saveGlobalPos.Header = "Save position globally";
					ParentGrid.ContextMenu.Items.Add(saveGlobalPos);

					if (gameName != null)
					{
						var saveGamePosItem = new MenuItem();
						saveGamePosItem.Click += SavePositionGame;
						saveGamePosItem.Header = "Save position for \"" + gameName + "\"";
						ParentGrid.ContextMenu.Items.Add(saveGamePosItem);
					}

					ParentGrid.ContextMenu.Items.Add(new Separator());
				}

				var openSettings = new MenuItem();
				openSettings.Click += OpenSettings;
				openSettings.Header = "Open display settings";
				ParentGrid.ContextMenu.Items.Add(openSettings);

				if (config is VirtualDmdConfig)
				{
					ParentGrid.ContextMenu.Items.Add(new Separator());

					var toggleAspect = new MenuItem();
					toggleAspect.Click += ToggleAspectRatio;
					toggleAspect.Header = "Ignore Aspect Ratio";
					toggleAspect.IsCheckable = true;
					ParentGrid.ContextMenu.Items.Add(toggleAspect);
				}

				ApplyConfig(_config, _gameName);
			}
			else
			{
				ParentGrid.ContextMenu = null;
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
			((VirtualDmdConfig)_config).SetPosition(new VirtualDisplayPosition(Left, Top, Width, Height), false);
		}

		private void SavePositionGame(object sender, RoutedEventArgs e)
		{
			((VirtualDmdConfig)_config).SetPosition(new VirtualDisplayPosition(Left, Top, Width, Height), true);
		}

		private void OpenSettings(object sender, RoutedEventArgs e)
		{
			var settingWindow = new DmdSettings(_config);
			_settingSubscription = settingWindow.OnConfigUpdated.Subscribe(config =>
			{
				ApplyConfig(config, _gameName);
			});
			settingWindow.Show();
		}

		private void ApplyConfig(IVirtualDmdConfig config, string gameName)
		{
			Logger.Info("Applying config to DMD.");
			DotSize = config.DotSize;
			DotRounding = config.DotRounding;
			UnlitDot = config.UnlitDot;
			IgnoreAspectRatio = config.IgnoreAr;
			Brightness = config.Brightness;
			DotGlow = config.DotGlow;
			BackGlow = config.BackGlow;
			GlassTexture = config.GlassTexture;
			GlassPadding = config.GlassPadding;
			GlassColor = config.GlassColor;
			FrameTexture = config.FrameTexture;
			FramePadding = config.FramePadding;
			AlwaysOnTop = config.StayOnTop;

			// find the game's dmd position in VPM's registry
			if (config.UseRegistryPosition)
			{
				try
				{
					var regPath = @"Software\Freeware\Visual PinMame\" + (gameName.Length > 0 ? gameName : "default");
					var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
					key = key.OpenSubKey(regPath);

					if (key == null)
					{
						// couldn't find the value in the 32-bit view so grab the 64-bit view
						key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
						key = key.OpenSubKey(regPath);
					}

					if (key != null)
					{
						var values = key.GetValueNames();
						if (!values.Contains("dmd_pos_x") && values.Contains("dmd_pos_y") && values.Contains("dmd_width") && values.Contains("dmd_height"))
						{
							Logger.Warn("Not all values were found at HKEY_CURRENT_USER\\{0}. Trying default.", regPath);
							key?.Dispose();
							regPath = @"Software\Freeware\Visual PinMame\default";
							key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
							key = key.OpenSubKey(regPath);
						}
					}
					// still null?
					if (key != null)
					{
						var values = key.GetValueNames();
						if (values.Contains("dmd_pos_x") && values.Contains("dmd_pos_y") && values.Contains("dmd_width") && values.Contains("dmd_height"))
						{
							SetVirtualDmdDefaultPosition(
								Convert.ToInt64(key.GetValue("dmd_pos_x").ToString()),
								Convert.ToInt64(key.GetValue("dmd_pos_y").ToString()),
								Convert.ToInt64(key.GetValue("dmd_width").ToString()),
								Convert.ToInt64(key.GetValue("dmd_height").ToString())
							);
						}
						else
						{
							Logger.Warn("Ignoring VPM registry for DMD position because not all values were found at HKEY_CURRENT_USER\\{0}. Found keys: [ {1} ]", regPath, string.Join(", ", values));
							SetVirtualDmdDefaultPosition();
						}
					}
					else
					{
						Logger.Warn("Ignoring VPM registry for DMD position because key was not found at HKEY_CURRENT_USER\\{0}", regPath);
						SetVirtualDmdDefaultPosition();
					}
					key?.Dispose();

				}
				catch (Exception ex)
				{
					Logger.Warn(ex, "Could not retrieve registry values for DMD position for game \"" + gameName + "\".");
					SetVirtualDmdDefaultPosition();
				}

			}
			else
			{
				Logger.Debug("DMD position: No registry because it's ignored.");
				SetVirtualDmdDefaultPosition();
			}
		}

		/// <summary>
		/// Sets the position of the DMD as defined in the .ini file.
		/// </summary>
		private void SetVirtualDmdDefaultPosition(double x = -1d, double y = -1d, double width = -1d, double height = -1d)
		{
			var aspectRatio = Width / Height;
			Left = _config.HasGameOverride("left") || x < 0 ? _config.Left : x;
			Top = _config.HasGameOverride("top") || y < 0 ? _config.Top : y;
			Width = _config.HasGameOverride("width") || width < 0 ? _config.Width : width;
			if (_config.IgnoreAr)
			{
				Height = _config.HasGameOverride("height") || height < 0 ? _config.Height : height;
			}
			else
			{
				Height = Width / aspectRatio;
			}
		}

		private void ToggleAspectRatio(object sender, RoutedEventArgs e)
		{
			IgnoreAspectRatio = (sender as MenuItem).IsChecked;
			((VirtualDmdConfig)_config).SetIgnoreAspectRatio(IgnoreAspectRatio);
		}
	}
}
