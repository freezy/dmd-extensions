using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Console.Common;

namespace Console.Mirror
{
	class MirrorCommand : ICommand
	{
		private readonly MirrorOptions _options;
		public MirrorCommand(MirrorOptions options)
		{
			_options = options;
		}

		public void Execute()
		{
			throw new NotImplementedException();
		}
	}
}
