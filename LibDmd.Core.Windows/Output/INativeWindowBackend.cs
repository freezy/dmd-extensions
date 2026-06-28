using System;
using LibDmd.Output.Virtual.Dmd;

namespace LibDmd.Output.NativeWindow
{
	internal interface INativeWindowBackend : IDisposable
	{
		bool IsAvailable { get; }
		int WindowLeft { get; }
		int WindowTop { get; }
		int WindowWidth { get; }
		int WindowHeight { get; }
		bool WindowStayOnTop { get; }
		bool IsMovingOrSizing { get; }
		void ConfigureWindow(int left, int top, int width, int height, bool stayOnTop);
		void ConfigureRenderStyle(VirtualDmdRenderStyle renderStyle);
		void Render(byte[] rgba);
	}
}
