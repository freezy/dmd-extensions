namespace LibDmd.Frame
{
	/// <summary>
	/// A set of dimensions, in pixel.
	/// </summary>
	public readonly struct Dimensions
	{
		public int Width { get; }
		public int Height { get; }

		public int Surface => Width * Height;

		public double AspectRatio => (double)Width / Height;

		public bool IsFlat => Width == 0 || Height == 0;

		public Dimensions(int width, int height) {
			Width = width;
			Height = height;
		}

		public static readonly Dimensions Dynamic = new Dimensions(-1, -1);

		public static bool operator < (Dimensions x, Dimensions y) => x.Surface < y.Surface;
		public static bool operator > (Dimensions x, Dimensions y) => x.Surface > y.Surface;
		public static bool operator == (Dimensions x, Dimensions y) => x.Width == y.Width && x.Height == y.Height;
		public static bool operator != (Dimensions x, Dimensions y) => !(x == y);
		public static Dimensions operator * (int x, Dimensions dim) => new Dimensions(dim.Width * x, dim.Height * x);
		public static Dimensions operator * (Dimensions dim, int x) => new Dimensions(dim.Width * x, dim.Height * x);
		public static Dimensions operator / (Dimensions dim, int x) => new Dimensions(dim.Width / x, dim.Height / x);

		public override string ToString() => $"{Width}x{Height}";

		public bool Equals(Dimensions other) => Width == other.Width && Height == other.Height;
		
		public bool IsDoubleSizeOf(Dimensions dimensions) => Width == dimensions.Width * 2 && Height == dimensions.Height * 2;
		
		public bool FitsInto(Dimensions dimensions) => Width <= dimensions.Width && Height <= dimensions.Height;

		public override bool Equals(object obj) => obj is Dimensions other && Equals(other);
		
		public bool Equals(int width, int height) => width == Width && height == Height;

		public override int GetHashCode()
		{
			unchecked {
				return (Width * 397) ^ Height;
			}
		}
	}
}
