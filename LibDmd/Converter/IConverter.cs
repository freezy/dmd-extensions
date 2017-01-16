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
		FrameFormat From { get; }

		/// <summary>
		/// Destination bit length
		/// </summary>
		//FrameFormat To { get; }

		/// <summary>
		/// Converts from source to destination
		/// </summary>
		/// <param name="from">Source data</param>
		void Convert(byte[] from);

		/// <summary>
		/// Initializes the converter. Run before rendering is started and after
		/// Dimensions have been initialized.
		/// </summary>
		/// <returns></returns>
		void Init();

		/// <summary>
		/// Must be run when dimensions of the source change.
		/// </summary>
		/// <param name="width">New width of the source</param>
		/// <param name="height">New height of the source</param>
		void SetDimensions(int width, int height);
	}
}
