using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

		public double DotSize
		{
			set
			{
				if (Dmd != null) {
					Dmd.DotSize = value;
				}
			}
		}

		public VirtualDmd() : base()
		{
			InitializeComponent();
			Initialize();
		}
	}
}