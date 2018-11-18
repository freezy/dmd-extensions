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
using NLog;

namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Interaction logic for Seg20AlphaControl.xaml
	/// </summary>
	public partial class Seg20AlphaControl : UserControl, IAlphaNumericDestination, INotifyPropertyChanged
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsAvailable => true;
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName) => OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, e);

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

			SizeChanged += LocationChanged_Event;

			_segGenerator = new SegGenerator();
			ImageSource = _writeableBitmap = _segGenerator.CreateImage(1280, 116);
			CompositionTarget.Rendering += (o, e) => _segGenerator.DrawImage(_writeableBitmap);
		}

		public void Init()
		{
		}

		public void RenderAlphaNumeric(AlphaNumericFrame frame)
		{
			_segGenerator.DrawFrame(frame);
		}

		private void LocationChanged_Event(object sender, SizeChangedEventArgs e)
		{
			ImageSource = _writeableBitmap = _segGenerator.CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
		}

		public void Dispose()
		{
		}


		public void ClearDisplay()
		{
			
		}
	}
}
