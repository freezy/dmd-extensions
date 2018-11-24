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
	/// Interaction logic for Seg20AlphaControl.xaml
	/// </summary>
	public partial class Seg20AlphaControl : UserControl, IAlphaNumericDestination, INotifyPropertyChanged, IVirtualControl
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

		private readonly SegGenerator _segGenerator;
		private WriteableBitmap _writeableBitmap;

		public Seg20AlphaControl()
		{
			DataContext = this;
			InitializeComponent();

			SizeChanged += SizeChanged_Event;
			//MouseDown += MouseDown_Event;
			//MouseUp += MouseUp_Event;

			_segGenerator = new SegGenerator();
			//ImageSource = _writeableBitmap = _segGenerator.CreateImage((int)Width, (int)Height);
			CompositionTarget.Rendering += (o, e) => _segGenerator.DrawImage(_writeableBitmap);
		}

		public void Init()
		{
		}

		public void RenderAlphaNumeric(AlphaNumericFrame frame)
		{
			Logger.Info("layout: {0}", frame.SegmentLayout);
			_segGenerator.UpdateFrame(frame);
		}

		private void MouseDown_Event(object sender, MouseButtonEventArgs e)
		{
			Logger.Info("mouse down!");
			_pauseRasterizing = true;
			_dirty = true;
		}

		private void MouseUp_Event(object sender, MouseButtonEventArgs e)
		{
			Logger.Info("mouse up!");
			if (_dirty) {
				//ImageSource = _writeableBitmap = _segGenerator.CreateImage((int)Width, (int)Height);
				_dirty = false;
			}
			_pauseRasterizing = false;
		}

		private void SizeChanged_Event(object sender, SizeChangedEventArgs e)
		{
			if (!_pauseRasterizing) {
				ImageSource = _writeableBitmap = _segGenerator.CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
			}
			
		}

		public void Dispose()
		{
		}


		public void ClearDisplay()
		{
			
		}
	}
}
