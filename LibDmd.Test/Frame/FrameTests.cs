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
	}
}
