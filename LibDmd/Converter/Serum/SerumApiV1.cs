using System.Collections.Generic;
using System;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using LibDmd.Frame;
using LibDmd.Common;
using LibDmd.Input;
using System.Windows.Media;
using NLog;

namespace LibDmd.Converter.Serum
{
	public class SerumApiV1 : ISerumApi
	{
		public uint NumColors { get; }

		private readonly FrameBufferPool _buffers = new FrameBufferPool();
		private readonly Dimensions _dimensions;

		private readonly Subject<ColoredFrame> _coloredGray6Frames;
		private readonly Subject<FrameEvent> _frameEvents;

		private readonly byte[] _bytePalette = new byte[64 * 3];

		private readonly FrameEvent _frameEvent = new FrameEvent();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SerumApiV1(Subject<ColoredFrame> coloredGray6Frames, Subject<FrameEvent> frameEvents, ref SerumFrame serumFrame)
		{
			_coloredGray6Frames = coloredGray6Frames;
			_frameEvents = frameEvents;
			_dimensions = serumFrame.Width32 > 0
				? new Dimensions((int)serumFrame.Width32, 32)
				: new Dimensions((int)serumFrame.Width64, 64);

			NumColors = serumFrame.nocolors;
		}

		public void Convert(ref SerumFrame serumFrame)
		{
			using (_buffers.Enter()) ConvertFrame(ref serumFrame);
		}

		private void ConvertFrame(ref SerumFrame serumFrame)
		{
			if (serumFrame.Frame == IntPtr.Zero) {
				Logger.Warn("[serum] Frame data is null.");
				return;
			}
			var pixels = FrameBufferPool.Allocate<byte>(_dimensions.Surface);
			var palette = FrameBufferPool.Allocate<Color>(64);

			// copy data from unmanaged to managed
			if (serumFrame.Palette != IntPtr.Zero) {
				Marshal.Copy(serumFrame.Palette, _bytePalette, 0, _bytePalette.Length);
			}
			Marshal.Copy(serumFrame.Frame, pixels, 0, _dimensions.Width * _dimensions.Height);
			BytesToColors(_bytePalette, palette);

			// send event trigger
			if (serumFrame.triggerID != 0xffffffff) {
				_frameEvents.OnNext(_frameEvent.Update((ushort)serumFrame.triggerID));
			}

			// send the colored frame
			_coloredGray6Frames.OnNext(FrameBufferPool.Create(_dimensions, pixels, palette));
		}

		public void UpdateRotations(ref SerumFrame serumFrame, Color[] palette, uint _)
		{
			if (serumFrame.Palette != IntPtr.Zero) {
				Marshal.Copy(serumFrame.Palette, _bytePalette, 0, _bytePalette.Length);
			}
			BytesToColors(_bytePalette, palette);
		}

		private static void BytesToColors(IReadOnlyList<byte> bytes, Color[] palette)
		{
			for (int ti = 0; ti < 64; ti++) {
				palette[ti].A = 255;
				palette[ti].R = bytes[ti * 3];
				palette[ti].G = bytes[ti * 3 + 1];
				palette[ti].B = bytes[ti * 3 + 2];
			}
		}
	}
}
