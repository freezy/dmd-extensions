using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DmdExt.Common;
using LibDmd;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Input.FileSystem;
using LibDmd.Input.Network;
using LibDmd.Input.Passthrough;
using LibDmd.Output;

namespace DmdExt.Server
{
	class ServerCommand : BaseCommand
	{
		private readonly IConfiguration _config;
		private readonly ServerOptions _serverOptions;

		public ServerCommand(IConfiguration config, ServerOptions serverOptions) {
			_config = config;
			_serverOptions = serverOptions;
			if (_serverOptions.SkipAnalytics) {
				Analytics.Instance.Disable();
			}
		}

		protected override void CreateRenderGraphs(RenderGraphCollection graphs, HashSet<string> reportingTags)
		{
			var renderers = GetRenderers(_config, reportingTags);
			var websocketServer = new WebsocketServer(_serverOptions.Ip, _serverOptions.Port, _serverOptions.Path);
			websocketServer.SetupGraphs(graphs, renderers);
			reportingTags.Add("Server");
		}
	}
}
