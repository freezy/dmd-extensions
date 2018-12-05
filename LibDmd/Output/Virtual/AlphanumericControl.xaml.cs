using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibDmd.Common;
using NLog;
using SkiaSharp;

namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Interaction logic for AlphanumericControl.xaml
	/// </summary>
	public partial class AlphanumericControl : UserControl, IVirtualControl
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsAvailable => true;

		public DisplaySetting DisplaySetting { get; set; }
		public bool IgnoreAspectRatio { get; set; }
		public VirtualWindow Host { get; set; }

		private WriteableBitmap _writeableBitmap;

		public AlphanumericControl()
		{
			DataContext = this;
			InitializeComponent();
			DisplaySetting = new DisplaySetting();

			SizeChanged += SizeChanged_Event;
			CompositionTarget.Rendering += (o, e) => DrawImage(_writeableBitmap);
		}

		public void RenderSegments(ushort[] data)
		{
			UpdateData(data);
		}

		public void UpdateStyle(RasterizeStyleDefinition styleDef)
		{
			DisplaySetting.ApplyStyle(styleDef);
			Res.Rasterize(DisplaySetting, true);
		}

		public void ClearDisplay()
		{
			_data = new ushort[Res.SegmentSize[DisplaySetting.SegmentType]];
		}

		public void Dispose()
		{
			Res.Clear();
		}

		private void SetBitmap(WriteableBitmap bitmap)
		{
			AlphanumericDisplay.Source = _writeableBitmap = bitmap;
		}

		private void SizeChanged_Event(object sender, SizeChangedEventArgs e)
		{
			if (!Host.Resizing) {
				CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
			}
		}
	}
}
