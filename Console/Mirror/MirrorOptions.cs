using CommandLine;
using DmdExt.Common;
using LibDmd.Common;

namespace DmdExt.Mirror
{
	class MirrorOptions : BaseOptions
	{
		[Option('s', "source", Required = true, HelpText = "The source you want to retrieve DMD data from. One of: [ pinballfx2, pinballfx3, pinballfxclassic, pinballarcade, propinball, futurepinball, screen ].")]
		public SourceType Source { get; set; }

		[Option('f', "fps", HelpText = "How many frames per second should be mirrored. Default: 25")]
		public double FramesPerSecond { get; set; } = 25d;

		[Option("idle-after", HelpText = "Wait for number of milliseconds until clearing the screen. Disable with 0. Default: 0.")]
		public int IdleAfter { get; set; } = 0;

		[Option("idle-play", HelpText = "Play this file while idleing instead of blank screen. Supported formats: JPG, PNG, GIF. Animated GIFs are supported.")]
		public string IdlePlay { get; set; }

		[OptionArray("position", HelpText = "[screen] Position and size of screen grabber source. Four values: <Left> <Top> <Width> <Height>. Default: \"0 0 128 32\".")]
		public int[] Position { get; set; } = { 0, 0, 128, 32 };

		[OptionArray("resize-to", HelpText = "[screen] Resize captured screen to this size. Two values: <Width> <Height>. Default: \"128 32\".")]
		public int[] ResizeTo { get; set; } = { 128, 32 };

		[Option("grid-spacing", HelpText = "[screen] How much of the white space around the dot should be cut off (grid size is defined by --resize-to). 1 means same size as the dot, 0.5 half size, etc. 0 for disable. Default: 0.")]
		public double GridSpacing { get; set; } = 0d;

		[Option("propinball-args", HelpText = "[propinball] Arguments send from the Pro Pinball master process. Usually something like: \"ndmd w0_0_0_0_w m392\". Will be set automatically when called through Pro Pinball.")]
		public string ProPinballArgs { get; set; } = "ndmd w0_0_0_0_w m392";

		[Option("fx3-legacy", HelpText = "[pinballfx3] If set, don't use the memory grabber but the legacy screen grabber, like Pinball FX2. Default: false.")]
		public bool Fx3GrabScreen { get; set; } = false;

		[OptionArray("colors", HelpText = "[futurepinball] Static DMD palette colors. Provide five or sixteen RGB hex colors, e.g. \"#000000\" \"#8E5525\" \"#F6B832\" \"#B95B00\" \"#F3EEC4\".")]
		public string[] Colors { get; set; } = new string[] {};

		[ParserState]
		public IParserState LastParserState { get; set; }

		public new void Validate()
			{
				if (Source == SourceType.PinballFXClassic) {
					Source = SourceType.PinballFX3;
				}

				base.Validate();

			if (Colors.Length > 0) {
				if (Source != SourceType.FuturePinball) {
					throw new InvalidOptionException("Argument --colors is only supported with --source futurepinball.");
				}
				if (Colors.Length != 5 && Colors.Length != 16) {
					throw new InvalidOptionException("Argument --colors must contain five or sixteen RGB colors.");
				}
				foreach (var color in Colors) {
					if (!ColorUtil.IsColor(color)) {
						throw new InvalidOptionException("Argument --colors must contain valid RGB colors. Example: \"#ff0000\".");
					}
				}
			}

			if (Source == SourceType.Screen) {
				if (Position.Length != 4)
				{
					throw new InvalidOptionException(
						"Argument --position must have four values: \"<Left> <Top> <Right> <Bottom>\".");
				}

				if (ResizeTo.Length != 2)
				{
					throw new InvalidOptionException("Argument --resize-to must have two values: \"<Width> <Height>\".");
				}

				var width = Position[2] - Position[0];
				var height = Position[3] - Position[1];
				if (width < 0 )
				{
					Position[2] += Position[0];
				}
				if (height < 0)
				{
					Position[3] += Position[1];
				}
			}
		}
	}

	enum SourceType
	{
		PinballFX2,
		PinballFX3,
		PinballFXClassic,
		Screen,
		PinballArcade,
		ProPinball,
		FuturePinball
	}
}
