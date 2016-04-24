using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;
//using PixelFormat = System.Windows.Media.PixelFormat;

namespace LibDmd.Input.Media
{
	public class MediaPlayer : IFrameSource
	{
		public string Name { get; } = "Media Player";

		public string Filename { get; set; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<BitmapSource> _frames = new Subject<BitmapSource>();

		public MediaPlayer()
		{
			var ffmpegPath = $@"../../../../FFmpeg/bin/{(Environment.Is64BitProcess ? @"x64" : @"x86")}";
			//RegisterLibrariesSearchPath(ffmpegPath);
			RegisterLibrariesSearchPath(".\\");

			ffmpeg.av_register_all();
			ffmpeg.avcodec_register_all();
			ffmpeg.avformat_network_init();
		}


		public IObservable<BitmapSource> GetFrames()
		{
			unsafe {

				var pFormatContext = ffmpeg.avformat_alloc_context();
				if (ffmpeg.avformat_open_input(&pFormatContext, Filename, null, null) != 0) {
					throw new ApplicationException(@"Could not open file");
				}

				if (ffmpeg.avformat_find_stream_info(pFormatContext, null) != 0) {
					throw new ApplicationException(@"Could not find stream info");
				}

				AVStream* pStream = null;
				for (var i = 0; i < pFormatContext->nb_streams; i++) {
					if (pFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO) {
						pStream = pFormatContext->streams[i];
						break;
					}
				}
				if (pStream == null) {
					throw new ApplicationException(@"Could not find video stream");
				}

				var context = new FrameContext {
					FormatContext = pFormatContext,
					Stream = pStream
				};

				var codecContext = *context.Stream->codec;
				context.Width = codecContext.width;
				context.Height = codecContext.height;
				var fps = codecContext.framerate;
				Console.WriteLine("Frame rate = {0}/{1}", fps.den, fps.num);
				var sourcePixFmt = codecContext.pix_fmt;
				var codecId = codecContext.codec_id;
				const AVPixelFormat convertToPixFmt = AVPixelFormat.AV_PIX_FMT_BGR24;
				context.ConvertContext = ffmpeg.sws_getContext(context.Width, context.Height, sourcePixFmt, context.Width, context.Height, convertToPixFmt,
					ffmpeg.SWS_FAST_BILINEAR, null, null, null);
				if (context.ConvertContext == null) {
					throw new ApplicationException(@"Could not initialize the conversion context");
				}

				context.ConvertedFrame = ffmpeg.av_frame_alloc();
				var convertedFrameBufferSize = ffmpeg.avpicture_get_size(convertToPixFmt, context.Width, context.Height);
				var pConvertedFrameBuffer = (sbyte*)ffmpeg.av_malloc((ulong)convertedFrameBufferSize);
				ffmpeg.avpicture_fill((AVPicture*)context.ConvertedFrame, pConvertedFrameBuffer, convertToPixFmt, context.Width, context.Height);

				var pCodec = ffmpeg.avcodec_find_decoder(codecId);
				if (pCodec == null) {
					throw new ApplicationException(@"Unsupported codec");
				}

				// Reusing codec context from stream info, initally it was looking like this: 
				// AVCodecContext* pCodecContext = ffmpeg.avcodec_alloc_context3(pCodec); // but it is not working for all kind of codecs
				context.CodecContext = &codecContext;

				if ((pCodec->capabilities & ffmpeg.AV_CODEC_CAP_TRUNCATED) == ffmpeg.AV_CODEC_CAP_TRUNCATED) {
					context.CodecContext->flags |= ffmpeg.AV_CODEC_FLAG_TRUNCATED;
				}

				if (ffmpeg.avcodec_open2(context.CodecContext, pCodec, null) < 0) {
					throw new ApplicationException(@"Could not open codec");
				}

				context.DecodedFrame = ffmpeg.av_frame_alloc();

				var packet = new AVPacket();
				context.Packet = &packet;
				ffmpeg.av_init_packet(context.Packet);

				var frameNumber = 0;

				/*
				var myTimer = new Timer();
				myTimer.Elapsed += DisplayTimeEvent;
				myTimer.Interval = 1000; // 1000 ms is one second
				myTimer.Start();*/

				while (frameNumber < 10) {
					var bmp = ReadFrame(context);

					if (bmp != null) {
						Console.WriteLine("Frame {2} decoded at {0}x{1}.", bmp.Width, bmp.Height, frameNumber);
						var bitmapSource = Convert(bmp);
						_frames.OnNext(bitmapSource);
						frameNumber++;
					}
				}

				ffmpeg.av_free(context.ConvertedFrame);
				ffmpeg.av_free(pConvertedFrameBuffer);
				ffmpeg.sws_freeContext(context.ConvertContext);

				ffmpeg.av_free(context.DecodedFrame);
				ffmpeg.avcodec_close(context.CodecContext);
				ffmpeg.avformat_close_input(&pFormatContext);
				
			}
			return _frames;
		}

		private void DisplayTimeEvent(object sender, ElapsedEventArgs e)
		{
			throw new NotImplementedException();
		}

		private static unsafe Bitmap ReadFrame(FrameContext context)
		{
			if (ffmpeg.av_read_frame(context.FormatContext, context.Packet) < 0) {
				throw new ApplicationException(@"Could not read frame");
			}

			if (context.Packet->stream_index != context.Stream->index) {
				return null;
			}

			var gotPicture = 0;
			var size = ffmpeg.avcodec_decode_video2(context.CodecContext, context.DecodedFrame, &gotPicture, context.Packet);
			if (size < 0) {
				throw new ApplicationException($"Error while decoding frame.");
			}

			if (gotPicture == 1) {

				var src = &context.DecodedFrame->data0;
				var dst = &context.ConvertedFrame->data0;
				var srcStride = context.DecodedFrame->linesize;
				var dstStride = context.ConvertedFrame->linesize;
				ffmpeg.sws_scale(context.ConvertContext, src, srcStride, 0, context.Height, dst, dstStride);

				var convertedFrameAddress = context.ConvertedFrame->data0;
				var imageBufferPtr = new IntPtr(convertedFrameAddress);
				var linesize = dstStride[0];
				return new Bitmap(context.Width, context.Height, linesize, System.Drawing.Imaging.PixelFormat.Format24bppRgb, imageBufferPtr);
			}
			return null;
		}


		public static BitmapSource Convert(Bitmap bitmap)
		{
			var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
			var bitmapSource = BitmapSource.Create(
				bitmapData.Width, bitmapData.Height, 96, 96, PixelFormats.Rgb24, null,
				bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

			bitmap.UnlockBits(bitmapData);
			bitmap.Dispose();
			bitmapSource.Freeze(); // make it readable on any thread
			return bitmapSource;
		}


		public const string LD_LIBRARY_PATH = "LD_LIBRARY_PATH";
		public static void RegisterLibrariesSearchPath(string path)
		{
			switch (Environment.OSVersion.Platform) {
				case PlatformID.Win32NT:
				case PlatformID.Win32S:
				case PlatformID.Win32Windows:
					SetDllDirectory(path);
					break;
				case PlatformID.Unix:
				case PlatformID.MacOSX:
					string currentValue = Environment.GetEnvironmentVariable(LD_LIBRARY_PATH);
					if (string.IsNullOrWhiteSpace(currentValue) == false && currentValue.Contains(path) == false) {
						string newValue = currentValue + Path.PathSeparator + path;
						Environment.SetEnvironmentVariable(LD_LIBRARY_PATH, newValue);
					}
					break;
			}
		}

		private unsafe class FrameContext
		{
			public AVFormatContext* FormatContext;
			public AVPacket* Packet;
			public AVStream* Stream;

			public AVCodecContext* CodecContext;
			public AVFrame* DecodedFrame;
			public AVFrame* ConvertedFrame;
			public SwsContext* ConvertContext;
			public int Height;
			public int Width;
		}

		[DllImport("kernel32", SetLastError = true)]
		private static extern bool SetDllDirectory(string lpPathName);
	}
}
