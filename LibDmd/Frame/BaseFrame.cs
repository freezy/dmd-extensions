using System;
using LibDmd.Output;

namespace LibDmd.Frame
{
	public abstract class BaseFrame
	{
		public Dimensions Dimensions { get; protected set; }

		protected Dimensions GetTargetDimensions(IFixedSizeDestination fixedDest, IMultiSizeDestination multiDest)
		{
			if (fixedDest == null && multiDest == null) {
				return Dimensions.Dynamic;
			}

			if (fixedDest != null) {
				return fixedDest.FixedSize;
			}

			var dim = Dimensions.Dynamic;
			foreach (var multiDim in multiDest.Sizes) {
				if (Dimensions == multiDim) {
					return multiDim;
				}

				if (Dimensions < multiDim && multiDim > dim) {
					dim = multiDim;
				}
			}

			return dim == Dimensions.Dynamic ? Dimensions : dim;
		}

		protected byte[] CenterFrame(Dimensions targetDim, byte[] data, int bytesPerPixel)
		{
			var paddingX = (targetDim.Width - Dimensions.Width) / 2;
			var paddingY = (targetDim.Height - Dimensions.Height) / 2;
			var frameData = new byte[targetDim.Surface * bytesPerPixel];
			var ySrc = 0;
			for (var yDest = paddingY; yDest < paddingY + Dimensions.Height; yDest++) {
				Buffer.BlockCopy(
					data,
					ySrc * Dimensions.Width * bytesPerPixel,
					frameData,
					(yDest * targetDim.Width + paddingX) * bytesPerPixel,
					Dimensions.Width * bytesPerPixel);
				ySrc++;
			}

			return frameData;
		}

		protected byte[] CenterVertically(Dimensions targetDim, byte[] data)
		{
			var transformedFrameData = new byte[targetDim.Surface];
			Buffer.BlockCopy(
				data,
				0,
				transformedFrameData,
				((targetDim.Height - Dimensions.Height) / 2) * targetDim.Width, data.Length
			);
			return transformedFrameData;
		}

	}
}
