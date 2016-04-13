using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PinDmd.Processor
{
	public abstract class AbstractProcessor
	{
		public IObservable<BitmapSource> WhenProcessed => _whenProcessed;
		protected Subject<BitmapSource> _whenProcessed = new Subject<BitmapSource>();

		public bool Enabled { get; set; }
		public abstract BitmapSource Process(BitmapSource bmp);

	}
}
