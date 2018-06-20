using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using LibDmd;
using LibDmd.Input;
using LibDmd.Processor;

namespace DmdExt.Common
{
	abstract class BaseOptions
	{
		[Option('d', "destination", HelpText = "The destination where the DMD data is sent to. One of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd, virtual ]. Default: \"auto\", which outputs to all available devices.")]
		public DestinationType Destination { get; set; } = DestinationType.Auto;

		[Option('r', "resize", HelpText = "How the source image is resized. One of: [ stretch, fill, fit ]. Default: \"stretch\".")]
		public ResizeMode Resize { get; set; } = ResizeMode.Stretch;

		[Option("no-virtual", HelpText = "Explicitly disables the virtual DMD when destination is \"auto\". Default: false.")]
		public bool NoVirtualDmd { get; set; } = false;

		[Option("virtual-stay-on-top", HelpText = "Makes the virtual DMD stay on top of other application windows. Default: false.")]
		public bool VirtualDmdOnTop { get; set; } = false;

		[Option("virtual-hide-grip", HelpText = "Hides the resize grip of the virtual DMD. Default: false.")]
		public bool VirtualDmdHideGrip { get; set; } = false;

		[OptionArray("virtual-position", HelpText = "Position and size of virtual DMD. Four values: <Left> <Top> <Width> [<Height>]. Height is optional and can be used for custom aspect ratio. Default: \"0 0 1024\".")]
		public int[] VirtualDmdPosition { get; set; } = { 0, 0, 1024 };

		[Option("virtual-dotsize", HelpText = "Scale the dot size of the virtual DMD. Default: 1")]
		public double VirtualDmdDotSize { get; set; } = 1;

		[Option('c', "color", HelpText = "Sets the color of a grayscale source that is rendered on an RGB destination. Default: ff3000")]
		public string RenderColor { get; set; } = "ff3000";

		[Option("flip-x", HelpText = "Flips the image horizontally (left/right). Default: false.")]
		public bool FlipHorizontally { get; set; } = false;

		[Option("flip-y", HelpText = "Flips the image vertically (top/down). Default: false.")]
		public bool FlipVertically { get; set; } = false;

		[Option('p', "port", HelpText = "Force COM port for PinDMDv3 devices. Example: \"COM3\".")]
		public string Port { get; set; } = null;

		[Option('q', "quit-when-done", HelpText = "Exit the program when finished, e.g. when Pinball FX2 doesn't receive any frames anymore. Default: false")]
		public bool QuitWhenDone { get; set; } = false;

		[Option("quit-after", HelpText = "Exit after n milliseconds. If set to -1, waits indefinitely or until source finishes when -q used. Default: -1")]
		public int QuitAfter { get; set; } = -1;

		[Option("no-clear", HelpText = "Don't clear screen when quitting. Default: false.")]
		public bool NoClear { get; set; } = false;

		[Option('o', "output-to-file", HelpText = "If set, writes all frames as PNG bitmaps to the provided folder.")]
		public string SaveToFile { get; set; }

		[Option("pinup", HelpText = "If set, enable output to PinUP. The value is the name of the game.")]
		public string PinUp { get; set; } = null;

		public enum DestinationType
		{
			Auto, PinDMDv1, PinDMDv2, PinDMDv3, PIN2DMD, Virtual
		}
	}
}
