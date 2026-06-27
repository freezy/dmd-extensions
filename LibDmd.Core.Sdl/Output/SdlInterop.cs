using System;
using System.Runtime.InteropServices;

namespace LibDmd.Output.NativeWindow
{
	/// <summary>
	/// Minimal SDL2 P/Invoke surface needed for a borderless, repositionable, GL-ES DMD window.
	/// </summary>
	/// <remarks>
	/// The native library is referenced as "SDL2"; .NET maps that to SDL2.dll / libSDL2-2.0.so.0 /
	/// libSDL2.dylib via the default resolver (the consuming app must ship the SDL2 runtime, and on
	/// macOS/Windows an ANGLE runtime — libEGL/libGLESv2 — for the GL-ES context).
	/// </remarks>
	internal static class Sdl
	{
		private const string Lib = "SDL2";

		public const uint SDL_INIT_VIDEO = 0x00000020;

		// SDL_WindowFlags
		public const uint SDL_WINDOW_OPENGL = 0x00000002;
		public const uint SDL_WINDOW_SHOWN = 0x00000004;
		public const uint SDL_WINDOW_BORDERLESS = 0x00000010;
		public const uint SDL_WINDOW_RESIZABLE = 0x00000020;
		public const uint SDL_WINDOW_ALLOW_HIGHDPI = 0x00002000;
		public const uint SDL_WINDOW_ALWAYS_ON_TOP = 0x00008000;

		public const int SDL_WINDOWPOS_UNDEFINED = 0x1FFF0000;

		// SDL_GLattr
		public const int SDL_GL_RED_SIZE = 0;
		public const int SDL_GL_GREEN_SIZE = 1;
		public const int SDL_GL_BLUE_SIZE = 2;
		public const int SDL_GL_ALPHA_SIZE = 3;
		public const int SDL_GL_DOUBLEBUFFER = 5;
		public const int SDL_GL_DEPTH_SIZE = 6;
		public const int SDL_GL_CONTEXT_MAJOR_VERSION = 17;
		public const int SDL_GL_CONTEXT_MINOR_VERSION = 18;
		public const int SDL_GL_CONTEXT_PROFILE_MASK = 21;

		// SDL_GLprofile
		public const int SDL_GL_CONTEXT_PROFILE_ES = 0x0004;

		// Event types
		public const uint SDL_QUIT = 0x100;
		public const uint SDL_WINDOWEVENT = 0x200;
		public const uint SDL_MOUSEMOTION = 0x400;
		public const uint SDL_MOUSEBUTTONDOWN = 0x401;
		public const uint SDL_MOUSEBUTTONUP = 0x402;

		public const byte SDL_BUTTON_LEFT = 1;

		// SDL_bool / true
		public const int SDL_TRUE = 1;
		public const int SDL_FALSE = 0;

		// Hints
		public const string SDL_HINT_OPENGL_ES_DRIVER = "SDL_OPENGL_ES_DRIVER";
		public const string SDL_HINT_VIDEO_HIGHDPI_DISABLED = "SDL_VIDEO_HIGHDPI_DISABLED";

		// Event field offsets. SDL_MouseMotion/Button events share: windowID@8, button@16, x@20, y@24.
		public const int EventOffsetWindowId = 8;
		public const int EventOffsetButton = 16;
		public const int EventOffsetX = 20;
		public const int EventOffsetY = 24;
		public const int EventSize = 64; // SDL_Event union is 56 bytes; 64 is a safe over-allocation.

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int SDL_Init(uint flags);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int SDL_InitSubSystem(uint flags);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_Quit();

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr SDL_GetError();

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern int SDL_SetHint(string name, string value);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int SDL_GL_SetAttribute(int attr, int value);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern IntPtr SDL_CreateWindow(string title, int x, int y, int w, int h, uint flags);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_DestroyWindow(IntPtr window);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr SDL_GL_CreateContext(IntPtr window);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr SDL_GL_GetCurrentContext();

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr SDL_GL_GetCurrentWindow();

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_GL_DeleteContext(IntPtr context);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int SDL_GL_MakeCurrent(IntPtr window, IntPtr context);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int SDL_GL_SetSwapInterval(int interval);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_GL_SwapWindow(IntPtr window);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern IntPtr SDL_GL_GetProcAddress(string proc);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_GL_GetDrawableSize(IntPtr window, out int w, out int h);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern int SDL_PollEvent(IntPtr e);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_PumpEvents();

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_GetWindowPosition(IntPtr window, out int x, out int y);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint SDL_GetWindowID(IntPtr window);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_SetWindowSize(IntPtr window, int w, int h);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_GetWindowSize(IntPtr window, out int w, out int h);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SDL_SetWindowAlwaysOnTop(IntPtr window, int onTop);

		[DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint SDL_GetGlobalMouseState(out int x, out int y);

		public static string GetError()
		{
			var ptr = SDL_GetError();
			return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr);
		}
	}
}
