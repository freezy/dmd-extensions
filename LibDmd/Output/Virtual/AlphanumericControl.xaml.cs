using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Interaction logic for AlphanumericControl.xaml
	/// </summary>
	public partial class AlphanumericControl : UserControl, INotifyPropertyChanged, IVirtualControl
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsAvailable => true;
		public event PropertyChangedEventHandler PropertyChanged;

		public bool IgnoreAspectRatio { get; set; }
		public VirtualWindow Host { get; set; }

		protected virtual void OnPropertyChanged(string propertyName) => OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, e);

		private bool _pauseRasterizing = false;
		private bool _dirty = true;

		#region ImageSource
		private ImageSource _imageSource;
		public ImageSource ImageSource
		{
			get => _imageSource;
			set {
				if (_imageSource != value) {
					_imageSource = value;
					OnPropertyChanged(nameof(ImageSource));
				}
			}
		}
		#endregion

		private WriteableBitmap _writeableBitmap;

		public AlphanumericControl()
		{
			DataContext = this;
			InitializeComponent();

			SizeChanged += SizeChanged_Event;
			//MouseDown += MouseDown_Event;
			//MouseUp += MouseUp_Event;

			//CreateImage(Width, Height);
			CompositionTarget.Rendering += (o, e) => DrawImage(_writeableBitmap);
		}

		public void ClearDisplay()
		{
			throw new NotImplementedException();
		}

		public void RenderSegments(ushort[] data)
		{
			UpdateData(data);
		}


		private void SizeChanged_Event(object sender, SizeChangedEventArgs e)
		{
			if (!_pauseRasterizing) {
				CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
			}
			
		}

		private void SetBitmap(WriteableBitmap bitmap)
		{
			AlphanumericDisplay.Source = _writeableBitmap = bitmap;
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}
