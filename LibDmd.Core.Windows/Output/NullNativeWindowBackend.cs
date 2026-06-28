using LibDmd.Output.Virtual.Dmd;

namespace LibDmd.Output.NativeWindow
{
	internal sealed class NullNativeWindowBackend : INativeWindowBackend
	{
		public NullNativeWindowBackend(int width, int height, int windowLeft, int windowTop, int windowWidth, int windowHeight, bool stayOnTop)
		{
			WindowLeft = windowLeft;
			WindowTop = windowTop;
			WindowWidth = windowWidth > 0 ? windowWidth : width * 4;
			WindowHeight = windowHeight > 0 ? windowHeight : height * 4;
			WindowStayOnTop = stayOnTop;
		}

		public bool IsAvailable => false;
		public int WindowLeft { get; private set; }
		public int WindowTop { get; private set; }
		public int WindowWidth { get; private set; }
		public int WindowHeight { get; private set; }
		public bool WindowStayOnTop { get; private set; }
		public bool IsMovingOrSizing => false;

		public void ConfigureWindow(int left, int top, int width, int height, bool stayOnTop)
		{
			WindowLeft = left;
			WindowTop = top;
			WindowWidth = width;
			WindowHeight = height;
			WindowStayOnTop = stayOnTop;
		}

		public void ConfigureRenderStyle(VirtualDmdRenderStyle renderStyle)
		{
		}

		public void Render(byte[] rgba)
		{
		}

		public void Dispose()
		{
		}
	}
}
