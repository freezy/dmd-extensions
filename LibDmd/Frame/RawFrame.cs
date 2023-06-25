namespace LibDmd.Frame
{
	public class RawFrame : DmdFrame
	{
		public byte[][] RawPlanes;
		public byte[][] ExtraRawPlanes;

		public int TotalPlanes => RawPlanes.Length + ExtraRawPlanes.Length;
		public int PlaneSize => RawPlanes.Length > 0 ? RawPlanes[0].Length : 0;

		public RawFrame Update(Dimensions dim, byte[] data, byte[][]rawPlanes, byte[][] extraRawPlanes)
		{
			Update(dim, data, rawPlanes.Length);
			RawPlanes = rawPlanes;
			ExtraRawPlanes = extraRawPlanes;
			return this;
		}
	}
}
