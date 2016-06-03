using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LibDmd.Output.VirtualDmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : UserControl, IFrameDestination, IRawOutput
	{
		public bool IsAvailable { get; } = true;
		public bool IsRgb { get; } = true;

		public VirtualDmdControl()
		{
			InitializeComponent();
		}

		public void Render(BitmapSource bmp)
		{
			Dispatcher.Invoke(() => Dmd.Source = bmp);
		}

		public void Init()
		{
			// nothing to init
		}

		public void Dispose()
		{
			// nothing to dispose
		}

		public void RenderRaw(byte[] data)
		{
			Console.WriteLine("GOT {0} BYTES OF DATA!", data.Length);
		}
	}
}
