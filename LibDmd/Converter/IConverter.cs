using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Converter
{
	/// <summary>
	/// Converts a frame from a given bit length to another bit length.
	/// </summary>
	public interface IConverter
	{
		/// <summary>
		/// Source bit length
		/// </summary>
		RenderBitLength From { get; }

		/// <summary>
		/// Destination bit length
		/// </summary>
		RenderBitLength To { get; }

		/// <summary>
		/// Converts from source to destination
		/// </summary>
		/// <param name="from">Source data</param>
		byte[] Convert(byte[] from);
	}
}
