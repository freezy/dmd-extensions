using System.Collections.Generic;
using LibDmd.Frame;

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
		IEnumerable<FrameFormat> From { get; }

		/// <summary>
		/// Receives frames and outputs them to the output sources the converter implements.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that if your convertor doesn't implement any ISource interface,
		/// frames will just be dropped.
		/// 
		/// If this method doesn't send anything to its output sources, the frame is
		/// equally dropped.
		/// </remarks>
		/// 
		/// <param name="frame">Source frame</param>
		void Convert(DmdFrame frame);

		/// <summary>
		/// Receives alphanumeric frames and converts them into colored DMD frames (if supported).
		/// </summary>
		/// <param name="frame">Source frame</param>
		void Convert(AlphaNumericFrame frame);

		/// <summary>
		/// Initializes the converter. Run before rendering is started and after
		/// Dimensions have been initialized.
		/// </summary>
		/// <returns></returns>
		void Init();
	}
}
