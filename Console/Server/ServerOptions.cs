using CommandLine;
using DmdExt.Common;
using LibDmd;

namespace DmdExt.Server
{
	class ServerOptions : BaseOptions
	{
		[Option("host", HelpText = "WebSocket host to listen to. Default: ws://localhost")]
		public string Host { get; set; } = "ws://localhost";

		[Option("path", HelpText = "WebSocket path. Default: /dmd")]
		public string Path { get; set; } = "/dmd";
	}
}
