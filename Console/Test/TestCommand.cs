using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using App;
using Console.Common;

namespace Console.Test
{
	class TestCommand : ICommand
	{
		private readonly TestOptions _options;
		public TestCommand(TestOptions options)
		{
			_options = options;
		}

		public void Execute()
		{
			var virtualDmd = new VirtualDmd();
			DmdExt.WinApp.Run(virtualDmd);
		}
	}
}
