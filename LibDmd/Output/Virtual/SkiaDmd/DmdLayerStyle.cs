using System;
using System.Collections.Generic;
using LibDmd.Output.Virtual.AlphaNumeric;
using SkiaSharp;

namespace LibDmd.Output.Virtual.SkiaDmd
{

	public class DmdLayerStyleDefinition
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
		/// Defines the dot is rounded
		/// </summary>
		public bool IsRoundedEnabled { get; set; }

		/// <summary>
		/// Dot size. 1 = 100%, but can be larger.
		/// </summary>
		public double Size { get; set; }

		/// <summary>
		/// Defines with which opacity the layer is drawn.
		/// </summary>
		public double Opacity { get; set; }

		/// <summary>
		/// Defines how much luminosity is added to the color. Can be negative.
		/// </summary>
		public float Luminosity { get; set; }

		/// <summary>
		/// How much the dots are rounded. 0 = square, 1 = circle.
		/// </summary>
		public double Rounded { get; set; }

		/// <summary>
		/// Defines how much blurring is applied to this layer
		/// </summary>
		public double Blur { get; set; }

		/// <summary>
		/// Returns a copy of this layer style with all parameters scaled by a given
		/// factor.
		/// </summary>
		/// <param name="scaleFactor">Scale factor</param>
		/// <returns></returns>
		public DmdLayerStyle Scale(float scaleFactor)
		{
			return new DmdLayerStyle {
				Size = scaleFactor * Size,
				Opacity = Opacity,
				Luminosity = Luminosity,
				Rounded = Rounded,
				Blur = scaleFactor * Blur
			};
		}

		/// <summary>
		/// Returns an identical copy of this layer style.
		/// </summary>
		/// <returns></returns>
		public DmdLayerStyleDefinition Copy()
		{
			return new DmdLayerStyleDefinition {
				IsEnabled = IsEnabled,
				IsBlurEnabled = IsBlurEnabled,
				IsRoundedEnabled = IsRoundedEnabled,
				Size = Size,
				Opacity = Opacity,
				Luminosity = Luminosity,
				Rounded = Rounded,
				Blur = Blur
			};
		}

		public override bool Equals(object obj)
		{
			if (!(obj is DmdLayerStyleDefinition item)) {
				return false;
			}
			return IsEnabled == item.IsEnabled
			       && IsBlurEnabled == item.IsBlurEnabled
			       && IsRoundedEnabled == item.IsRoundedEnabled
			       && Size.Equals(item.Size)
			       && Opacity.Equals(item.Opacity)
			       && Luminosity.Equals(item.Luminosity)
			       && Rounded.Equals(item.Rounded)
			       && Blur.Equals(item.Blur);
		}

		protected bool Equals(DmdLayerStyleDefinition other)
		{
			return IsEnabled == other.IsEnabled
			       && IsBlurEnabled == other.IsBlurEnabled
			       && IsRoundedEnabled == other.IsRoundedEnabled
			       && Size.Equals(other.Size)
			       && Opacity.Equals(other.Opacity)
			       && Luminosity.Equals(other.Luminosity)
			       && Rounded.Equals(other.Rounded)
			       && Blur.Equals(other.Blur);
		}

		public int GetHashCode(DmdLayerStyleDefinition obj)
		{
			unchecked {
				var hashCode = obj.IsEnabled.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.IsBlurEnabled.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.IsRoundedEnabled.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.Size.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.Opacity.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.Luminosity.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.Rounded.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.Blur.GetHashCode();
				return hashCode;
			}
		}

		public override string ToString()
		{
			return $"DmdLayerStyleDefinition[enabled:{IsEnabled},size:{Size},opacity:{Opacity},lum:{Luminosity},rounded:{IsRoundedEnabled}/{Rounded},blur:{IsBlurEnabled}/{Blur}]";
		}
	}

	/// <summary>
	/// Describes an individual layer of a given style.
	/// </summary>
	public class DmdLayerStyle
	{
		/// <summary>
		/// Dot size. 1 = 100%, but can be larger.
		/// </summary>
		public double Size { get; set; }

		/// <summary>
		/// Defines with which opacity the layer is drawn.
		/// </summary>
		public double Opacity { get; set; }

		/// <summary>
		/// Defines how much luminosity is added to the color. Can be negative.
		/// </summary>
		public float Luminosity { get; set; }

		/// <summary>
		/// How much the dots are rounded. 0 = square, 1 = circle.
		/// </summary>
		public double Rounded { get; set; }

		/// <summary>
		/// Defines how much blurring is applied to this layer
		/// </summary>
		public double Blur { get; set; }

		public override string ToString()
		{
			return $"DmdLayerStyle[size:{Size},opacity:{Opacity},rounded:{Rounded},blur:{Blur}]";
		}
	}
}