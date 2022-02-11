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
		/// Width of the output device in pixels.
		/// </summary>
		int DmdWidth { get; }

		/// <summary>
		/// Height of the output device in pixels.
		/// </summary>
		int DmdHeight { get; }

		/// <summary>
		/// Allow scaling to HD for this destination.
		/// </summary>
		bool DmdAllowHdScaling { get; }
	}
}
