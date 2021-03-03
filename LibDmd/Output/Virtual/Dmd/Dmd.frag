

in vec2 dmdUv;
in vec2 glassUv;

uniform sampler2D dmdTexture; // Base DMD
uniform sampler2D dmdDotGlow; // DMD Small blur
uniform sampler2D dmdBackGlow; // DMD Large blur
uniform sampler2D glassTexture; // Glass texture

uniform vec2 dmdSize; // DMD size
uniform vec3 unlitDot; // Color of unlit dots
uniform vec4 glassColor; // Glass color: RGB components are used to tint the glass, the A component is the amount of light from the DMD that the glass reflects

out vec4 FragColor;

// from http://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
float udRoundBox(vec2 p, float b, float r)
{
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; // SDF with negative values inside the dot
    // return length(max(q, 0.0)) - r; // SDF with 0 distance inside the dot
}

// Compute the dot color contribution from dot placed at 'ofs' to the current fragment
vec3 computeDotColor(vec2 ofs)
{
	// Nearest filtering for base dots
	vec2 nearest = (floor(dmdUv * dmdSize) + ofs) / dmdSize;
	// Sampling position as (0,0) at dot center, (-1,-1) to (1,1) in dot corners
	vec2 pos = 2.0 * (fract(dmdUv * dmdSize) - ofs);
	// Dots from the lamp
	float dot = smoothstep(sharpMax, sharpMin, udRoundBox(pos, dotSize, dotRounding * dotSize));
	vec3 dmd = texture(dmdTexture, nearest).rgb;
#ifdef UNLIT
	// Add a little shadow for unlit dots which are lightly visible on real DMDs
	dmd += unlitDot;
#endif
	return dmd * dot;
}

void main()
{
	vec3 dotColor;

#ifdef DOT_OVERLAP
	// Base dot color, taken in account the 9 nearest surrounding dots
	dotColor = vec3(0.0);
	for(int x = -1; x <= 1; x++) {
		for(int y = -1; y <= 1; y++) {
			dotColor = max(dotColor, computeDotColor(vec2(float(x) + 0.5, float(y) + 0.5)));
		}
	}
#else
	// Base dot color, taken in account only the nearest dot
	dotColor = computeDotColor(vec2(0.5, 0.5));
#endif

#ifdef DOTGLOW
	// glow from nearby lamps (taken from light DMD blur)
	dotColor += texture(dmdDotGlow, dmdUv).rgb * dotGlow;
#endif

#ifdef BACKGLOW
	// add background diffuse light (very blurry from large DMD blur)
	dotColor += texture(dmdBackGlow, dmdUv).rgb * backGlow;
#endif

#ifdef BRIGHTNESS
	// Apply the overall brightness
	dotColor *= brightness;
#endif

#ifdef GLASS
	// Apply the glass as a tinted (lighten by the DMD using large blur, and the base color) additive blend.
	vec4 glass = texture(glassTexture, glassUv);
	vec3 glassLight = glassColor.rgb + 2.5 * glassColor.a * texture(dmdBackGlow, dmdUv).rgb * brightness;
	dotColor += glass.rgb * glassLight.rgb;
#endif

#ifdef GAMMA
	// Gamma correction 
	dotColor = pow(dotColor, vec3(1.0 / gamma));
#endif

    FragColor = vec4(dotColor, 1.0);
}