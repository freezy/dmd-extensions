using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input
{
	/// <summary>
	/// Acts as source for any frames ending up on the DMD.
	/// </summary>
	///
	/// <remarks>
	/// Since we want a contineous flow of frames, the method to override
	/// returns an observable. Note that the producer decides on the frequency
	/// in which frames are delivered to the consumer.
	///
	/// When implementing a source, make sure to only implement the "native"
	/// bit lengths of the source. Convertion if necessary is done in the Render
	/// Graph directly.
	/// </remarks>
	public interface ISource
	{
		/// <summary>
		/// A display name for the source
		/// </summary>
		string Name { get; }

		/// <summary>
		/// The size of the source. Can change any time.
		/// </summary>
		BehaviorSubject<Dimensions> Dimensions { get; set; }

		/// <summary>
		/// An observable that triggers when the source starts providing frames.
		/// </summary>
		IObservable<Unit> OnResume { get; }

		/// <summary>
		/// An observable that triggers when the source is interrupted, e.g. a game is stopped.
		/// </summary>
		IObservable<Unit> OnPause { get; }
	}

	/// <summary>
	/// A set of dimensions, in pixel.
	/// </summary>
	public struct Dimensions
	{
		public int Width { get; set; }
		public int Height { get; set; }

		public int Surface => Width * Height;

		public double AspectRatio => (double)Width / Height;

		public bool IsFlat => Width == 0 || Height == 0;

		public Dimensions(int width, int height) {
			Width = width;
			Height = height;
		}

		public static bool operator == (Dimensions x, Dimensions y)
		{
			return x.Width == y.Width && x.Height == y.Height;
		}

		public static bool operator != (Dimensions x, Dimensions y)
		{
			return !(x == y);
		}

		public override string ToString()
		{
			return $"{Width}x{Height}";
		}

		public bool Equals(Dimensions other)
		{
			return Width == other.Width && Height == other.Height;
		}

		public override bool Equals(object obj)
		{
			return obj is Dimensions other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked {
				return (Width * 397) ^ Height;
			}
		}
	}

	public enum ResizeMode
	{
		/// <summary>
		/// Stretch to fit dimensions. Aspect ratio is not kept.
		/// </summary>
		Stretch,

		/// <summary>
		/// Smaller dimensions fits while larger dimension gets cropped.
		/// </summary>
		Fill,

		/// <summary>
		/// Larger dimensions fits and smaller dimension stays black.
		/// </summary>
		Fit
	}
}
