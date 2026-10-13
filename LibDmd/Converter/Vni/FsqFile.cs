using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibDmd.Common;
using LibDmd.Frame;

namespace LibDmd.Converter.Vni
{
	/// <summary>
	/// Reads the FSQ file format, the animation file that comes with a v1 .pal file.
	/// </summary>
	///
	/// <remarks>
	/// The file starts with "FSQ " and a version byte. Older files have no header.
	/// The file has no index, the animations are found through the offsets of
	/// the palette file's mappings.
	/// </remarks>
	public class FsqFile : AnimationSet
	{
		private readonly Dictionary<(uint, SwitchMode), FsqFrameSeq> _animationsByOffset = new Dictionary<(uint, SwitchMode), FsqFrameSeq>();

		public FsqFile(string filename, PalFile palFile) : this(File.ReadAllBytes(filename), filename, palFile)
		{
		}

		public FsqFile(byte[] data, string filename, PalFile palFile)
		{
			Version = data.Length >= 5 && Encoding.ASCII.GetString(data, 0, 4) == "FSQ " ? data[4] : 0;
			Animations = new List<FrameSeq>();

			var maxWidth = 0;
			var maxHeight = 0;
			var mappings = palFile.Mappings?.Values ?? Enumerable.Empty<Mapping>();
			using (var reader = new BinaryReader(new MemoryStream(data))) {
				foreach (var mapping in mappings.Where(m => m.IsAnimation)) {
					var key = GetKey(mapping);
					if (_animationsByOffset.ContainsKey(key)) {
						continue;
					}
					try {
						reader.BaseStream.Seek(mapping.Offset, SeekOrigin.Begin);
						var animation = new FsqFrameSeq(reader, Version, mapping.Mode);
						_animationsByOffset[key] = animation;
						Animations.Add(animation);
						maxWidth = Math.Max(maxWidth, animation.Size.Width);
						maxHeight = Math.Max(maxHeight, animation.Size.Height);

					} catch (Exception e) when (e is IOException || e is WrongFormatException) {
						Logger.Warn("[fsq] Cannot read animation at offset {0} of {1}: {2}", mapping.Offset, filename, e.Message);
					}
				}
			}
			Dimensions = Animations.Count > 0 ? new Dimensions(maxWidth, maxHeight) : Dimensions.Standard;
		}

		public override FrameSeq Find(Mapping mapping)
		{
			_animationsByOffset.TryGetValue(GetKey(mapping), out var animation);
			return animation;
		}

		/// <summary>
		/// How an animation is read depends on the mode it's played in.
		/// </summary>
		private static (uint, SwitchMode) GetKey(Mapping mapping)
		{
			return (mapping.Offset, mapping.Mode);
		}

		public override string ToString()
		{
			return $"FSQ v{Version}, {Animations.Count} animation(s)";
		}
	}
}
