using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Output;

namespace LibDmd.Processor
{
	/// <summary>
	/// Resizes or flips a frame to given dimensions.
	/// </summary>
	public class TransformationProcessor : AbstractProcessor
	{
		/// <summary>
		/// Pixel width of the output frame
		/// </summary>
		public int Width { get; set; } = 128;

		/// <summary>
		/// Pixel height of the output frame.
		/// </summary>
		public int Height { get; set; } = 32;

		/// <summary>
		/// If set, flips the image vertically.
		/// </summary>
		public bool FlipVertically { get; set; }

		/// <summary>
		/// If set, flips the image horizontally.
		/// </summary>
		public bool FlipHorizontally { get; set; }

		public override BitmapSource Process(BitmapSource bmp, IFrameDestination dest)
		{
			if (bmp.PixelWidth == Width && bmp.PixelHeight == Height && !FlipHorizontally && !FlipVertically) {
				return bmp;
			}
			var sw = new Stopwatch();
			sw.Start();
			var resized = new TransformedBitmap(bmp, new ScaleTransform(
				Width / bmp.Width * (FlipHorizontally ? -1 : 1), 
				Height / bmp.Height * (FlipVertically ? -1 : 1), 
				(double)bmp.PixelWidth / 2, 
				(double)bmp.PixelHeight / 2));
			var rBmp = BitmapFrame.Create(resized);
			rBmp.Freeze();

			_whenProcessed.OnNext(rBmp);
			return rBmp;
		}
	}
}
