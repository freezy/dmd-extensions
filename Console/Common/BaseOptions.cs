using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace Console.Common
{
	abstract class BaseOptions
	{
		[Option('d', "destination", HelpText = "The destination where the DMD data is sent to. One of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd, virtual ]. Default: \"auto\", which outputs to all available devices.")]
		public DestinationType Destination { get; set; } = DestinationType.Auto;

		[Option("no-virtual", HelpText = "Explicitly disables the virtual DMD when destination is \"auto\". Default: false.")]
		public bool NoVirtualDmd { get; set; } = false;

		[Option("flip-x", HelpText = "Flips the image horizontally. Default: false.")]
		public bool FlipHorizontally { get; set; } = false;

		[Option("flip-y", HelpText = "Flips the image vertically. Default: false.")]
		public bool FlipVertically { get; set; } = false;

		public enum DestinationType
		{
			Auto, PinDMDv1, PinDMDv2, PinDMDv3, PIN2DMD, Virtual
		}
	}
}
