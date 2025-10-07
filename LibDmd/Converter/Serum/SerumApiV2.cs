using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Converter.Serum
{
	public class SerumApiV2 : ISerumApi
	{
		public uint NumColors => 0;

		private readonly Subject<DmdFrame> _rgb565Frames;
		private readonly Dictionary<uint, byte[]> _frames = new Dictionary<uint, byte[]>();
		private readonly Dimensions _dimensions;
		private readonly ScalerMode _scalerMode;
		private readonly DmdFrame _frame;

		private const int MAX_COLOR_ROTATIONS_V2 = 4;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SerumApiV2(Subject<DmdFrame> rgb565Frames, ScalerMode scalerMode, ref SerumFrame serumFrame)
		{
			_rgb565Frames = rgb565Frames;
			_scalerMode = scalerMode;
			_dimensions = ReadDimensions(ref serumFrame);
			_frame = new DmdFrame(_dimensions, 16);
			Logger.Info($"Serum v2 initialized {_dimensions}.");
		}

		public void Convert(ref SerumFrame serumFrame)
		{
			ReadAndPushNextFrame(ref serumFrame);
		}

		public void UpdateRotations(ref SerumFrame serumFrame, Color[] palette, uint changed)
		{
			ReadAndPushNextFrame(ref serumFrame);
		}

		private void ReadAndPushNextFrame(ref SerumFrame serumFrame)
		{
			byte[] frameData;
			Dimensions dim;
			if (_scalerMode == ScalerMode.None) {
				if (serumFrame.Has32PFrame) {
					frameData = Read32PFrame(ref serumFrame);
					dim = new Dimensions((int)serumFrame.Width32, 32);
				} else {
					frameData = Read64PFrame(ref serumFrame);
					dim = new Dimensions((int)serumFrame.Width64, 64);
					// TODO scale down
				}
			} else {
				if (serumFrame.Has64PFrame) {
					frameData = Read64PFrame(ref serumFrame);
					dim = new Dimensions((int)serumFrame.Width64, 64);
				} else {
					frameData = Read32PFrame(ref serumFrame);
					dim = new Dimensions((int)serumFrame.Width32, 32);
					// TODO scale up
				}
			}

			_frame.Update(dim, frameData);
			_rgb565Frames.OnNext(_frame);
		}

		private static Dimensions ReadDimensions(ref SerumFrame serumFrame)
		{
			if (serumFrame.Width32 > 0) {
				return new Dimensions((int)serumFrame.Width32, 32);
			}

			if (serumFrame.Width64 > 0) {
				return new Dimensions((int)serumFrame.Width64, 64);
			}

			throw new ArgumentException("Invalid SerumFrame dimensions.");
		}

		private byte[] Read32PFrame(ref SerumFrame serumFrame)
		{
			var frameSize = serumFrame.Width32 * 32 * 2;
			var frame = GetFrame(frameSize);
			Marshal.Copy(serumFrame.Frame32Data, frame, 0, (int)frameSize);
			return frame;
		}

		private byte[] Read64PFrame(ref SerumFrame serumFrame)
		{
			var frameSize = serumFrame.Width64 * 64 * 2;
			var frame = GetFrame(frameSize);
			Marshal.Copy(serumFrame.Frame64Data, frame, 0, (int)frameSize);
			return frame;
		}

		private byte[] GetFrame(uint size)
		{
			if (!_frames.ContainsKey(size)) {
				_frames[size] = new byte[size];
			}
			return _frames[size];
		}
	}
}
