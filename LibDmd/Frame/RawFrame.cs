namespace LibDmd.Frame
{
	public class RawFrame : DmdFrame
	{
		public byte[][] RawPlanes;

		public RawFrame Update(Dimensions dim, byte[] data, byte[][]rawPlanes)
		{
			Update(dim, data, rawPlanes.Length);
			RawPlanes = rawPlanes;
			return this;
		}
	}
}
