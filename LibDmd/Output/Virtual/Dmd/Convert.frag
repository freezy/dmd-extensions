
in vec2 uv;

uniform sampler2D palette;
uniform sampler2D dmdData;

out vec4 FragColor;

vec3 lutPalette(float luminance)
{
	return texture(palette, vec2((0.5 + luminance * 15.0) / 16.0, 0.5)).rgb;
}

vec3 decodeGray2()
{
	return lutPalette(texture(dmdData, uv).r * 255.0 / 3.0);
}

vec3 decodeGray4()
{
	return lutPalette(texture(dmdData, uv).r * 255.0 / 15.0);
}

vec3 decodeRGB()
{
	vec3 color = texture(dmdData, uv).rgb;
	return color;
}

void main()
{
#if defined(GRAY2)
	vec3 color = decodeGray2();
#elif defined(GRAY4)
	vec3 color = decodeGray4();
#elif defined(RGB24)
	vec3 color = decodeRGB();
#elif defined(BITMAP)
	vec3 color = decodeRGB();
#endif

#ifdef GAMMA
	color = pow(color, vec3(gamma));
#endif

	FragColor = vec4(color, 1.0);
}