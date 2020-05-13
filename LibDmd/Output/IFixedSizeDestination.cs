using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Input;

namespace LibDmd.Output
{
	/// <summary>
	/// Indicates that an output device's pixel dimensions are constant.
	/// </summary>
	///
	/// <remarks>
	/// By constant we mean it doesn't change, meaning if the source happens to
	/// change dimensions, the render graph will need to resize the data.
	/// </remarks>
	public interface IFixedSizeDestination
	{
		/// <summary>
		/// Fixed size in pixels, or dots.
		/// </summary>
		Dimensions FixedSize { get; }
	}
}
