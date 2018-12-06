using System;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{

	public class RasterizeLayerStyleDefinition
	{
		/// <summary>
		/// Defines if this layer is active
		/// </summary>
		public bool IsEnabled { get; set; }

		/// <summary>
		/// Defines if blurring of this layer is active
		/// </summary>
		public bool IsBlurEnabled { get; set; }

		/// <summary>
		/// Defines if dilation of this layer is active
		/// </summary>
		public bool IsDilateEnabled { get; set; }

		/// <summary>
		/// The color in which the segment is painted
		/// </summary>
		public SKColor Color { get; set; }

		/// <summary>
		/// Defines how much blurring is applied to this layer
		/// </summary>
		public SKPoint Blur { get; set; }

		/// <summary>
		/// Defines how much dilation is applied to this layer
		/// </summary>
		public SKPoint Dilate { get; set; }

		/// <summary>
		/// Returns a copy of this layer style with all parameters scaled by a given
		/// factor.
		/// </summary>
		/// <param name="scaleFactor">Scale factor</param>
		/// <returns></returns>
		public RasterizeLayerStyle Scale(float scaleFactor)
		{
			return new RasterizeLayerStyle {
				Blur = new SKPoint(scaleFactor * Blur.X, scaleFactor * Blur.Y),
				Dilate = new SKPoint((float)Math.Round(scaleFactor * Dilate.X), (float)Math.Round(scaleFactor * Dilate.Y))
			};
		}

		/// <summary>
		/// Returns an identical copy of this layer style.
		/// </summary>
		/// <returns></returns>
		public RasterizeLayerStyleDefinition Copy()
		{
			return new RasterizeLayerStyleDefinition {
				IsEnabled = IsEnabled,
				IsBlurEnabled = IsBlurEnabled,
				IsDilateEnabled = IsDilateEnabled,
				Color = new SKColor(Color.Red, Color.Green, Color.Blue, Color.Alpha),
				Blur = new SKPoint(Blur.X, Blur.Y),
				Dilate = new SKPoint(Dilate.X, Dilate.Y)
			};
		}

		public override bool Equals(object obj)
		{
			if (!(obj is RasterizeLayerStyleDefinition item)) {
				return false;
			}

			return IsEnabled == item.IsEnabled
				   && IsBlurEnabled == item.IsBlurEnabled
				   && IsDilateEnabled == item.IsDilateEnabled
				   && Color.Equals(item.Color)
				   && Blur.Equals(item.Blur)
				   && Dilate.Equals(item.Dilate);
		}

		protected bool Equals(RasterizeLayerStyleDefinition other)
		{
			return IsEnabled == other.IsEnabled
				   && IsBlurEnabled == other.IsBlurEnabled
				   && IsDilateEnabled == other.IsDilateEnabled
				   && Color.Equals(other.Color)
				   && Blur.Equals(other.Blur)
				   && Dilate.Equals(other.Dilate);
		}

		public override int GetHashCode()
		{
			unchecked {
				var hashCode = IsEnabled.GetHashCode();
				hashCode = (hashCode * 397) ^ IsBlurEnabled.GetHashCode();
				hashCode = (hashCode * 397) ^ IsDilateEnabled.GetHashCode();
				hashCode = (hashCode * 397) ^ Color.GetHashCode();
				hashCode = (hashCode * 397) ^ Blur.GetHashCode();
				hashCode = (hashCode * 397) ^ Dilate.GetHashCode();
				return hashCode;
			}
		}

		public override string ToString()
		{
			return $"RasterizeLayerStyleDefinition[enabled:{IsEnabled},color:{Color.ToString()},blur:{IsBlurEnabled}/{Blur.X}x{Blur.Y},dilate:{IsDilateEnabled}/{Dilate.X}x{Dilate.Y}";
		}
	}

	/// <summary>
	/// Describes an individual layer of a given style.
	/// </summary>
	///
	/// <summary>
	/// The logic is:
	///    - first, the segments gets drawn in the defined color
	///    - then it gets dilated
	///    - lastly it gets blurred.
	/// </summary>
	public class RasterizeLayerStyle
	{
		/// <summary>
		/// Defines how much blurring is applied to this layer
		/// </summary>
		public SKPoint Blur { get; set; }

		/// <summary>
		/// Defines how much dilation is applied to this layer
		/// </summary>
		public SKPoint Dilate { get; set; }

		public override string ToString()
		{
			return $"RasterizeLayerStyle[blur:{Blur.X}x{Blur.Y},dilate:{Dilate.X}x{Dilate.Y}";
		}
	}
}