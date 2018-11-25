using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLog;
using SkiaSharp.Extended.Svg;
using WebSocketSharp;
using Logger = NLog.Logger;

namespace LibDmd.Output.Virtual
{
	class AlphaNumericResources
	{
		public static int Full = 99;
		public ISubject<Dictionary<int, SKSvg>> AlphaNumericThinLoaded = new Subject<Dictionary<int, SKSvg>>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static AlphaNumericResources _instance;
		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		private readonly Dictionary<int, SKSvg> _alphaNumericThin = new Dictionary<int, SKSvg>();

		public static AlphaNumericResources GetInstance()
		{
			return _instance ?? (_instance = new AlphaNumericResources());
		}

		private AlphaNumericResources()
		{
			LoadSvgs();
		}

		private void LoadSvgs()
		{
			// load svgs from packages resources
			const string prefix = "LibDmd.Output.Virtual.alphanum_thin_inner.";
			//const string prefix = "LibDmd.Output.Virtual.alphanum.";
			var segmentFileNames = new[] {
				$"{prefix}00-top.svg",
				$"{prefix}01-top-right.svg",
				$"{prefix}02-bottom-right.svg",
				$"{prefix}03-bottom.svg",
				$"{prefix}04-bottom-left.svg",
				$"{prefix}05-top-left.svg",
				$"{prefix}06-middle-left.svg",
				$"{prefix}07-comma.svg",
				$"{prefix}08-diag-top-left.svg",
				$"{prefix}09-center-top.svg",
				$"{prefix}10-diag-top-right.svg",
				$"{prefix}11-middle-right.svg",
				$"{prefix}12-diag-bottom-right.svg",
				$"{prefix}13-center-bottom.svg",
				$"{prefix}14-diag-bottom-left.svg",
				$"{prefix}15-dot.svg",
			};
			Logger.Info("Loading segment SVGs...");
			for (var i = 0; i < segmentFileNames.Length; i++) {
				var svg = new SKSvg();
				svg.Load(_assembly.GetManifestResourceStream(segmentFileNames[i]));
				_alphaNumericThin.Add(i, svg);
			}
			var full = new SKSvg();
			full.Load(_assembly.GetManifestResourceStream($"{prefix}full.svg"));
			_alphaNumericThin.Add(Full, full);
			AlphaNumericThinLoaded.OnNext(_alphaNumericThin);
			AlphaNumericThinLoaded = new BehaviorSubject<Dictionary<int, SKSvg>>(_alphaNumericThin);
		}
	}
}
