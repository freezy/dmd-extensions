using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd
{
	public class DMDFrame
	{
		public int width;
		public int height;
		public byte[] Data;

		public DMDFrame Update(byte[] Data)
		{
			this.Data = Data;
			return this;
		}

		public DMDFrame Update(int width, int height, byte[] Data)
		{
			this.width = width;
			this.height = height;
			this.Data = Data;
			return this;
		}
	}

	public class RawDMDFrame : DMDFrame
	{
		public byte[][] RawPlanes;

		public RawDMDFrame Update(int width, int height, byte[] Data, byte[][] RawPlanes)
		{
			this.Update(width, height, Data);
			this.RawPlanes = RawPlanes;
			return this;
		}
	}
}
