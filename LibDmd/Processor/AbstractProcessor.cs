using System;
using System.IO;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Processor
{
	/// <summary>
	/// Base class for all processors that contains common logic.
	/// </summary>
	public abstract class AbstractProcessor
	{
		/// <summary>
		/// Produces frames as they come out of the processor.
		/// Useful for debugging and displaying intermediate results.
		/// </summary>
		public IObservable<BitmapSource> WhenProcessed => _whenProcessed;

		/// <summary>
		/// If set to false, this processor will be ignored by the <see cref="RenderGraph"/>.
		/// </summary>
		public virtual bool Enabled { get; set; } = true;

		/// <summary>
		/// Processes a frame
		/// </summary>
		/// <param name="bmp">Unprocessed frame</param>
		/// <param name="dest">The destination for which the frame is processed.</param>
		/// <returns>Processed frame</returns>
		public abstract BitmapSource Process(BitmapSource bmp, IDestination dest);

		/// <summary>
		/// If false, non-RGB displays will skip this processor.
		/// </summary>
		/// <remarks>
		/// This is useful because we don't want to artificially monochromify
		/// frames that are sent to a monochrome display anyway.
		/// </remarks>
		public virtual bool IsGrayscaleCompatible { get; } = true;

		protected Subject<BitmapSource> _whenProcessed = new Subject<BitmapSource>();

		protected static void Dump(BitmapSource image, string filePath)
		{
			using (var fileStream = new FileStream(filePath, FileMode.Create)) {
				BitmapEncoder encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(image));
				encoder.Save(fileStream);
			}
		}
	}
}
