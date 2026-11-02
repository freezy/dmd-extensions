using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Converter.Serum;
using LibDmd.Frame;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class SerumApiV2Tests : TestBase
	{
		private IntPtr _frame32;
		private IntPtr _frame64;

		[SetUp]
		public void Setup()
		{
			_frame32 = Marshal.AllocHGlobal(128 * 32 * 2);
			_frame64 = Marshal.AllocHGlobal(256 * 64 * 2);
		}

		[TearDown]
		public void TearDown()
		{
			Marshal.FreeHGlobal(_frame32);
			Marshal.FreeHGlobal(_frame64);
		}

		[TestCase(ScalerMode.None)]
		[TestCase(ScalerMode.Scale2x)]
		public void Should_Skip_Frames_Without_Width(ScalerMode scalerMode)
		{
			var frames = new Subject<DmdFrame>();
			var received = new List<Dimensions>();
			frames.Subscribe(f => received.Add(f.Dimensions));

			var serumFrame = CreateFrame(128, 256);
			var api = new SerumApiV2(frames, scalerMode, ref serumFrame);

			var emptyFrame = CreateFrame(0, 0);
			api.Convert(ref emptyFrame);
			api.UpdateRotations(ref emptyFrame, null, 0x10000);

			received.Should().BeEmpty();
		}

		[TestCase(ScalerMode.None, 128, 0, 128, 32)]
		[TestCase(ScalerMode.None, 0, 256, 256, 64)]
		[TestCase(ScalerMode.Scale2x, 128, 256, 256, 64)]
		[TestCase(ScalerMode.Scale2x, 128, 0, 128, 32)]
		public void Should_Push_Available_Resolution(ScalerMode scalerMode, int width32, int width64, int expectedWidth, int expectedHeight)
		{
			var frames = new Subject<DmdFrame>();
			var received = new List<Dimensions>();
			frames.Subscribe(f => received.Add(f.Dimensions));

			var serumFrame = CreateFrame(width32, width64);
			var api = new SerumApiV2(frames, scalerMode, ref serumFrame);
			api.Convert(ref serumFrame);

			received.Should().Equal(new Dimensions(expectedWidth, expectedHeight));
		}

		private SerumFrame CreateFrame(int width32, int width64)
		{
			return new SerumFrame {
				SerumVersion = 2,
				Frame32Data = _frame32,
				Width32 = (uint)width32,
				Frame64Data = _frame64,
				Width64 = (uint)width64,
			};
		}
	}
}
