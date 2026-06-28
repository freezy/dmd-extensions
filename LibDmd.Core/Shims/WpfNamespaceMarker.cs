// The cross-platform core keeps the legacy `using System.Windows.Media;` directives
// in its shared source files (the color types they reference are aliased to
// LibDmd.DmdColor / DmdColors / DmdColorConverter by the LibDmd.Core project's
// global usings). Declaring the namespace here — with no Color/Colors/ColorConverter
// of its own — makes those `using` directives resolve in the WPF-free build without
// touching a single shared file. It intentionally contains nothing public.
//
// ReSharper disable once CheckNamespace
namespace System.Windows.Media
{
	internal static class WpfNamespaceMarker
	{
	}
}
