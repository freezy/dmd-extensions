using System;
using System.Collections.Generic;
using System.Reflection;
using NLog;

namespace LibDmd.Output.NativeWindow
{
	/// <summary>
	/// A free-floating, in-process OS window that renders the DMD with the
	/// dmdext virtual-DMD look.
	/// </summary>
	/// <remarks>
	/// This is the cross-platform contract the VPE bridge talks to. The concrete
	/// implementation lives in a platform assembly (e.g. <c>LibDmd.Core.Windows</c>)
	/// and is resolved at runtime through <see cref="NativeDmdWindow.TryCreate"/>,
	/// so a consumer can ship the same managed bridge on platforms where the
	/// platform window assembly isn't present (the call just returns null there).
	///
	/// Implementations come in two flavours:
	/// <list type="bullet">
	/// <item>self-driven — they own a background thread and event loop. <see cref="Pump"/> is a no-op.</item>
	/// <item>host-driven — they require <see cref="Pump"/> to be called once per frame from
	/// the host's UI/main thread (mandatory on macOS, where AppKit is main-thread-only).</item>
	/// </list>
	/// All frame data is pushed in via the <see cref="IDestination"/> render methods; the
	/// window owns its own GL(ES) context and never shares Unity's GPU device.
	///
	/// THREADING CONTRACT — implementations MUST be safe against this concurrency:
	/// the <see cref="IDestination"/> render methods (RenderGrayX/RenderRgbX) and the color/palette
	/// setters are invoked by the RenderGraph on the DMD <i>worker</i> thread (and, when a clocked
	/// colorizer is active, from its rotation/timer thread) — NOT necessarily the host's main thread.
	/// <see cref="ConfigureWindow"/>, <see cref="ConfigureStyle"/> and <see cref="Pump"/> are called
	/// from the host's main thread. Implementations therefore buffer incoming frames under a lock and
	/// only touch GL/OS-window state from their own loop (self-driven) or from <see cref="Pump"/>
	/// (host-driven). Render methods must never block on the main thread.
	/// </remarks>
	public interface INativeDmdWindow : IDestination
	{
		int WindowLeft { get; }
		int WindowTop { get; }
		int WindowWidth { get; }
		int WindowHeight { get; }
		bool WindowStayOnTop { get; }

		/// <summary>True while the user is interactively moving/resizing the window.</summary>
		bool IsMovingOrSizing { get; }

		/// <summary>
		/// True if this window must be driven from the host's main thread via <see cref="Pump"/>.
		/// False for self-driven backends that run their own loop.
		/// </summary>
		bool RequiresHostPump { get; }

		/// <summary>Repositions/resizes the window. Safe to call from any thread.</summary>
		void ConfigureWindow(DmdWindowLayout layout);

		/// <summary>Updates the DMD render style. Safe to call from any thread.</summary>
		void ConfigureStyle(DmdWindowStyle style);

		/// <summary>
		/// Pumps the window's event loop and renders the latest frame. Must be called
		/// once per host frame on the host's main thread for host-driven backends; a
		/// no-op for self-driven ones.
		/// </summary>
		void Pump();
	}

	/// <summary>Window placement, in screen coordinates.</summary>
	public sealed class DmdWindowLayout
	{
		public int Left;
		public int Top;
		public int Width;
		public int Height;
		public bool StayOnTop;

		public DmdWindowLayout() { }

		public DmdWindowLayout(int left, int top, int width, int height, bool stayOnTop)
		{
			Left = left;
			Top = top;
			Width = width;
			Height = height;
			StayOnTop = stayOnTop;
		}
	}

	/// <summary>
	/// Neutral, platform-independent mirror of the virtual-DMD render style. Kept here
	/// (rather than reusing the WPF/GL-coupled style type) so the cross-platform bridge
	/// can build it without referencing the platform window assembly.
	/// </summary>
	public sealed class DmdWindowStyle
	{
		public float DotSize = 0.92f;
		public float DotRounding = 1.0f;
		public float DotSharpness = 0.8f;
		public float UnlitDotR;
		public float UnlitDotG;
		public float UnlitDotB;
		public float Brightness = 0.95f;
		public float DotGlow;
		public float BackGlow;
		public float Gamma = 1.0f;
		public float GlassR;
		public float GlassG;
		public float GlassB;
		public float GlassLighting;
	}

	/// <summary>
	/// Factory that resolves the platform-specific native-window implementation without a
	/// hard assembly reference, so the managed bridge stays cross-platform. This is the
	/// single reflection point; everything afterwards is strongly typed via
	/// <see cref="INativeDmdWindow"/>.
	/// </summary>
	public static class NativeDmdWindow
	{
		// Self-driven Win32 backend (own message loop) and the cross-platform host-pumped SDL/GL-ES
		// backend. The factory picks per-OS, preferring the native one and falling back to SDL.
		private const string Win32TypeName = "LibDmd.Output.NativeWindow.NativeWindowDestination, LibDmd.Core.Windows";
		private const string SdlTypeName = "LibDmd.Output.NativeWindow.SdlNativeDmdWindow, LibDmd.Core.Sdl";

		private static bool _resolved;
		private static ConstructorInfo[] _ctors;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Creates a native DMD window for the given resolution, or returns null if no platform backend
		/// is available (the assembly isn't shipped for this OS) or every candidate backend failed to
		/// initialize. Candidates are attempted in preference order (native first, then SDL); a backend
		/// that throws during construction is logged and the next candidate is tried.
		/// </summary>
		public static INativeDmdWindow TryCreate(int width, int height, DmdWindowLayout layout, DmdWindowStyle style)
		{
			layout = layout ?? new DmdWindowLayout(100, 100, width * 4, height * 4, false);
			style = style ?? new DmdWindowStyle();

			foreach (var ctor in ResolveConstructors()) {
				try {
					return (INativeDmdWindow)ctor.Invoke(new object[] { width, height, layout, style });
				} catch (Exception exception) {
					// Backend present but couldn't create its window/context (e.g. no SDL/ANGLE runtime).
					// Fall through to the next candidate rather than giving up on the whole feature.
					Logger.Info(exception.InnerException ?? exception,
						$"[DMD] Native-window backend {ctor.DeclaringType?.FullName} failed to initialize; trying next candidate.");
				}
			}

			return null;
		}

		private static ConstructorInfo[] ResolveConstructors()
		{
			if (_resolved) {
				return _ctors;
			}

			_resolved = true;
			var ctors = new List<ConstructorInfo>();
			foreach (var typeName in CandidateTypeNames()) {
				var type = Type.GetType(typeName, throwOnError: false);
				if (type == null || !typeof(INativeDmdWindow).IsAssignableFrom(type)) {
					continue;
				}

				var ctor = type.GetConstructor(new[] {
					typeof(int), typeof(int), typeof(DmdWindowLayout), typeof(DmdWindowStyle)
				});
				if (ctor != null) {
					ctors.Add(ctor);
				}
			}

			_ctors = ctors.ToArray();
			return _ctors;
		}

		private static string[] CandidateTypeNames()
		{
			return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
				? new[] { Win32TypeName, SdlTypeName }
				: new[] { SdlTypeName };
		}
	}
}
