using System.Collections.Generic;
using System.IO;
using System.Text;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Input.FileSystem;

namespace LibDmd.Converter.Vni
{
	public class VniFile : AnimationSet
	{

		public VniFile(string filename)
		{
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);

			// name
			var header = Encoding.UTF8.GetString(reader.ReadBytes(4));
			if (header != "VPIN") {
				reader.Close();
				fs.Close();
				throw new WrongFormatException("Not a VPIN file: " + filename);
			}

			// version
			Version = reader.ReadInt16BE();

			// number of animations
			var numAnimations = reader.ReadInt16BE();

			if (Version >= 2) {
				Logger.Trace("[vni] VNI[{1}] Skipping {0} bytes of animation indexes.", numAnimations * 4, reader.BaseStream.Position);
				for (var i = 0; i < numAnimations; i++) {
					reader.ReadUInt32();
				}
			}

			Animations = new List<FrameSeq>(numAnimations);
			Logger.Debug("[vni] VNI[{3}] Reading {0} animations from {1} v{2}...", numAnimations, header, Version, reader.BaseStream.Position);

			var maxWidth = 0;
			var maxHeight = 0;
			for (var i = 0; i < numAnimations; i++) {
				Animations.Add(new VniFrameSeq(reader, Version));
				int h = Animations[i].Size.Height;
				int w = Animations[i].Size.Width;
				if (h > maxHeight)
					maxHeight = h;
				if (w > maxWidth)
					maxWidth = w;
			}
			reader.Close();
			fs.Close();

			Dimensions = new Dimensions(maxWidth, maxHeight);
		}

		public override string ToString()
		{
			return $"VPIN v{Version}, {Animations.Count} animation(s)";
		}
	}
}
