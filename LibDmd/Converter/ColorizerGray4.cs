using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Converter
{
	public class ColorizerGray4 : IConverter
	{
		public RenderBitLength From { get; } = RenderBitLength.Gray4;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		public void Convert(byte[] from, byte[] to)
		{
			throw new NotImplementedException();
		}
	}
}
