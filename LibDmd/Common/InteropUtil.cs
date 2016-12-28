using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Common
{
	public class InteropUtil
	{
		public static ushort[] ReadUInt16Array(IntPtr data, int length)
		{
			var buffer = new ushort[length];
			var byteBuffer = new byte[length * 2];
			Marshal.Copy(data, byteBuffer, 0, length * 2);
			var pos = 0;
			for (var i = 0; i < length; i += 2) {
				buffer[pos++] = BitConverter.ToUInt16(byteBuffer, i);
			}
			return buffer;
		}
	}
}
