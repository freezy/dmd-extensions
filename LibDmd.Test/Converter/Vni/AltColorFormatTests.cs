using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Converter.Vni;
using LibDmd.Frame;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class AltColorFormatTests
	{
		private const string GameName = "fsq-test";
		private const string PacKey = "000102030405060708090a0b0c0d0e0f";
		private static readonly Dimensions Dim = new Dimensions(128, 32);
		private static readonly Color[] Colors16 = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)(i * 16), 0, 0)).ToArray();

		private string _altColorPath;

		[SetUp]
		public void SetUp()
		{
			_altColorPath = Path.Combine(Path.GetTempPath(), "libdmd-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(_altColorPath, GameName));
		}

		[TearDown]
		public void TearDown()
		{
			if (_altColorPath != null && Directory.Exists(_altColorPath)) {
				Directory.Delete(_altColorPath, true);
			}
		}

		private class ClockedColorizer : VniColorizer
		{
			public readonly List<ColoredFrame> Frames = new List<ColoredFrame>();

			public ClockedColorizer(PalFile palFile, AnimationSet animations) : base(palFile, animations)
			{
				GetColoredGray2Frames().Subscribe(f => Frames.Add((ColoredFrame)f.Clone()));
				GetColoredGray4Frames().Subscribe(f => Frames.Add((ColoredFrame)f.Clone()));
				GetColoredGray6Frames().Subscribe(f => Frames.Add((ColoredFrame)f.Clone()));
			}

			public void Tick(DmdFrame frame) => ConvertClocked(frame);
		}

		[TestCase(2, SwitchMode.Follow)]
		[TestCase(2, SwitchMode.FollowReplace)]
		[TestCase(3, SwitchMode.Follow)]
		[TestCase(3, SwitchMode.FollowReplace)]
		public void ShouldFollowFsqFrameHashes(int version, SwitchMode mode)
		{
			var first = new DmdFrame(Dim, FrameUtil.Join(Dim, new[] { Planes.With(1), Planes.Empty() }), 2);
			var second = new DmdFrame(Dim, FrameUtil.Join(Dim, new[] { Planes.With(2), Planes.Empty() }), 2);
			var hashes = new[] { FrameUtil.Checksum(first.BitPlanes[0]), FrameUtil.Checksum(second.BitPlanes[0]) };
			var table = new byte[Planes.Size];
			Buffer.BlockCopy(hashes, 0, table, 0, hashes.Length * 4);
			var fsq = new FsqBuilder(version);
			var offset = fsq.Animation(
				new FsqFrame { Delay = 50, Hash = version == 2 ? hashes[0] : 0, Mask = table, Planes = new[] { Planes.With(100), Planes.Empty() } },
				new FsqFrame { Delay = 50, Hash = version == 2 ? hashes[1] : 0, Mask = Planes.Empty(), Planes = new[] { Planes.With(101), Planes.Empty() } });
			var pal = new PalBuilder(1).Palette(0, 1, Colors16).Mapping(hashes[0], mode, 0, offset).BuildFile();
			var animations = new FsqFile(fsq.Build(), "follow.fsq", pal);
			animations.Find(pal.Mappings[hashes[0]]).NumFrames.Should().Be(2);
			using (var colorizer = new ClockedColorizer(pal, animations)) {
				colorizer.Tick(first);
				colorizer.Tick(second);
				var animationBit = mode == SwitchMode.Follow ? 4 : 1;
				colorizer.Frames.Select(f => Array.FindIndex(f.Data, b => (b & animationBit) != 0)).Should().Equal(100, 101);
			}
		}

		[TestCase(SwitchMode.ColorMask, 4)]
		[TestCase(SwitchMode.Follow, 4)]
		[TestCase(SwitchMode.LayeredColorMask, 4)]
		[TestCase(SwitchMode.ColorMaskLoop, 4)]
		[TestCase(SwitchMode.ColorMask, 6)]
		[TestCase(SwitchMode.Follow, 6)]
		[TestCase(SwitchMode.LayeredColorMask, 6)]
		[TestCase(SwitchMode.ColorMaskLoop, 6)]
		public void ShouldKeep256x64FsqColorMaskPlaneIndices(SwitchMode mode, int bitDepth)
		{
			var dim = new Dimensions(256, 64);
			var data = new byte[dim.Surface];
			data[1] = 1;
			var trigger = new DmdFrame(dim, data, 2);
			var crc = FrameUtil.Checksum(trigger.BitPlanes[0]);
			var planes = Enumerable.Range(0, bitDepth).Select(_ => new byte[2048]).ToArray();
			// Stored planes 0 and 1 must be discarded in favor of the incoming grayscale planes.
			planes[0][100 / 8] |= (byte)(1 << (100 % 8));
			planes[1][101 / 8] |= (byte)(1 << (101 % 8));
			for (var i = 2; i < bitDepth; i++) {
				var pixel = 13000 + i;
				planes[i][pixel / 8] |= (byte)(1 << (pixel % 8));
			}
			var hashTable = new byte[2048];
			Buffer.BlockCopy(new[] { crc }, 0, hashTable, 0, 4);
			var fsq = new FsqBuilder(3);
			var colorFrame = new FsqFrame { Delay = 50, Mask = hashTable, Planes = planes };
			var offset = mode == SwitchMode.LayeredColorMask
				? fsq.Animation(new FsqFrame { Planes = new[] { new byte[2048], hashTable } }, colorFrame)
				: fsq.Animation(colorFrame);
			var colors = Enumerable.Range(0, 1 << bitDepth).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(1).Palette(0, 1, colors).Mapping(crc, mode, 0, offset).BuildFile();
			using (var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "hd.fsq", pal))) {
				colorizer.Tick(trigger);
				colorizer.Frames.Should().ContainSingle();
				var output = colorizer.Frames[0];
				output.Dimensions.Should().Be(dim);
				output.BitLength.Should().Be(bitDepth);
				output.Data[1].Should().Be(1);
				output.Data[100].Should().Be(0);
				output.Data[101].Should().Be(0);
				for (var i = 2; i < bitDepth; i++) {
					output.Data[13000 + i].Should().Be((byte)(1 << i));
				}
			}
		}

		[TestCase(2)]
		[TestCase(4)]
		public void ShouldColorizeFsqLayeredColorMask(int bitLength)
		{
			var data = new byte[Dim.Surface];
			data[1] = 1;
			var trigger = new DmdFrame(Dim, data, bitLength);
			var triggerPlane = trigger.BitPlanes[0];
			var mask = Planes.With(1);

			// v3: the first frame holds the masks and the frame hashes, the others the planes added to the input.
			var hashes = new[] { FrameUtil.Checksum(triggerPlane), FrameUtil.ChecksumWithMask(triggerPlane, mask) };
			var hashTable = new byte[Planes.Size];
			Buffer.BlockCopy(hashes, 0, hashTable, 0, hashes.Length * 4);
			var fsq = new FsqBuilder(3);
			var offset = fsq.Animation(
				new FsqFrame { Planes = new[] { mask, hashTable } },
				new FsqFrame { Delay = 20, Hash = hashes[0], Mask = Planes.Empty(), Planes = new[] { Planes.With(100), Planes.Empty() } },
				new FsqFrame { Delay = 20, Hash = hashes[1], Mask = Planes.Empty(), Planes = new[] { Planes.With(101), Planes.Empty() } });
			var pal = new PalBuilder(1).Palette(0, 1, Colors16).Mapping(hashes[0], SwitchMode.LayeredColorMask, 0, offset).BuildFile();
			var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal));

			colorizer.Tick(trigger);

			colorizer.Frames.Should().ContainSingle();
			var frame = colorizer.Frames[0];
			frame.Data[1].Should().Be(1);
			frame.Data[100].Should().Be(4);
			frame.Data[101].Should().Be(4);
		}

		[Test]
		public void ShouldReadHeaderlessFsq()
		{
			var fsq = new FsqBuilder(0);
			var offset = fsq.Animation(
				new FsqFrame { Delay = 10, Planes = new[] { Planes.With(1), Planes.Empty() } },
				new FsqFrame { Delay = 10, Planes = new[] { Planes.With(2), Planes.Empty() } });
			var pal = new PalBuilder(1).Palette(0, 1, Colors16).Mapping(1, SwitchMode.Replace, 0, offset).BuildFile();

			var file = new FsqFile(fsq.Build(), "test.fsq", pal);

			file.Find(pal.Mappings[1]).NumFrames.Should().Be(2);
			file.Dimensions.Should().Be(Dim);
		}

		[Test]
		public void ShouldReadVniV6WithDifferentCompiledAnimationName()
		{
			var w = new BigEndianWriter().Ascii("VPIN").U16(6).U16(2).U32(0).U32(0);
			var offset1 = (uint)w.Position;
			WriteVniAnimation(w, "a", "compiled animation", 1);
			var offset2 = (uint)w.Position;
			WriteVniAnimation(w, "second", "", 2);

			var vni = new VniFile(w.ToArray(), "test.vni");

			vni.Find(offset1).NumFrames.Should().Be(1);
			vni.Find(offset2).NumFrames.Should().Be(2);
		}

		[Test]
		public void ShouldAcceptMasksOf256x64()
		{
			var pal = new PalBuilder(2).Palette(0, 1, Colors16).Mapping(1, SwitchMode.Palette, 0, 0).Mask(new byte[2048]).Mask(new byte[2048]).BuildFile();

			pal.Masks.Should().HaveCount(2);
		}

		[Test]
		public void ShouldLoadFsqForV1Palette()
		{
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(new FsqFrame { Delay = 10, Mask = Planes.Empty(), Planes = new[] { Planes.With(1), Planes.Empty() } });
			var pal = new PalBuilder(1).Palette(0, 1, Colors16).Mapping(1, SwitchMode.Replace, 0, offset);
			File.WriteAllBytes(Path.Combine(_altColorPath, GameName, "pin2dmd.pal"), pal.Build());
			File.WriteAllBytes(Path.Combine(_altColorPath, GameName, "pin2dmd.fsq"), fsq.Build());

			var loader = new VniLoader(_altColorPath, GameName);
			loader.Load(null);

			loader.Animations.Should().BeOfType<FsqFile>();
			loader.Animations.Find(loader.Pal.Mappings[1]).Should().NotBeNull();
		}

		[Test]
		public void ShouldLoadVniForV2Palette()
		{
			var w = new BigEndianWriter().Ascii("VPIN").U16(6).U16(1).U32(0);
			WriteVniAnimation(w, "a", "", 1);
			File.WriteAllBytes(Path.Combine(_altColorPath, GameName, "pin2dmd.pal"), new PalBuilder(2).Palette(0, 1, Colors16).Build());
			File.WriteAllBytes(Path.Combine(_altColorPath, GameName, "pin2dmd.vni"), w.ToArray());
			File.WriteAllBytes(Path.Combine(_altColorPath, GameName, "pin2dmd.fsq"), new FsqBuilder(2).Build());

			var loader = new VniLoader(_altColorPath, GameName);
			loader.Load(null);

			loader.Animations.Should().BeOfType<VniFile>();
		}

		[Test]
		public void ShouldLoadFsqFromPac()
		{
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(new FsqFrame { Delay = 10, Mask = Planes.Empty(), Planes = new[] { Planes.With(1), Planes.Empty() } });
			var pal = new PalBuilder(1).Palette(0, 1, Colors16).Mapping(1, SwitchMode.Replace, 0, offset);
			var pac = new BigEndianWriter().Ascii("PAC ").U8(1);
			WritePacChunk(pac, 1, pal.Build());
			WritePacChunk(pac, 2, fsq.Build());
			File.WriteAllBytes(Path.Combine(_altColorPath, GameName, "pin2dmd.pac"), pac.ToArray());

			var loader = new VniLoader(_altColorPath, GameName);
			loader.Load(PacKey);

			loader.Animations.Should().BeOfType<FsqFile>();
			loader.Animations.Find(loader.Pal.Mappings[1]).NumFrames.Should().Be(1);
		}

		[Test]
		public void ShouldUseFsqV3HashTable()
		{
			var data = new byte[Dim.Surface];
			data[1] = 1;
			var trigger = new DmdFrame(Dim, data, 2);
			var crc = FrameUtil.Checksum(trigger.BitPlanes[0]);
			var hashTable = new byte[Planes.Size];
			Buffer.BlockCopy(new[] { crc }, 0, hashTable, 0, 4);
			var fsq = new FsqBuilder(3);
			var offset = fsq.Animation(
				new FsqFrame { Planes = new[] { Planes.Empty(), hashTable } },
				new FsqFrame { Delay = 20, Hash = 0, Mask = Planes.Empty(), Planes = new[] { Planes.With(100), Planes.Empty() } });
			var pal = new PalBuilder(1).Palette(0, 1, Colors16).Mapping(crc, SwitchMode.LayeredColorMask, 0, offset).BuildFile();
			var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal));

			colorizer.Tick(trigger);

			colorizer.Frames.Should().ContainSingle();
			colorizer.Frames[0].Data[100].Should().Be(4);
		}

		[Test]
		public void ShouldLoadHeaderlessFsqFromPac()
		{
			var fsq = new FsqBuilder(0);
			var offset = fsq.Animation(new FsqFrame { Delay = 10, Planes = new[] { Planes.With(1), Planes.Empty() } });
			var pal = new PalBuilder(1).Palette(0, 1, Colors16).Mapping(1, SwitchMode.Replace, 0, offset);
			var pac = new BigEndianWriter().Ascii("PAC ").U8(1);
			WritePacChunk(pac, 1, pal.Build());
			WritePacChunk(pac, 2, fsq.Build());
			File.WriteAllBytes(Path.Combine(_altColorPath, GameName, "pin2dmd.pac"), pac.ToArray());

			var loader = new VniLoader(_altColorPath, GameName);
			loader.Load(PacKey);

			loader.Animations.Should().BeOfType<FsqFile>();
			loader.Animations.Find(loader.Pal.Mappings[1]).NumFrames.Should().Be(1);
		}

		private static void WritePacChunk(BigEndianWriter pac, int type, byte[] data)
		{
			byte[] compressed;
			using (var ms = new MemoryStream()) {
				using (var gzip = new GZipStream(ms, CompressionMode.Compress)) {
					gzip.Write(data, 0, data.Length);
				}
				compressed = ms.ToArray();
			}
			var key = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
			byte[] encrypted;
			using (var aes = Aes.Create()) {
				aes.Padding = PaddingMode.Zeros;
				using (var encryptor = aes.CreateEncryptor(key, key)) {
					encrypted = encryptor.TransformFinalBlock(compressed, 0, compressed.Length);
				}
			}
			pac.U16(type).U32((uint)encrypted.Length).Bytes(encrypted);
		}

		private static void WriteVniAnimation(BigEndianWriter w, string name, string compiledName, int numFrames)
		{
			w.U16(name.Length).Ascii(name);
			w.U16(0).U16(0).U16(0).U8(0).U8(0).U16(0).U16(0).U16(0).U8(0).U8(0);
			w.U16(numFrames);
			w.U16(0).U16(0);                // palette index, no colors (v2)
			w.U8(0);                        // edit mode (v3)
			w.U16(128).U16(32);             // size (v4)
			w.U16(0);                       // no masks (v5)
			w.U8(1).U16(compiledName.Length).Ascii(compiledName).U32(0); // compiled animation (v6)
			for (var i = 0; i < numFrames; i++) {
				w.U16(Planes.Size).U16(40000).U32(0).U8(2).U8(0);
				w.U8(0).Bytes(Planes.Reversed(Planes.With(i)));
				w.U8(1).Bytes(Planes.Reversed(Planes.Empty()));
			}
		}
	}
}
