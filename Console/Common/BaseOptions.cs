using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using LibDmd.Processor;

namespace Console.Common
{
	abstract class BaseOptions
	{
		[Option('d', "destination", HelpText = "The destination where the DMD data is sent to. One of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd, virtual ]. Default: \"auto\", which outputs to all available devices.")]
		public DestinationType Destination { get; set; } = DestinationType.Auto;

		[Option('r', "resize", HelpText = "How the source image is resized. One of: [ stretch, fill, fit ]. Default: \"stretch\".")]
		public TransformationProcessor.ResizeMode Resize { get; set; } = TransformationProcessor.ResizeMode.Stretch;

		[Option("no-virtual", HelpText = "Explicitly disables the virtual DMD when destination is \"auto\". Default: false.")]
		public bool NoVirtualDmd { get; set; } = false;

		[Option("virtual-stay-on-top", HelpText = "Makes the virtual DMD stay on top of other application windows. Default: false.")]
		public bool VirtualDmdOnTop { get; set; } = false;

		[Option("virtual-hide-grip", HelpText = "Hides the resize grip of the virtual DMD. Default: false.")]
		public bool VirtualDmdHideGrip { get; set; } = false;

		[OptionArray("virtual-position", HelpText = "Position and size of virtual DMD. Three values: <Left> <Top> <Width>. Default: \"0 0 1024\".")]
		public int[] VirtualDmdPosition { get; set; } = { 0, 0, 1024 };

		[Option("use-gray4", HelpText = "Sends frames in 4-bit grayscale to the display if supported. Default: false")]
		public bool RenderAsGray4 { get; set; } = false;

		[Option("flip-x", HelpText = "Flips the image horizontally. Default: false.")]
		public bool FlipHorizontally { get; set; } = false;

		[Option("flip-y", HelpText = "Flips the image vertically. Default: false.")]
		public bool FlipVertically { get; set; } = false;

		[Option('q', "quit-when-done", HelpText = "Exit the program when finished, e.g. when Pinball FX2 doesn't receive any frames anymore. Default: false")]
		public bool QuitWhenDone { get; set; } = false;

		[Option('o', "output-to-file", HelpText = "If set, writes all frames as PNG bitmaps to the provided folder.")]
		public string SaveToFile { get; set; }

		public enum DestinationType
		{
			Auto, PinDMDv1, PinDMDv2, PinDMDv3, PIN2DMD, Virtual
		}
	}
}
