using LibDmd.DmdDevice;
using NUnit.Framework;

namespace LibDmd.Test
{
	//[TestFixture]
	public class DmdDeviceTests : TestBase
	{
		[SetUp]
		public void Setup()
		{
			AddLogger();
		}

		[TearDown]
		public void Teardown()
		{
			RemoveLogger();
		}

		//[TestCase]
		public void Should_Pass_Through_A_Gray2_Frame() {

			var dmdDevice = new DmdDevice.DmdDevice();
			dmdDevice.SetGameName("test");

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			dmdDevice.RenderGray2(frame);
			dmdDevice.Close();
		}


		//[TestCase]
		public void Should_Pass_Through_An_Alphanumeric_Frame()
		{
			var dmdDevice = new DmdDevice.DmdDevice();
			dmdDevice.SetGameName("test");

			var segData = FrameGenerator.AlphaNumericData(
				0xff, 0xfe, 0xfd, 0xfc, 0xfb, 0xfa, 0xf9, 0xf8,
				0xf7, 0xf6, 0xf5, 0xf4, 0xf2, 0xf1, 0xf1, 0xf0,
				0x8f, 0x8e, 0x8d, 0x8c, 0x8b, 0x8a, 0x89, 0x88,
				0x87, 0x86, 0x85, 0x84, 0x82, 0x81, 0x81, 0x80
			);

			dmdDevice.RenderAlphaNumeric(NumericalLayout.__2x16Alpha, segData, new ushort[] {});
			dmdDevice.Close();
		}

		//[TestCase]
		public void Should_Pass_Through_An_Alphanumeric_Frame2()
		{
			var config = new TestConfiguration();

			(config.Global as TestGlobalConfig).Colorize = true;

			var dmdDevice = new DmdDevice.DmdDevice(config);
			dmdDevice.SetGameName("test");

			var segData = FrameGenerator.AlphaNumericData(
				0xff, 0xfe, 0xfd, 0xfc, 0xfb, 0xfa, 0xf9, 0xf8,
				0xf7, 0xf6, 0xf5, 0xf4, 0xf2, 0xf1, 0xf1, 0xf0,
				0x8f, 0x8e, 0x8d, 0x8c, 0x8b, 0x8a, 0x89, 0x88,
				0x87, 0x86, 0x85, 0x84, 0x82, 0x81, 0x81, 0x80
			);

			dmdDevice.RenderAlphaNumeric(NumericalLayout.__2x16Alpha, segData, new ushort[] {});
			dmdDevice.Close();
		}
	}
}
