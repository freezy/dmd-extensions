using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace LibDmd.Input
{
	public abstract class AbstractSource
	{
		public BehaviorSubject<Dimensions> Dimensions { get; set; } = new BehaviorSubject<Dimensions>(new Dimensions { Width = 128, Height = 32 });

		public abstract string Name { get; }
		protected int Width;
		protected int Height;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void SetDimensions(int width, int height)
		{
			if (width != Dimensions.Value.Width || height != Dimensions.Value.Height) {
				Logger.Info("{4} received new dimensions: {0}x{1} => {2}x{3}.", Width, Height, width, height, Name);
				Dimensions.OnNext(new Dimensions { Width = width, Height = height });
			}
			Width = width;
			Height = height;
		}
	}
}
