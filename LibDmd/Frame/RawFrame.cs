namespace LibDmd.Frame
{
	public class RawFrame : DmdFrame
	{
		public byte[][] RawPlanes;
		public byte[][] ExtraRawPlanes;

		public int TotalPlanes => RawPlanes.Length + ExtraRawPlanes.Length;
		public int PlaneSize => RawPlanes.Length > 0 ? RawPlanes[0].Length : 0;

		public byte[][] AllPlanes
		{
			get {
				var planes = new byte[TotalPlanes][];
				for (int i = 0; i < TotalPlanes; i++) {
					if (i < RawPlanes.Length) {
						planes[i] = RawPlanes[i];
					} else {
						planes[i] = ExtraRawPlanes[i - RawPlanes.Length];
					}
				}
				return planes;
			}
		}

		public byte[] RawBuffer
		{
			get {
				var planeSize = PlaneSize;
				var rawBuffer = new byte[TotalPlanes * planeSize];
				for (int i = 0; i < TotalPlanes; i++) {
					if (i < RawPlanes.Length) {
						RawPlanes[i].CopyTo(rawBuffer, i * planeSize);
					} else {
						ExtraRawPlanes[i - RawPlanes.Length].CopyTo(rawBuffer, i * planeSize);
					}
				}
				return rawBuffer;
			}
		}
		public RawFrame Update(Dimensions dim, byte[] data, byte[][]rawPlanes, byte[][] extraRawPlanes)
		{
			Update(dim, data, rawPlanes.Length);
			RawPlanes = rawPlanes;
			ExtraRawPlanes = extraRawPlanes;
			return this;
		}
	}
}
