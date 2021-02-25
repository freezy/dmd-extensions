namespace LibDmd.Output
{
	/// <summary>
	/// Indicates that the output device can adapt its pixel size in real time.
	/// </summary>
	/// 
	/// <remarks>
	/// This means that output device can at any moment receive notifications
	/// that the frame source has changed dimensions. Usually only possible 
	/// for the virtual DMD.
	/// </remarks>
	public interface IResizableDestination
	{
		/// <summary>
		/// The next frame will come with new dimensions.
		/// </summary>
		/// <param name="width">Width in pixels</param>
		/// <param name="height">Height in pixels</param>
		void SetDimensions(int width, int height);
	}
}
