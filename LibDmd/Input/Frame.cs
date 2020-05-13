namespace LibDmd.Input
{
	public class DmdFrame
	{
		public Dimensions Dimensions;
		public byte[] Data;

		public DmdFrame()
		{
		}

		public DmdFrame(Dimensions dim)
		{
			Dimensions = dim;
			Data = new byte[dim.Surface];
		}

		public DmdFrame Update(byte[] data)
		{
			Data = data;
			return this;
		}

		public DmdFrame Update(Dimensions dim, byte[] data)
		{
			Dimensions = dim;
			Data = data;
			return this;
		}
	}

	public class RawDmdFrame : DmdFrame
	{
		public byte[][] RawPlanes;

		public RawDmdFrame()
		{
		}

		public RawDmdFrame(Dimensions dim) : base(dim)
		{
		}

		public RawDmdFrame Update(Dimensions dim, byte[] data, byte[][]rawPlanes)
		{
			Update(dim, data);
			RawPlanes = rawPlanes;
			return this;
		}

	}
}
