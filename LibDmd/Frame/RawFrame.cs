using LibDmd.DmdDevice;
using LibDmd.Input;

namespace LibDmd.Frame
{
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
