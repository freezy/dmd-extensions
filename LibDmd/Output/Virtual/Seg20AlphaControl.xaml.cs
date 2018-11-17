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

namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Interaction logic for Seg20AlphaControl.xaml
	/// </summary>
	public partial class Seg20AlphaControl : UserControl, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
			=> this.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));

		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
			=> this.PropertyChanged?.Invoke(this, e);

		#region ImageSource
		private ImageSource _ImageSource;
		public ImageSource ImageSource
		{
			get => _ImageSource;
			set
			{
				if (_ImageSource != value) {
					_ImageSource = value;
					OnPropertyChanged(nameof(ImageSource));
				}
			}
		}
		#endregion

		public string Name { get; private set; }

		private readonly ISegGenerator ImageService;
		private readonly WriteableBitmap WriteableBitmap;

		public Seg20AlphaControl()
		{
			DataContext = this;
			InitializeComponent();

			var imageService = new SegGenerator();

			this.Name = "SkiaSharp Wpf Example";
			this.ImageService = imageService;
			this.ImageSource = this.WriteableBitmap = imageService.CreateImage(900, 600);
			CompositionTarget.Rendering += (o, e) => this.ImageService.UpdateImage(this.WriteableBitmap);
		}
	}
}
