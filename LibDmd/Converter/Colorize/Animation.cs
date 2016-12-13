using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	public class Animation
	{
		public readonly Frame[] Frames;
		public ushort CurrentFrame { get; private set; }

		public bool IsFinished => Frames.Length == CurrentFrame;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Animation(BinaryReader reader)
		{
			var numFrames = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [fsq] Reading {0} frames", numFrames, reader.BaseStream.Position);
			Frames = new Frame[numFrames];
			for (var i = 0; i < numFrames; i++) {
				Frames[i] = new Frame(reader);
			}
		}
		
		public void Start()
		{
			CurrentFrame = 0;
		}

		public Frame Next()
		{
			if (IsFinished) {
				throw new IndexOutOfRangeException("Animation is finished, cannot continue.");
			}
			return Frames[CurrentFrame++];
		}

		public static Animation[] ReadFrameSequence(string filename)
		{
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);
			var numAnimations = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [fsq] Reading {0} animations", numAnimations, reader.BaseStream.Position);
			var animations = new Animation[numAnimations];
			for (var i = 0; i < numAnimations; i++) {
				animations[i] = new Animation(reader);
			}
			return animations;
		}
	}
}
