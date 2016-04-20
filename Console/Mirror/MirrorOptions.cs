using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Console.Common;

namespace Console.Mirror
{
	class MirrorOptions : BaseOptions
	{
		[Option('s', "source", Required = true, HelpText = "The source you want to retrieve DMD data from. One of: [ pinballfx2, screen ].")]
		public SourceType Source { get; set; }

		[Option('f', "fps", HelpText = "How many frames per second should be mirrored. Default: 25")]
		public double FramesPerSecond { get; set; } = 25d;

		[OptionArray('p', "position", HelpText = "Position and size of screen grabber source. Four values: <Left> <Top> <Width> <Height>. Default: \"0 0 128 32\".")]
		public int[] Position { get; set; } = { 0, 0, 128, 32 };

		[Option("grid-spacing", HelpText = "How much of the white space around the dot should be cut off. 1 means same size as the dot, 0.5 half size, etc. 0 for disable. Default: 1.")]
		public double GridSpacing { get; set; } = 1d;

		[OptionArray("grid-size", HelpText = "Number of horizontal and vertical dots when removing grid spacing. Two values: <Width> <Height>. Default: \"128 32\".")]
		public int[] GridSize { get; set; } = { 128, 32 };

		[Option("no-shading", HelpText = "Disabled shading, i.e. artificial downsampling for RGB displays. Default: false.")]
		public bool DisableShading { get; set; } = false;

		[Option("shading-numshades", HelpText = "Number of shades for artifical downsampling for RGB displays. Default: 4")]
		public int NumShades { get; set; } = 4;

		[Option("shading-intensity", HelpText = "Multiplies luminosity of the parsed dot so it covers the whole spectrum before downsampling. Default: 2.5.")]
		public double ShadeIntensity { get; set; } = 2.5;

		[Option("shading-brightness", HelpText = "Adds luminosity to the parsed dot after being multiplied. Useful if even black dots should be slightly illuminated. Default: 0.1.")]
		public double ShadeBrightness { get; set; } = 0.1;


		[ParserState]
		public IParserState LastParserState { get; set; }
	}

	enum SourceType
	{
		PinballFX2,
		Screen
	}

}
