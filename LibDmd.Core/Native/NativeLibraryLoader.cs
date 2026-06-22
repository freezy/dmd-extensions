#if NET
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibDmd.Native
{
	/// <summary>
	/// Resolves the native libraries used by the core (<c>libserum</c>, <c>libzedmd</c>)
	/// across operating systems.
	///
	/// The P/Invoke declarations in the core keep their Windows-style names
	/// (<c>serum64.dll</c>, <c>zedmd64.dll</c>). On .NET (CoreCLR) we install a
	/// <see cref="NativeLibrary.SetDllImportResolver"/> that maps those names to the
	/// per-OS file (<c>serum64.dll</c> / <c>libserum.so</c> / <c>libserum.dylib</c>, …),
	/// trying a list of candidate names so the same managed binary works on Windows,
	/// macOS and Linux.
	///
	/// This whole type is compiled only for the <c>net*</c> target framework. Under
	/// <c>netstandard2.1</c> (the Unity/Mono path) <c>NativeLibrary</c> does not exist;
	/// there, Unity resolves native plugins from its per-OS <c>Plugins/</c> folders, so
	/// no resolver is needed.
	/// </summary>
	public static class NativeLibraryLoader
	{
		private static bool _registered;
		private static readonly object Gate = new object();

		/// <summary>
		/// The logical native libraries the core imports, keyed by the base name that
		/// appears (minus the <c>64</c> suffix and extension) in the <c>[DllImport]</c>
		/// attributes.
		/// </summary>
		private static readonly string[] KnownLibraries = { "serum", "zedmd" };

		/// <summary>
		/// Installs the import resolver. Safe to call multiple times; only the first
		/// call has an effect. Automatically invoked when the assembly is loaded, but
		/// also exposed so hosts can register explicitly and fail fast.
		/// </summary>
		[ModuleInitializer]
		public static void Register()
		{
			if (_registered) {
				return;
			}
			lock (Gate) {
				if (_registered) {
					return;
				}
				NativeLibrary.SetDllImportResolver(typeof(NativeLibraryLoader).Assembly, Resolve);
				_registered = true;
			}
		}

		private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
		{
			foreach (var candidate in GetCandidates(libraryName)) {
				if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle)) {
					return handle;
				}
			}
			// returning Zero lets the runtime fall back to its default resolution
			return IntPtr.Zero;
		}

		/// <summary>
		/// Produces the ordered list of file names to try for a given import name.
		/// </summary>
		private static IEnumerable<string> GetCandidates(string libraryName)
		{
			var baseName = GetBaseName(libraryName);
			if (baseName == null) {
				// not one of ours — let the default resolver handle it
				yield return libraryName;
				yield break;
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				yield return $"{baseName}64.dll";
				yield return $"{baseName}.dll";
			} else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				yield return $"lib{baseName}.dylib";
				yield return $"{baseName}.dylib";
				yield return $"lib{baseName}64.dylib";
			} else {
				yield return $"lib{baseName}.so";
				yield return $"{baseName}.so";
				yield return $"lib{baseName}64.so";
			}

			// last resort: the literal name as written in the attribute
			yield return libraryName;
		}

		private static string GetBaseName(string libraryName)
		{
			var lower = libraryName.ToLowerInvariant();
			foreach (var known in KnownLibraries) {
				if (lower.Contains(known)) {
					return known;
				}
			}
			return null;
		}
	}
}
#endif
