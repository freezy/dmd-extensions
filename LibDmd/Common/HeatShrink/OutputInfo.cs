namespace LibDmd.Common.HeatShrink
{
	public class OutputInfo
	{
		/// <summary>
		/// output buffer
		/// </summary>
		public byte[] Buf;
		/// <summary>
		/// buffer size, redundant in c#
		/// </summary>
		public int BufSize;
		/// <summary>
		/// bytes pushed to buffer, so far
		/// </summary>
		public int OutputSize;

		public override string ToString()
		{
			return $"OutputInfo [buf_size={BufSize}, output_size={OutputSize}]";
		}
	}
}
