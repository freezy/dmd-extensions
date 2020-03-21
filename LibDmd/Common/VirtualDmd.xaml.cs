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

		private string _gameName;

		public double DotSize
		{
			set
			{
				if (Dmd != null) {
					Dmd.DotSize = value;
				}
			}
		}

		public VirtualDmd(Configuration config = null, string gameName = null) : base()
		{
			InitializeComponent();
			Initialize();
			_gameName = gameName;

			if (config != null) {
				ParentGrid.ContextMenu = new ContextMenu();

				var saveGlobalPos = new MenuItem();
				saveGlobalPos.Click += SavePositionGlobal;
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

		private void SavePositionGlobal(object sender, RoutedEventArgs e)
		{
		}

		private void SavePositionGame(object sender, RoutedEventArgs e)
		{
		}
	}
}
