namespace LibDmd
{
	public class DMDFrame
	{
		public int Width;
		public int Height;
		public byte[] Data;
		public int BitLength;

		public DMDFrame Update(byte[] data, int bitSize)
		{
			Data = data;
			BitLength = bitSize;
			return this;
		}

		public DMDFrame Update(int width, int height, byte[] data, int bitSize)
		{
			Width = width;
			Height = height;
			Data = data;
			BitLength = bitSize;
			return this;
		}
	}

	public class RawDMDFrame : DMDFrame
	{
		public byte[][] RawPlanes;

		public RawDMDFrame Update(int width, int height, byte[] data, byte[][] rawPlanes)
		{
			this.Update(width, height, data, rawPlanes.Length);
			this.RawPlanes = rawPlanes;
			return this;
		}



	}
}
