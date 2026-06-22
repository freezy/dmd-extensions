using LibDmd.Output;

// ReSharper disable once CheckNamespace
namespace LibDmd
{
	/// <summary>
	/// Cross-platform (LibDmd.Core) no-op replacement for the telemetry <c>Analytics</c>
	/// singleton.
	///
	/// The real implementation (RudderStack/Raygun + WMI + registry) is Windows-only and
	/// excluded from the core. The plan drops analytics entirely; this stub keeps the call
	/// sites compiling and does nothing.
	/// </summary>
	public class Analytics
	{
		private static Analytics _instance;
		public static Analytics Instance => _instance ?? (_instance = new Analytics());

		public void Disable(bool log = true) { }
		public void Init(string version, string runner) { }
		public void StartGame() { }
		public void SetSource(string source, string gameId) { }
		public void SetSource(string host) { }
		public void ClearSource() { }
		public void AddDestination(IDestination dest) { }
		public void ClearVirtualDestinations() { }
		public void SetColorizer(string name) { }
		public void ClearColorizer() { }
		public void EndGame() { }
	}
}
