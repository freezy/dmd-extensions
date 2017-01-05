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
	public interface IConverter<out T>
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
		T Convert(byte[] from);

		/// <summary>
		/// Must be run when dimensions of the source change.
		/// </summary>
		/// <param name="width">New width of the source</param>
		/// <param name="height">New height of the source</param>
		void SetDimensions(int width, int height);
	}
}
