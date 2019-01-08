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
		/// Defines if this layer is a unlit layer that is always on
		/// </summary>
		public bool IsUnlit { get; set; }

		/// <summary>
		/// The color to render the unlit layer
		/// </summary>
		public SKColor UnlitColor { get; set; }

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
		public double Size { get; set; } = 1;

		/// <summary>
		/// Defines with which opacity the layer is drawn.
		/// </summary>
		public double Opacity { get; set; } = 1;

		/// <summary>
		/// Defines how much luminosity is added to the color. Can be negative, -1 = black, 1 = white
		/// </summary>
		public float Luminosity { get; set; }

		/// <summary>
		/// How much the dots are rounded. 0 = square, 1 = circle.
		/// </summary>
		public double Rounded { get; set; } = 1;

		/// <summary>
		/// Defines how much blurring is applied to this layer
		/// </summary>
		public double Blur { get; set; }

		/// <summary>
		/// Returns an identical copy of this layer style.
		/// </summary>
		/// <returns></returns>
		public DmdLayerStyleDefinition Copy()
		{
			return new DmdLayerStyleDefinition {
				IsEnabled = IsEnabled,
				IsUnlit = IsUnlit,
				UnlitColor = new SKColor(UnlitColor.Red, UnlitColor.Green, UnlitColor.Blue, UnlitColor.Alpha),
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
			if (!(obj is DmdLayerStyleDefinition other)) {
				return false;
			}
			return IsEnabled == other.IsEnabled
				   && IsUnlit == other.IsUnlit
				   && UnlitColor == other.UnlitColor
			       && IsBlurEnabled == other.IsBlurEnabled
			       && IsRoundedEnabled == other.IsRoundedEnabled
			       && Size.Equals(other.Size)
			       && Opacity.Equals(other.Opacity)
			       && Luminosity.Equals(other.Luminosity)
			       && Rounded.Equals(other.Rounded)
			       && Blur.Equals(other.Blur);
		}

		protected bool Equals(DmdLayerStyleDefinition other)
		{
			return IsEnabled == other.IsEnabled
			       && IsUnlit == other.IsUnlit
			       && UnlitColor == other.UnlitColor
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
				hashCode = (hashCode * 397) ^ obj.IsUnlit.GetHashCode();
				hashCode = (hashCode * 397) ^ obj.UnlitColor.GetHashCode();
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

		public override int GetHashCode()
		{
			return GetHashCode(this);
		}
	}
}