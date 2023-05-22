using System.Text;
using LibDmd.Frame;

namespace LibDmd
{
	public class DMDFrame
	{
		public Dimensions Dimensions;
		public byte[] Data;
		public int BitLength;

		public DMDFrame Update(byte[] data, int bitSize)
		{
			Data = data;
			BitLength = bitSize;
			return this;
		}

		public DMDFrame Update(Dimensions dim, byte[] data, int bitSize)
		{
			Dimensions = dim;
			Data = data;
			BitLength = bitSize;
			return this;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"DMDFrame {Dimensions}@{BitLength} ({Data.Length} bytes):");
			for (var y = 0; y < Dimensions.Height; y++) {
				for (var x = 0; x < Dimensions.Width; x++) {
					sb.Append(Data[y * Dimensions.Width + x].ToString("X"));
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}
	}

	public class RawDMDFrame : DMDFrame
	{
		public byte[][] RawPlanes;

		public RawDMDFrame Update(Dimensions dim, byte[] data, byte[][] rawPlanes)
		{
			this.Update(dim, data, rawPlanes.Length);
			this.RawPlanes = rawPlanes;
			return this;
		}
	}
}
