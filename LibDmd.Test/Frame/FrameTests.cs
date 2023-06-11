using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class FrameTests : TestBase
	{
	
		[SetUp]
		public void Setup()
		{
		}

		[TestCase]
		public void Should_Correctly_Determine_BitLength()
		{
			4.GetBitLength().Should().Be(2);
			16.GetBitLength().Should().Be(4);
			64.GetBitLength().Should().Be(6);
		}

		[TestCase]
		public void Should_Correctly_Determine_ByteLength()
		{
			2.GetByteLength().Should().Be(1);
			4.GetByteLength().Should().Be(1);
			6.GetByteLength().Should().Be(1);
			24.GetByteLength().Should().Be(3);
		}

		[TestCase]
		public void Should_Correctly_Determine_FrameSize()
		{
			new DmdFrame(128, 32, 2).Data.Length.Should().Be(4096);
			new DmdFrame(128, 32, 4).Data.Length.Should().Be(4096);
			new DmdFrame(128, 32, 6).Data.Length.Should().Be(4096);
			new DmdFrame(128, 32, 8).Data.Length.Should().Be(4096);
			new DmdFrame(128, 32, 24).Data.Length.Should().Be(4096 * 3);
		}

		[TestCase]
		public void Should_Correctly_Compare_Frames()
		{
			var gray1 = FrameGenerator.Random(128, 32, 4);
			var gray2 = FrameGenerator.Random(128, 32, 4);
			var rgb1 = FrameGenerator.Random(128, 32, 24);
			var rgb2 = FrameGenerator.Random(128, 32, 24);
			
			(gray1 == gray2).Should().Be(false);
			(gray1 == gray1).Should().Be(true);
			(gray2 == gray2).Should().Be(true);
			
			(rgb1 == rgb2).Should().Be(false);
			(rgb1 == rgb1).Should().Be(true);
			(rgb2 == rgb2).Should().Be(true);
			
			(rgb1 == gray1).Should().Be(false);

			gray1.Update(gray2);
			(gray1 == gray2).Should().Be(true);
			
			rgb1.Update(gray2);
			(rgb1 == gray2).Should().Be(true);

			// const int num = 1000000;
			// for (var i = 0; i < num; i++) {
			// 	Profiler.Start("Compare1");
			// 	FrameUtil.CompareBuffers1(f1.Data, f1.Data);
			// 	Profiler.Stop("Compare1");
			// 	Profiler.Start("Compare2");
			// 	FrameUtil.CompareBuffers2(f1.Data, f1.Data);
			// 	Profiler.Stop("Compare2");
			// }
			//
			// Profiler.Start("Compare11");
			// for (var i = 0; i < num; i++) {
			// 	FrameUtil.CompareBuffers1(f1.Data, f1.Data);
			// }
			// Profiler.Stop("Compare11");
			//
			// Profiler.Start("Compare22");
			// for (var i = 0; i < num; i++) {
			// 	FrameUtil.CompareBuffers2(f1.Data, f1.Data);
			// }
			// Profiler.Stop("Compare22");
			//
			// AddLogger();
			// Profiler.Print();
			// RemoveLogger();
		}
	}
}
