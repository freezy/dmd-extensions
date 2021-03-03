

in vec2 dmdUv;
in vec2 glassUv;

uniform sampler2D dmdTexture; // Base DMD
uniform sampler2D dmdTextureBlur1; // DMD Blur level 1
uniform sampler2D dmdTextureBlur2; // DMD Blur level 2
uniform sampler2D dmdTextureBlur3; // DMD Blur level 3
uniform sampler2D glassTexture; // The glass texture (lit by the room's ambient color and by the DMD texture)

uniform vec2 dmdSize; // DMD size
uniform vec3 unlitDot; // light for unlit dots
uniform vec4 glassColor; // Glass color: RGB components are used to tint the glass, the A component is the amount of light from the DMD that the glass reflects

out vec4 FragColor;

// from http://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
float udRoundBox(vec2 p, float b, float r)
{
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; // SDF with negative values inside the dot
    // return length(max(q, 0.0)) - r; // SDF with 0 distance inside the dot
}

void main()
{
	vec3 dotColor;

#ifdef DOT_OVERLAP
	dotColor = vec3(0.0);
	for(int x = -1; x <= 1; x++) {
		for(int y = -1; y <= 1; y++) {
			vec2 ofs = vec2(float(x) + 0.5, float(y) + 0.5);
			// Nearest filtering for base dots
			vec2 nearest = (floor(dmdUv * dmdSize) + ofs) / dmdSize;
			// Sampling position as (0,0) at dot center, (-1,-1) to (1,1) in dot corners
			vec2 pos = 2.0 * (fract(dmdUv * dmdSize) - ofs);
			// Dots from the lamp
			float dot = smoothstep(sharpMax, sharpMin, udRoundBox(pos, dotSize, dotRounding * dotSize));
			vec3 dmd = texture(dmdTexture, nearest).rgb;
#ifdef UNLIT
			// Add a little shadow for unlit dots which are lightly visible on real DMDs
			dmd += unlitDot / brightness;
#endif
			dotColor = max(dotColor, dmd * dot);
			// dotColor += dmd * dot; // Additive blending is more logical here but results are better with the max
		}
	}
#else
	// Nearest filtering for base dots
	vec2 nearest = (floor(dmdUv * dmdSize) + vec2(0.5, 0.5)) / dmdSize;
	// Sampling position as (0,0) at dot center, (-1,-1) to (1,1) in dot corners
	vec2 pos = 2.0 * (fract(dmdUv * dmdSize) - vec2(0.5, 0.5));
	// Dots from the lamp
	float dot = smoothstep(sharpMax, sharpMin, udRoundBox(pos, dotSize, dotRounding * dotSize));
	vec3 dmd = texture(dmdTexture, nearest).rgb;
#ifdef UNLIT
	// Add a little shadow for unlit dots which are lightly visible on real DMDs
	dmd += unlitDot / brightness;
#endif
	dotColor = dmd * dot;
#endif

#ifdef BACKGLOW
    // base background diffuse light (very blurry from 3rd level of the DMD blur)
	dotColor += texture(dmdTextureBlur2, dmdUv).rgb * backGlow;
#endif

#ifdef DOTGLOW
	// glow from nearby lamps (taken from first level of blur)
	dotColor += texture(dmdTextureBlur1, dmdUv).rgb * dotGlow;
#endif

#ifdef BRIGHTNESS
	// Apply the overall brightness
	dotColor *= brightness;
#endif

#ifdef GLASS
	// Apply the glass as a tinted (lighten by the DMD, using large blur, and the base color) additive blend.
	vec4 glass = texture(glassTexture, glassUv);
	vec3 glassLight = glassColor.rgb + 2.5 * glassColor.a * texture(dmdTextureBlur3, dmdUv).rgb * brightness;
	dotColor += glass.rgb * glassLight.rgb;
#endif

	// Reinhard tone mapping
	// dotColor = dotColor / (dotColor + vec3(1.0)); 

	// Exposure adjusted tone mapping
	// const float exposure = 2.0;
	// dotColor = vec3(1.0, 1.0, 1.0) - exp(-dotColor * exposure);

#ifdef GAMMA
	// Gamma correction 
	dotColor = pow(dotColor, vec3(1.0 / gamma));
#endif

    FragColor = vec4(dotColor, 1.0);
}