using System;

namespace LibDmd.Common
{
	/// <summary>
	/// Thrown when input data doesn't match the expected format.
	/// </summary>
	/// <remarks>
	/// Moved here from <c>Input/FileSystem/ImageSource.cs</c> so the cross-platform core
	/// (the VNI loader) can reference it without the dropped bitmap/file-system sources.
	/// </remarks>
	public class WrongFormatException : Exception
	{
		public WrongFormatException(string message) : base(message)
		{
		}
	}
}
