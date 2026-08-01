using LibDmd.Common;

namespace LibDmd.DmdDevice
{
	/// <summary>
	/// Configuration of a single colorization plugin.
	/// </summary>
	/// <remarks>
	/// Extracted from <c>Configuration.cs</c> so the cross-platform core (LibDmd.Core) can
	/// reference it (it's used by the colorization loader/plugin) without pulling in the
	/// full INI/SkiaSharp configuration layer.
	/// </remarks>
	public class PluginConfig
	{
		public readonly string Path;
		public readonly bool PassthroughEnabled;
		public readonly ScalerMode ScalerMode;

		public PluginConfig(string path, bool passthroughEnabled, ScalerMode scalerMode)
		{
			Path = path;
			PassthroughEnabled = passthroughEnabled;
			ScalerMode = scalerMode;
		}
	}
}
