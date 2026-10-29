using System;
using System.IO;
using System.Linq;
using LibDmd.Common;
using LibDmd.Frame;

namespace LibDmd.Converter.Vni
{
	/// <summary>
	/// An animation, as read from the FSQ file.
	/// </summary>
	///
	/// <remarks>
	/// How an FSQ animation is stored depends on the mode it's played in, so
	/// it's read for a given mode.
	/// </remarks>
	public class FsqFrameSeq : FrameSeq
	{
		/// <summary>
		/// The mode the animation was read for.
		/// </summary>
		public SwitchMode Mode { get; }

		public FsqFrameSeq(BinaryReader reader, int fileVersion, SwitchMode mode) : base(reader.BaseStream.Position)
		{
			Mode = mode;
			Name = $"FSQ@{Offset}";

			int numFrames = reader.ReadUInt16BE();
			var frames = new FsqAnimationFrame[numFrames];
			AnimationDuration = 0;
			for (var i = 0; i < numFrames; i++) {
				frames[i] = new FsqAnimationFrame(reader, fileVersion, AnimationDuration);
				AnimationDuration += frames[i].Delay;
			}

			if (fileVersion > 2 && numFrames > 0 && (mode == SwitchMode.Follow || mode == SwitchMode.FollowReplace)) {
				// Follow scenes keep their first display frame. Its first plane holds the little-endian
				// hash table after the 12-byte frame header and plane marker.
				var endPosition = reader.BaseStream.Position;
				reader.BaseStream.Position = Offset + 2 + 13;
				foreach (var frame in frames) {
					frame.Hash = reader.ReadUInt32();
				}
				reader.BaseStream.Position = endPosition;
			}

			// LCM/LRM scenes instead have a separate metadata frame containing masks and, in v3, hashes.
			if (HasMaskFrame(mode, fileVersion, frames)) {
				var maskFrame = frames[0];
				var numMasks = maskFrame.Planes.Count;
				if (fileVersion > 2) {
					var hashesPerPlane = maskFrame.PlaneSize / 4;
					numMasks = Math.Max(numMasks - (numFrames - 1 + hashesPerPlane - 1) / hashesPerPlane, 0);

					// the hash table is little endian and is what the frames are matched against.
					var hashTable = maskFrame.Planes.Skip(numMasks).SelectMany(p => p.Plane).ToArray();
					for (var i = 1; i < numFrames && i * 4 <= hashTable.Length; i++) {
						var pos = (i - 1) * 4;
						frames[i].Hash = (uint)(hashTable[pos] | hashTable[pos + 1] << 8 | hashTable[pos + 2] << 16 | hashTable[pos + 3] << 24);
					}
				}
				Masks = maskFrame.Planes.Take(numMasks).Select(p => p.Plane).ToArray();
				frames = frames.Skip(1).ToArray();
			}

			// Smaller color-mask scenes store only the added planes. The 256x64 layout
			// stores the full depth, with stored plane 2 corresponding to output plane 2.
			if (IsColorMaskMode(mode)) {
				foreach (var frame in frames) {
					if (frame.PlaneSize != 2048) {
						frame.PrependEmptyPlanes(2);
					}
				}
			}

			Frames = frames.Cast<AnimationFrame>().ToArray();
			Size = frames.Length > 0 ? GetDimensions(frames[0].PlaneSize) : Dimensions.Standard;
		}

		/// <summary>
		/// Returns whether the animation is played in a mode that adds planes to the incoming frame.
		/// </summary>
		public static bool IsColorMaskMode(SwitchMode mode)
		{
			return mode == SwitchMode.ColorMask || mode == SwitchMode.Follow || mode == SwitchMode.LayeredColorMask || mode == SwitchMode.ColorMaskLoop;
		}

		private static bool HasMaskFrame(SwitchMode mode, int fileVersion, FsqAnimationFrame[] frames)
		{
			if (fileVersion < 2 || frames.Length < 2) {
				return false;
			}
			if (mode != SwitchMode.LayeredColorMask && mode != SwitchMode.MaskedReplace) {
				return false;
			}
			// the mask frame isn't displayed, so it has no delay, no hash and no replace mask.
			var first = frames[0];
			return first.Delay == 0 && first.Hash == 0 && first.Mask == null;
		}

		private static Dimensions GetDimensions(int planeSize)
		{
			switch (planeSize) {
				case 256: return new Dimensions(128, 16);
				case 512: return new Dimensions(128, 32);
				case 1536: return new Dimensions(192, 64);
				case 2048: return new Dimensions(256, 64);
				default: throw new WrongFormatException($"Unsupported FSQ plane size of {planeSize} bytes.");
			}
		}
	}
}
