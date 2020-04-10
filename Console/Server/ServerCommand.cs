using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DmdExt.Common;
using LibDmd;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Input.FileSystem;
using LibDmd.Input.PinMame;
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
		}

		protected override void CreateRenderGraphs(RenderGraphCollection graphs)
		{
		}
	}
}
