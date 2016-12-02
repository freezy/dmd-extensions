using System;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Media.Imaging;
using LibDmd.Common;

namespace LibDmd.Input.FileSystem
{
	public class ImageSource : IFrameSource, IFrameSourceGray4
	{
		public string Name { get; } = "Image Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly BehaviorSubject<BitmapSource> _frames;
		private readonly BehaviorSubject<byte[]> _framesGray4;

		public ImageSource(BitmapSource bmp)
		{
			_frames = new BehaviorSubject<BitmapSource>(bmp);
			_framesGray4 =  new BehaviorSubject<byte[]>(ConvertToGray4(bmp));
		}

		public ImageSource(string fileName)
		{
			if (!File.Exists(fileName)) {
				throw new FileNotFoundException("Cannot find file \"" + fileName + "\".");
			}

			try {
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.UriSource = new Uri(fileName);
				bmp.EndInit();

				_frames = new BehaviorSubject<BitmapSource>(bmp);


			} catch (UriFormatException) {
				throw new WrongFormatException($"Error parsing file name \"{fileName}\". Is this a path on the file system?");

			} catch (NotSupportedException e) {
				if (e.Message.Contains("No imaging component suitable")) {
					throw new WrongFormatException($"Could not determine image format. Are you sure {fileName} is an image?");
				}
				throw;
			}
		}

		public IObservable<BitmapSource> GetFrames()
		{
			return _frames;
		}

		public IObservable<byte[]> GetGray4Frames()
		{
			return _framesGray4;
		}

		private static byte[] ConvertToGray4(BitmapSource bmp)
		{
			var frame = new byte[bmp.PixelWidth * bmp.PixelHeight];
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);

			for (var y = 0; y < bmp.PixelHeight; y++) {
				rect.Y = y;
				for (var x = 0; x < bmp.PixelWidth; x++) {

					rect.X = x;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

					// convert to HSL
					double hue;
					double saturation;
					double luminosity;
					ColorUtil.RgbToHsl(bytes[2], bytes[1], bytes[0], out hue, out saturation, out luminosity);

					frame[y * bmp.PixelWidth + x] = (byte)(luminosity * 15d);
				}
			}

			return frame;
		}
	}

	public class WrongFormatException : Exception
	{
		public WrongFormatException(string message) : base(message)
		{
		}
	}
}
