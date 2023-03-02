using System;

namespace LibDmd.Converter.Serum
{
	public class CRC32encode
	{
		private bool crc32_ready = false;
		private uint[] crc32_table=new uint[256];

		public CRC32encode() // initiating the CRC table, must be called at startup
		{
			if (crc32_ready) return;
			for (uint i = 0; i < 256; i++)
			{
				uint ch = i;
				uint crc = 0;
				for (uint j = 0; j < 8; j++)
				{
					uint b = (ch ^ crc) & 1;
					crc >>= 1;
					if (b!=0) crc = crc ^ 0xEDB88320;
					ch >>= 1;
				}
				crc32_table[i] = crc;
			}
			crc32_ready = true;
		}

		public uint crc32_fast(byte[] s, uint n, byte ShapeMode) // computing a buffer CRC32, "init_crc32()" must have been called before the first use
		{

			uint crc = 0xFFFFFFFF;
			for (uint i = 0; i<n; i++)
			{
				byte val = s[i];
				if ((ShapeMode == 1) && (val > 1))  val = 1;
				crc = (crc >> 8) ^ crc32_table[(val ^ crc) & 0xFF];
			}
			return ~crc;
		}

		public uint crc32_fast_mask(byte[] source, byte[] mask, uint n, byte ShapeMode) // computing a buffer CRC32 on the non-masked area, "init_crc32()" must have been called before the first use
																								   // take into account if we are in shape mode
		{
			uint crc = 0xFFFFFFFF;
			for (uint i = 0; i < n; i++)
			{
				if (mask[i] == 0)
				{
					byte val = source[i];
					if ((ShapeMode == 1) && (val > 1)) val = 1;
					crc = (crc >> 8) ^ crc32_table[(val ^ crc) & 0xFF];
				}
			}
			return ~crc;
		}

	}
}
