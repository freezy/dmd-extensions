using System;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Media.Imaging;
using LibDmd.Common;

namespace LibDmd.Input.FileSystem
{
	public class ImageSource : IFrameSourceGray4, IFrameSourceGray2, IFrameSourceRgb24
	{
		public string Name { get; } = "Image Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly BehaviorSubject<BitmapSource> _frames;
		private readonly BehaviorSubject<byte[]> _framesGray2;
		private readonly BehaviorSubject<byte[]> _framesGray4;
		private readonly BehaviorSubject<byte[]> _framesRgb24;

		public ImageSource(BitmapSource bmp)
		{
			_frames = new BehaviorSubject<BitmapSource>(bmp);
			_framesGray2 = new BehaviorSubject<byte[]>(ImageUtil.ConvertToGray2(bmp));
			_framesGray4 =  new BehaviorSubject<byte[]>(ImageUtil.ConvertToGray4(bmp));
			_framesRgb24 = new BehaviorSubject<byte[]>(ImageUtil.ConvertToRgb24(bmp));
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
				throw new Exception("Error instantiating image source.", e);
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

		public IObservable<byte[]> GetGray2Frames()
		{
			return _framesGray2;
		}

		public IObservable<byte[]> GetRgb24Frames()
		{
			return _framesRgb24;
		}
	}

	public class WrongFormatException : Exception
	{
		public WrongFormatException(string message) : base(message)
		{
		}
	}
}
