using System;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;

namespace LibDmd.Input.FileSystem
{
	public class ImageSourceGray2 : ImageSource, IGray2Source
	{
		DMDFrame _dmdFrame = new DMDFrame();

		public IObservable<DMDFrame> GetGray2Frames() => _frames;

		private readonly BehaviorSubject<DMDFrame> _frames;

		public ImageSourceGray2(BitmapSource bmp)
		{
			SetDimensions(bmp.PixelWidth, bmp.PixelHeight);
			_frames = new BehaviorSubject<DMDFrame>(_dmdFrame.Update(bmp.PixelWidth, bmp.PixelHeight, ImageUtil.ConvertToGray2(bmp)));
		}
	}	
	
	public class ImageSourceGray4 : ImageSource, IGray4Source
	{
		DMDFrame _dmdFrame = new DMDFrame();

		public IObservable<DMDFrame> GetGray4Frames() => _frames;

		private readonly BehaviorSubject<DMDFrame> _frames;

		public ImageSourceGray4(BitmapSource bmp)
		{
			SetDimensions(bmp.PixelWidth, bmp.PixelHeight);
			_frames = new BehaviorSubject<DMDFrame>(_dmdFrame.Update(bmp.PixelWidth, bmp.PixelHeight, ImageUtil.ConvertToGray4(bmp)));
		}
	}
	
	public class ImageSourceBitmap : ImageSource, IBitmapSource
	{
		public IObservable<BitmapSource> GetBitmapFrames() => _frames;

		private readonly BehaviorSubject<BitmapSource> _frames;

		public ImageSourceBitmap(BitmapSource bmp)
		{
			SetDimensions(bmp.PixelWidth, bmp.PixelHeight);
			_frames = new BehaviorSubject<BitmapSource>(bmp);
		}

		public ImageSourceBitmap(string fileName)
		{
			if (!File.Exists(fileName)) {
				throw new FileNotFoundException("Cannot find file \"" + fileName + "\".");
			}

			try {
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.UriSource = new Uri(Path.IsPathRooted(fileName) ? fileName : Path.Combine(Directory.GetCurrentDirectory(), fileName));
				bmp.EndInit();

				SetDimensions(bmp.PixelWidth, bmp.PixelHeight);
				_frames = new BehaviorSubject<BitmapSource>(bmp);

			} catch (UriFormatException) {
				throw new WrongFormatException($"Error parsing file name \"{fileName}\". Is this a path on the file system?");

			} catch (NotSupportedException e) {
				if (e.Message.Contains("No imaging component suitable"))
				{
					throw new WrongFormatException($"Could not determine image format. Are you sure {fileName} is an image?");
				}
				throw new Exception("Error instantiating image source.", e);
			}
		}
	}

	public abstract class ImageSource : AbstractSource
	{
		public override string Name { get; } = "Image Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();
	}

	public class WrongFormatException : Exception
	{
		public WrongFormatException(string message) : base(message)
		{
		}
	}
}
