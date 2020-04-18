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
	}

	public class VpmRawDMDFrame : DMDFrame
	{
		public byte[][] RawPlanes;
	}
}
