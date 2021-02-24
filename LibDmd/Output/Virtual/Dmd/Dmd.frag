#version 130

in vec2 dmdUv;
in vec2 glassUv;

uniform sampler2D dmdTexture; // Base DMD
uniform sampler2D dmdTextureBlur1; // DMD Blur level 1
uniform sampler2D dmdTextureBlur2; // DMD Blur level 2
uniform sampler2D dmdTextureBlur3; // DMD Blur level 3
uniform sampler2D glassTexture; // The glass texture (lit by the room's ambient color and by the DMD texture)

uniform vec2 dmdSize; // DMD size
uniform vec3 unlitDot; // light for unlit dots
uniform float backGlow; // Background reflectiveness
uniform float brightness; // Dot brightness
uniform float dotSize; // Dot size
uniform float dotRounding; // Dot corner radius
uniform float dotGlow; // Dot glow
uniform vec4 glassColor; // Glass color: RGB components are used to tint the glass, the A component is the amount of light from the DMD that the glass reflects

out vec4 FragColor;

const float gamma = 2.2;

// from http://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
float udRoundBox(vec2 p, vec2 b, float r)
{
    return length(max(abs(p) - b + r, 0.0)) - r;
}

void main()
{
    // base background diffuse light (very blurry from 3rd level of the DMD blur)
	vec3 dotColor = texture(dmdTextureBlur2, dmdUv).rgb * backGlow;

	// glow from nearby lamps (taken from first level of blur)
	dotColor += texture(dmdTextureBlur1, dmdUv).rgb * dotGlow;

	// Nearest filtering for base dots
	vec2 nearest = (floor(dmdUv * dmdSize) + vec2(0.5, 0.5)) / dmdSize;
	// Sampling position as (0,0) at dot center, (-1,-1) to (1,1) in dot corners
	vec2 pos = 4.4 * (fract(dmdUv * dmdSize) - vec2(0.5, 0.5));
	// Dots from the lamp
	float dot = smoothstep(1.0, 0.0, udRoundBox(pos, vec2(2.0 * dotSize), dotRounding));
	vec3 dmd = texture(dmdTexture, nearest).rgb;
	dotColor += dmd * dot;

	// Apply the overall brightness
	dotColor *= brightness;

	// Add a little shadow for unlit dots which are lightly visible on real DMDs
	dotColor += dot * unlitDot;

	// Apply the glass as a tinted (lighten by the DMD, using large blur, and the base color) additive blend.
	vec4 glass = texture(glassTexture, glassUv);
	vec3 glassLight = glassColor.rgb + 2.5 * glassColor.a * texture(dmdTextureBlur3, dmdUv).rgb * brightness;
	dotColor += glass.rgb * glassLight.rgb;

	// No tone mapping
	vec3 mapped = dotColor;

	// Reinhard tone mapping
	// mapped = mapped / (mapped + vec3(1.0)); 

	// Exposure adjusted tone mapping
	// const float exposure = 2.0;
	// mapped = vec3(1.0, 1.0, 1.0) - exp(-mapped * exposure);

	// Gamma correction 
	mapped = pow(mapped, vec3(1.0 / gamma));

    FragColor = vec4(mapped, 1.0);
}