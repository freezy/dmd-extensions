using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input
{
	interface IRawSource
	{
		/// <summary>
		/// A display name for the source
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Returns an observable that produces raw data that is sent directly to the display.
		/// </summary>
		IObservable<byte[]> GetRawdata();
	}
}
