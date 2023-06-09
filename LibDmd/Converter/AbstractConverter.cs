using System.Collections.Generic;
using LibDmd.Frame;

namespace LibDmd.Converter
{
	public abstract class AbstractConverter : IConverter
	{
		public abstract IEnumerable<FrameFormat> From { get; }
		public abstract bool IsConnected { get; }
		public abstract void Convert(DmdFrame frame);
		public abstract void Convert(AlphaNumericFrame frame);
		public abstract void Init();
	}
}
