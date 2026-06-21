using System.Windows.Media;
using FluentAssertions;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class PaletteTests
	{
		[TestCase]
		public void Should_Reject_All_Black_Palette()
		{
			DmdDevice.DmdDevice.IsUsablePalette(new[] {
				Colors.Black,
				Color.FromRgb(0, 0, 0),
				Color.FromArgb(0, 0, 0, 0)
			}).Should().BeFalse();
		}

		[TestCase]
		public void Should_Reject_Empty_Palette()
		{
			DmdDevice.DmdDevice.IsUsablePalette(new Color[] {}).Should().BeFalse();
		}

		[TestCase]
		public void Should_Accept_Palette_With_A_Non_Black_Color()
		{
			DmdDevice.DmdDevice.IsUsablePalette(new[] {
				Colors.Black,
				Colors.OrangeRed
			}).Should().BeTrue();
		}

		[TestCase]
		public void Should_Reject_Black_Color()
		{
			DmdDevice.DmdDevice.IsUsableColor(Colors.Black).Should().BeFalse();
		}

		[TestCase]
		public void Should_Accept_Non_Black_Color()
		{
			DmdDevice.DmdDevice.IsUsableColor(Colors.OrangeRed).Should().BeTrue();
		}
	}
}
