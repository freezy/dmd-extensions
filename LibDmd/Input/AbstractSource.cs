using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input
{
	public abstract class AbstractSource
	{
		public BehaviorSubject<Dimensions> Dimensions { get; } = new BehaviorSubject<Dimensions>(new Dimensions { Width = 128, Height = 32 });

		protected int Width;
		protected int Height;

		public void SetDimensions(int width, int height)
		{
			if (width != Width || height != Height) {
				Dimensions.OnNext(new Dimensions { Width = width, Height = height });
			}
			Width = width;
			Height = height;
		}
	}
}
