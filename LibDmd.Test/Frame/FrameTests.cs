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
	}
}
