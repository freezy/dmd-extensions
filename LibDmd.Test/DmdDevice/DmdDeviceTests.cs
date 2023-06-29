// ReSharper disable PossibleNullReferenceException

using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibDmd.Common;
using LibDmd.DmdDevice;
using NLog;
using NUnit.Framework;

namespace LibDmd.Test
{
	//[TestFixture]
	public class DmdDeviceTests : TestBase
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

			(config.VirtualAlphaNumericDisplay as TestVirtualAlphaNumericDisplayConfig).Enabled = true;
			(config.Global as TestGlobalConfig).Colorize = true;

			var dmdDevice = new DmdDevice.DmdDevice(config);
			dmdDevice.SetGameName("test");
			dmdDevice.SetColorize(true);

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
		public async Task Should_Colorize_An_Alphanumeric_Frame()
		{
			var config = new TestConfiguration();

			(config.VirtualAlphaNumericDisplay as TestVirtualAlphaNumericDisplayConfig).Enabled = true;
			(config.Global as TestGlobalConfig).Colorize = true;
			(config.Global as TestGlobalConfig).Plugins = new [] {
				new PluginConfig(@"L:\Visual Pinball\VPinMAME\pin2color.dll", false, ScalerMode.Doubler)
			};


			var dmdDevice = new DmdDevice.DmdDevice(config);
			dmdDevice.SetGameName("bttf_a27");
			dmdDevice.SetColorize(true);
			dmdDevice.Init();

			var segData = new ushort[] {
				5120, 5120, 5120, 0, 10767, 2167, 57, 5232, 0, 8705, 63, 0, 0, 0, 0, 0, 0, 0, 8705, 2166, 121, 0,
				113, 62, 8705, 62, 6259, 121, 0, 16640, 16640, 16640, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
			};

			Logger.Info("Starting thread");
			await Task.Run(() => {
				Thread.CurrentThread.IsBackground = true;


				dmdDevice.RenderAlphaNumeric(NumericalLayout.__2x16Alpha, segData, new ushort[] {});
				Logger.Info("Sleeping..");
				Thread.Sleep(1000);
				Logger.Info("Continuing");
				dmdDevice.Close();
			});

			await Observable.Timer(TimeSpan.FromMilliseconds(2000)).FirstAsync();
		}
	}
}
