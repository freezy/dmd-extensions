using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using LibDmd;
using LibDmd.Common;
using LibDmd.Converter.Serum;
using LibDmd.Frame;
using LibDmd.Input.Passthrough;
using LibDmd.Native;
using LibDmd.Output;
using LibDmd.Output.NativeWindow;
using LibDmd.Output.ZeDMD;

namespace LibDmd.Core.Harness
{
	internal static class Program
	{
		private static int Main(string[] args)
		{
			Console.WriteLine("=== LibDmd.Core cross-platform harness (Phase 0) ===");
			Console.WriteLine($"  OS:      {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
			Console.WriteLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
			Console.WriteLine();

			// Install the cross-platform native-library resolver (maps serum64.dll/zedmd64.dll
			// to the per-OS file). It also runs via a module initializer; calling it is harmless.
			NativeLibraryLoader.Register();

			if (Array.IndexOf(args, "--native-window") >= 0) {
				return TestNativeWindow() ? 0 : 1;
			}

			TestSerumNative();
			Console.WriteLine();
			TestPipeline();
			Console.WriteLine();
			TestZeDmd();
			Console.WriteLine();

			if (args.Length >= 2) {
				TestSerumColorization(args[0], args[1]);
			} else {
				Console.WriteLine("(tip: pass <altcolorDir> <romName> to exercise Serum colorization from a .cRZ)");
			}

			Console.WriteLine();
			Console.WriteLine("=== Done ===");
			return 0;
		}

		/// <summary>Proves the native libserum loads and is callable across the P/Invoke boundary.</summary>
		private static void TestSerumNative()
		{
			Console.WriteLine("[1] Native interop - libserum:");
			try {
				var version = Serum.GetVersion();
				Console.WriteLine($"    OK: libserum reports version \"{version}\" - native load + P/Invoke works.");
			} catch (DllNotFoundException e) {
				Console.WriteLine($"    libserum not found next to the executable: {e.Message}");
			} catch (Exception e) {
				Console.WriteLine($"    libserum call failed: {e.GetType().Name}: {e.Message}");
			}
		}

		/// <summary>Proves the Rx frame pipeline runs WPF-free: source -> RenderGraph -> destination.</summary>
		private static void TestPipeline()
		{
			Console.WriteLine("[2] Frame pipeline - PassthroughGray2Source -> RenderGraph -> LoggingDestination:");
			var lastFormat = new BehaviorSubject<FrameFormat>(FrameFormat.Gray2);
			var source = new PassthroughGray2Source(lastFormat, "Harness Gray2 Source");
			var dest = new LoggingDestination();

			// runOnMainThread: true keeps frame processing synchronous on this thread, so the
			// pushed frames are rendered before we tear the graph down.
			var graph = new RenderGraph(new UndisposedReferences(), runOnMainThread: true) {
				Name = "Harness",
				Source = source,
				Destinations = new List<IDestination> { dest },
			};
			graph.Init();

			using (graph.StartRendering(ex => Console.WriteLine($"    pipeline error: {ex.Message}"))) {
				for (var i = 0; i < 3; i++) {
					var data = new byte[Dimensions.Standard.Surface];
					for (var p = 0; p < data.Length; p++) {
						data[p] = (byte)((p + i) & 0x3);
					}
					source.NextFrame(new DmdFrame(Dimensions.Standard, data, 2));
				}
			}

			Console.WriteLine($"    {dest.FrameCount} frame(s) made it through the WPF-free pipeline.");
		}

		/// <summary>Probes for a real ZeDMD (exercises the libzedmd load path; no hardware expected).</summary>
		private static void TestZeDmd()
		{
			Console.WriteLine("[3] Real-hardware probe - ZeDMD (libzedmd):");
			try {
				var zedmd = ZeDMD.GetInstance(false, 100, null);
				Console.WriteLine($"    ZeDMD instantiated. IsAvailable={zedmd.IsAvailable} (false = no device attached, expected).");
			} catch (Exception e) {
				Console.WriteLine($"    ZeDMD probe failed: {e.GetType().Name}: {e.Message}");
			}
		}

		/// <summary>Optionally loads a real Serum colorization (.cRZ) to prove the converter end to end.</summary>
		private static void TestSerumColorization(string altColorDir, string romName)
		{
			Console.WriteLine($"[4] Serum colorization - rom \"{romName}\" in \"{altColorDir}\":");
			try {
				using (var serum = new Serum(altColorDir, romName, ScalerMode.None)) {
					Console.WriteLine($"    Serum.IsLoaded={serum.IsLoaded}, colorization version={serum.ColorizationVersion}");
				}
			} catch (Exception e) {
				Console.WriteLine($"    Serum load failed: {e.GetType().Name}: {e.Message}");
			}
		}

		private static bool TestNativeWindow()
		{
			// Cross-platform: the NativeDmdWindow factory resolves the Win32 window on Windows and the
			// host-pumped SDL/GL-ES (ANGLE) window on macOS/Linux. On macOS this MUST run on the main
			// thread (the process main thread, which is where Main -> here executes), and Pump() must be
			// called once per frame -- exactly what the loop below does.
			Console.WriteLine("[native-window] RenderGraph -> NativeDmdWindow (Win32 / SDL+GL-ES via ANGLE):");
			try {
				var w = Dimensions.Standard.Width;
				var h = Dimensions.Standard.Height;
				var layout = new DmdWindowLayout(100, 100, w * 8, h * 8, false);
				// A realistic DMD look so the shader (dots + glow) is visibly exercised.
				var style = new DmdWindowStyle {
					DotSize = 0.85f, DotRounding = 1.0f, DotSharpness = 0.8f,
					Brightness = 1.0f, DotGlow = 1.0f, BackGlow = 0.4f, Gamma = 1.0f,
				};
				using (var window = NativeDmdWindow.TryCreate(w, h, layout, style)) {
					if (window == null || !window.IsAvailable) {
						Console.WriteLine("    Native window unavailable (no platform backend, or the SDL2 / ANGLE runtime is missing next to the executable).");
						return false;
					}

					var lastFormat = new BehaviorSubject<FrameFormat>(FrameFormat.Rgb24);
					var source = new PassthroughRgb24Source(lastFormat, "Harness RGB24 Source", deDupe: false);
					var graph = new RenderGraph(new UndisposedReferences(), runOnMainThread: true) {
						Name = "Native Window Harness",
						Source = source,
						Destinations = new List<IDestination> { window },
					};
					graph.Init();

					Console.WriteLine($"    Window up: {window.Name} (RequiresHostPump={window.RequiresHostPump}). Animating ~10s; try dragging it to reposition.");
					using (graph.StartRendering(ex => Console.WriteLine($"    pipeline error: {ex.Message}"))) {
						for (var frame = 0; frame < 600; frame++) {
							source.NextFrame(CreateRgb24TestFrame(frame));
							if (window.RequiresHostPump) {
								window.Pump(); // SDL/macOS: render + event pump, must be on the main thread
							}
							System.Threading.Thread.Sleep(16);
						}
					}
				}

				Console.WriteLine("    Native window test completed.");
				return true;
			} catch (Exception e) {
				Console.WriteLine($"    Native window test failed: {e}");
				return false;
			}
		}

		private static DmdFrame CreateRgb24TestFrame(int frame)
		{
			var dim = Dimensions.Standard;
			var data = new byte[dim.Surface * 3];
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width; x++) {
					var offset = (y * dim.Width + x) * 3;
					var bar = ((x + frame) / 8) % 3;
					var on = ((x + frame) % 32) < 16 ^ ((y / 4) % 2 == 0);
					data[offset] = (byte)(on && bar == 0 ? 255 : 24);
					data[offset + 1] = (byte)(on && bar == 1 ? 180 : 8);
					data[offset + 2] = (byte)(on && bar == 2 ? 64 : 0);
				}
			}

			return new DmdFrame(dim, data, 24);
		}
	}
}
