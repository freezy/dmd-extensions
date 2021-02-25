using System;

namespace LibDmd.Output.Virtual.Dmd
{
	internal static class Global
	{
		public static Uri MakePackUri(string relativeFile)
		{
			var uriString = "pack://application:,,,/" + AssemblyShortName + ";component/" + relativeFile;
			return new Uri(uriString);
		}

		private static string AssemblyShortName
		{
			get
			{
				if (_assemblyShortName == null)
				{
					var a = typeof(Global).Assembly;
					_assemblyShortName = a.ToString().Split(',')[0];
				}
				return _assemblyShortName;
			}
		}

		private static string _assemblyShortName;
	}
}
