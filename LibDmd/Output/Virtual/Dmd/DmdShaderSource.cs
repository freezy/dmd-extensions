using System.Globalization;
using System.Text;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// Single source of truth for the virtual-DMD fragment shaders (dot rendering, the two-stage
	/// gaussian glow, and the glass overlay). Shared verbatim by both renderers so the look can't
	/// drift:
	/// <list type="bullet">
	/// <item><see cref="VirtualDmdOpenGlPipeline"/> — desktop OpenGL (GLSL 1.20), used by the WPF
	/// virtual DMD and the Win32 native window.</item>
	/// <item><c>GlesDmdPipeline</c> (LibDmd.Core.Sdl) — OpenGL ES 2.0 (GLSL ES 1.00), used by the
	/// host-pumped SDL window on macOS/Linux.</item>
	/// </list>
	/// The bodies below are valid in <b>both</b> profiles (they use the legacy
	/// <c>varying</c>/<c>texture2D</c>/<c>gl_FragColor</c> surface, which GLSL 1.20 and GLSL ES 1.00
	/// share). The only per-profile differences are the header each pipeline prepends — GLSL 1.20
	/// uses <c>#version 120</c>; GLSL ES 1.00 uses <c>#version 100</c> + a <c>precision</c> default
	/// (which 1.20 must NOT contain) — and the vertex shader (immediate-mode vs VBO), which therefore
	/// stays in each pipeline. Feature toggles and constants are emitted by
	/// <see cref="BuildDmdConfig"/>.
	/// </summary>
	internal static class DmdShaderSource
	{
		/// <summary>Blur uniforms + the two separable gaussian kernels (no header, no main).</summary>
		public const string BlurFragmentFunctions = @"
varying vec2 uv;

uniform sampler2D texture;
uniform vec2 direction;

// Kernels from https://rastergrid.com/blog/2010/09/efficient-gaussian-blur-with-linear-sampling/
vec4 blur_level_2(sampler2D image, vec2 coord, vec2 blurDirection) {
	vec4 color = vec4(0.0);
	vec2 off1 = blurDirection;
	color += texture2D(image, coord) * 0.5;
	color += texture2D(image, coord + off1) * 0.25;
	color += texture2D(image, coord - off1) * 0.25;
	return color;
}

vec4 blur_level_12(sampler2D image, vec2 coord, vec2 blurDirection) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(1.3846153846) * blurDirection;
	vec2 off2 = vec2(3.2307692308) * blurDirection;
	color += texture2D(image, coord) * 0.2270270270;
	color += texture2D(image, coord + off1) * 0.3162162162;
	color += texture2D(image, coord - off1) * 0.3162162162;
	color += texture2D(image, coord + off2) * 0.0702702703;
	color += texture2D(image, coord - off2) * 0.0702702703;
	return color;
}
";

		public const string BlurMain2 = "void main() { gl_FragColor = vec4(blur_level_2(texture, uv, direction).rgb, 1.0); }";
		public const string BlurMain12 = "void main() { gl_FragColor = vec4(blur_level_12(texture, uv, direction).rgb, 1.0); }";

		/// <summary>
		/// The DMD dot/glow/glass body (no header, no <c>#define</c>/<c>const</c> block — those come
		/// from <see cref="BuildDmdConfig"/>). Relies on a <c>varying vec2 uv;</c> supplied by each
		/// pipeline's vertex shader.
		/// </summary>
		public const string DmdFragmentBody = @"
varying vec2 uv;

uniform sampler2D dmdTexture;
uniform sampler2D dmdDotGlow;
uniform sampler2D dmdBackGlow;
uniform vec2 dmdSize;
uniform sampler2D glassTexture;
uniform vec2 glassTexOffset;
uniform vec2 glassTexScale;
uniform vec3 unlitDot;
uniform vec4 glassColor;

// from http://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
float udRoundBox(vec2 p, float b, float r)
{
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

vec3 computeDotColor(vec2 ofs)
{
	vec2 nearest = (floor(uv * dmdSize) + ofs) / dmdSize;
	vec2 pos = 2.0 * (fract(uv * dmdSize) - ofs);
	float dot = smoothstep(sharpMax, sharpMin, udRoundBox(pos, dotSize, dotRounding * dotSize));
	vec3 dmd = texture2D(dmdTexture, nearest).rgb + unlitDot;
#ifndef UNLIT
	dmd -= unlitDot;
#endif
	return dmd * dot;
}

void main()
{
#ifdef DOT_OVERLAP
	vec3 dotColor = vec3(0.0);
	for(int x = -1; x <= 1; x++) {
		for(int y = -1; y <= 1; y++) {
			dotColor = max(dotColor, computeDotColor(vec2(float(x) + 0.5, float(y) + 0.5)));
		}
	}
#else
	vec3 dotColor = computeDotColor(vec2(0.5, 0.5));
#endif

#ifdef DOTGLOW
	dotColor += texture2D(dmdDotGlow, uv).rgb * dotGlow;
#endif
#ifdef BACKGLOW
	dotColor += texture2D(dmdBackGlow, uv).rgb * backGlow;
#endif
#ifdef BRIGHTNESS
	dotColor *= brightness;
#endif
#ifdef GLASS
	// Glass as a tinted additive blend, lightened by the DMD's large blur and the base color.
	vec2 glassUv = uv * glassTexScale - glassTexOffset;
	vec4 glass = texture2D(glassTexture, glassUv);
	vec3 glassLight = glassColor.rgb + 2.5 * glassColor.a * texture2D(dmdBackGlow, uv).rgb * brightness;
	dotColor += glass.rgb * glassLight.rgb;
#endif
#ifdef GAMMA
	dotColor = pow(dotColor, vec3(1.0 / gamma));
#endif
	gl_FragColor = vec4(dotColor, 1.0);
}";

		/// <summary>
		/// Emits the feature <c>#define</c>s and tuning <c>const</c>s prepended to
		/// <see cref="DmdFragmentBody"/>. No <c>#version</c>/<c>precision</c> header — each pipeline
		/// prepends its own (GLSL 1.20 must not declare a precision; GLSL ES 1.00 must).
		/// </summary>
		public static string BuildDmdConfig(
			float dotSize, float dotRounding, float dotSharpness,
			float brightness, float backGlow, float dotGlow, float gamma,
			bool hasBackGlow, bool hasDotGlow, bool hasBrightness,
			bool hasUnlit, bool hasGlass, bool hasGamma)
		{
			var nfi = NumberFormatInfo.InvariantInfo;
			var b = new StringBuilder();
			if (hasBackGlow) b.AppendLine("#define BACKGLOW");
			if (hasDotGlow) b.AppendLine("#define DOTGLOW");
			if (hasBrightness) b.AppendLine("#define BRIGHTNESS");
			if (hasUnlit) b.AppendLine("#define UNLIT");
			if (hasGlass) b.AppendLine("#define GLASS");
			if (hasGamma) b.AppendLine("#define GAMMA");
			if (dotSize > 0.5f) b.AppendLine("#define DOT_OVERLAP");
			b.AppendFormat(nfi, "const float dotSize = {0:0.00000};\n", dotSize);
			b.AppendFormat(nfi, "const float dotRounding = {0:0.00000};\n", dotRounding);
			b.AppendFormat(nfi, "const float sharpMax = {0:0.00000};\n", 0.01f + dotSize * (1.0f - dotSharpness));
			b.AppendFormat(nfi, "const float sharpMin = {0:0.00000};\n", -0.01f - dotSize * (1.0f - dotSharpness));
			b.AppendFormat(nfi, "const float brightness = {0:0.00000};\n", brightness);
			b.AppendFormat(nfi, "const float backGlow = {0:0.00000};\n", backGlow);
			b.AppendFormat(nfi, "const float dotGlow = {0:0.00000};\n", dotGlow);
			b.AppendFormat(nfi, "const float gamma = {0:0.00000};\n", gamma);
			return b.ToString();
		}
	}
}
