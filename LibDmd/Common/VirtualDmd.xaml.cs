using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual;
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

		public double DotSize
		{
			set
			{
				if (Dmd != null) {
					Dmd.DotSize = value;
				}
			}
		}

		public VirtualDmd(VirtualDmdConfig config = null, string gameName = null) : base()
		{
			InitializeComponent();
			Initialize();

			if (config != null) {
				_config = config;

				ParentGrid.ContextMenu = new ContextMenu();

				var saveGlobalPos = new MenuItem();
				saveGlobalPos.Click += SavePositionGlobally;
				saveGlobalPos.Header = "Save position globally";
				ParentGrid.ContextMenu.Items.Add(saveGlobalPos);

				if (gameName != null) {
					var saveGamePos = new MenuItem();
					saveGamePos.Click += SavePositionGame;
					saveGamePos.Header = "Save position for \"" + gameName + "\"";
					ParentGrid.ContextMenu.Items.Add(saveGamePos);
				}
				
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
	}
}
