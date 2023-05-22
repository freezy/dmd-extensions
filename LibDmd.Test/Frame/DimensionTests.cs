using FluentAssertions;
using LibDmd.Frame;
using NUnit.Framework;

// ReSharper disable EqualExpressionComparison

namespace LibDmd.Test
{
	[TestFixture]
	public class DimensionTests : TestBase
	{
	
		[SetUp]
		public void Setup()
		{
		}

		[TestCase]
		public void Should_Correctly_Apply_Operators()
		{
			new Dimensions(12, 48).Should().Be(new Dimensions(12, 48));
			(new Dimensions(128, 32) / 2).Should().Be(new Dimensions(64, 16));
			(new Dimensions(128, 32) * 2).Should().Be(new Dimensions(256, 64));
			(2 * new Dimensions(128, 32)).Should().Be(new Dimensions(256, 64));
			(new Dimensions(128, 32).Equals(128, 32)).Should().Be(true);
			(new Dimensions(128, 32).Equals(128, 31)).Should().Be(false);
			(new Dimensions(128, 32) == new Dimensions(128, 32)).Should().Be(true);
			(new Dimensions(128, 32) == new Dimensions(128, 31)).Should().Be(false);
			(new Dimensions(128, 32) == new Dimensions(127, 32)).Should().Be(false);
			(new Dimensions(128, 32) == new Dimensions(32, 128)).Should().Be(false);
			(new Dimensions(128, 32) != new Dimensions(128, 31)).Should().Be(true);
			(new Dimensions(128, 32) != new Dimensions(128, 32)).Should().Be(false);
			(new Dimensions(128, 32) > new Dimensions(128, 31)).Should().Be(true);
			(new Dimensions(128, 32) > new Dimensions(128, 32)).Should().Be(false);
			(new Dimensions(128, 32) < new Dimensions(128, 33)).Should().Be(true);
			(new Dimensions(128, 32) < new Dimensions(128, 32)).Should().Be(false);
			new Dimensions(128, 32).Surface.Should().Be(128 * 32);
		}
	}
}
