using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LibDmd.Output.VirtualDmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : UserControl, IFrameDestination
	{
		public new string Name { get; } = "Virtual DMD";
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
	}
}
