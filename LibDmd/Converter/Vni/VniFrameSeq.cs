﻿using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;

namespace LibDmd.Converter.Vni
{
	/// <summary>
	/// An animation, as read from the VNI file.
	/// </summary>
	public class VniFrameSeq : FrameSeq
	{
		public VniFrameSeq(BinaryReader reader, int fileVersion) : base(reader.BaseStream.Position)
		{
			// animations name
			var nameLength = reader.ReadInt16BE();
			Name = nameLength > 0 ? Encoding.UTF8.GetString(reader.ReadBytes(nameLength)) : "<undefined>";

			// other data
			Cycles = reader.ReadInt16BE();
			HoldCycles = reader.ReadInt16BE();
			ClockFrom = reader.ReadInt16BE();
			ClockIsSmall = reader.ReadByte() != 0;
			ClockInFront = reader.ReadByte() != 0;
			ClockOffsetX = reader.ReadInt16BE();
			ClockOffsetY = reader.ReadInt16BE();
			RefreshDelay = reader.ReadInt16BE();
			Type = reader.ReadByte();
			Fsk = reader.ReadByte();

			int numFrames = reader.ReadInt16BE();
			if (numFrames < 0) {
				numFrames += 65536;
			}

			if (fileVersion >= 2) {
				ReadPalettesAndColors(reader);
			}
			if (fileVersion >= 3) {
				EditMode = (AnimationEditMode)reader.ReadByte();
			}
			if (fileVersion >= 4) {
				Size = new Dimensions(reader.ReadInt16BE(), reader.ReadInt16BE());
			}
			if (fileVersion >= 5)
			{
				int numMasks = reader.ReadInt16BE();
				Masks = new byte[numMasks][];
				for (var i = 0; i < numMasks; i++)
				{
					// ReSharper disable once UnusedVariable
					int locked = reader.ReadByte();
					int size = reader.ReadInt16BE();
					Masks[i] = reader.ReadBytesRequired(size).Select(VniAnimationPlane.Reverse).ToArray();
				}
			}

			if (fileVersion >= 6)
			{
				// ReSharper disable once UnusedVariable
				int compiledAnimation = reader.ReadByte();
				int size = reader.ReadInt16BE();
				// ReSharper disable once UnusedVariable
				string animName = size > 0 ? Encoding.UTF8.GetString(reader.ReadBytes(nameLength)) : "<undefined>";

				// ReSharper disable once UnusedVariable
				uint startFrame = reader.ReadUInt32BE();
			}

			Logger.Debug("[vni] VNI[{3}] Reading {0} frame{1} for animation \"{2}\"...", numFrames, numFrames == 1 ? "" : "s", Name, reader.BaseStream.Position);
			Frames = new AnimationFrame[numFrames];
			AnimationDuration = 0;
			for (var i = 0; i < numFrames; i++) {
				Frames[i] = new VniAnimationFrame(reader, fileVersion, AnimationDuration);
				if (Frames[i].Mask != null && TransitionFrom == 0) {
					TransitionFrom = i;
				}
				AnimationDuration += Frames[i].Delay;
			}
		}

		private void ReadPalettesAndColors(BinaryReader reader)
		{
			PaletteIndex = reader.ReadInt16BE();
			var numColors = reader.ReadInt16BE();
			if (numColors <= 0) {
				return;
			}
			AnimationColors = new Color[numColors];
			for (var i = 0; i < numColors; i++) {
				AnimationColors[i] = Color.FromRgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
			}
			Logger.Debug("[vni] VNI[{2}] Found {0} colors for palette {1}.", numColors, PaletteIndex, reader.BaseStream.Position);
		}

		public override string ToString()
		{
			return $"{Name}, {Frames.Length} frames";
		}
	}
}
