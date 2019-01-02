using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual;
using LibDmd.Output.Virtual.SkiaDmd;
using NLog;

namespace LibDmd.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualSkiaDmd : VirtualDisplay
	{
		public override IVirtualControl VirtualControl => Dmd;

		public VirtualSkiaDmd(DmdStyleDefinition styleDef, Configuration config) : base()
		{
			InitializeComponent();
			Initialize();

			Dmd.StyleDefinition = styleDef;
			Dmd.Configuration = config;
		}
	}
}