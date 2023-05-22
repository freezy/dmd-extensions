namespace LibDmd.Frame
{
	/// <summary>
	/// A set of dimensions, in pixel.
	/// </summary>
	public struct Dimensions
	{
		public int Width { get; set; }
		public int Height { get; set; }

		public int Surface => Width * Height;

		public double AspectRatio => (double)Width / Height;

		public bool IsFlat => Width == 0 || Height == 0;

		public Dimensions(int width, int height) {
			Width = width;
			Height = height;
		}

		public static readonly Dimensions Dynamic = new Dimensions(-1, -1);

		public bool Is(int width, int height) => width == Width && height == Height;
			
		public static bool operator <(Dimensions x, Dimensions y) => x.Surface < y.Surface;
		public static bool operator >(Dimensions x, Dimensions y) => x.Surface > y.Surface;
		public static bool operator == (Dimensions x, Dimensions y) => x.Width == y.Width && x.Height == y.Height;
		public static bool operator != (Dimensions x, Dimensions y) => !(x == y);
		public static Dimensions operator * (int x, Dimensions dim) => new Dimensions(dim.Width * x, dim.Height * x);
		public static Dimensions operator * (Dimensions dim, int x) => new Dimensions(dim.Width * x, dim.Height * x);
		public static Dimensions operator / (Dimensions dim, int x) => new Dimensions(dim.Width / x, dim.Height / x);

		public override string ToString() => $"{Width}x{Height}";

		public bool Equals(Dimensions other) => Width == other.Width && Height == other.Height;

		public override bool Equals(object obj) => obj is Dimensions other && Equals(other);

		public override int GetHashCode()
		{
			unchecked {
				return (Width * 397) ^ Height;
			}
		}
	}
}
