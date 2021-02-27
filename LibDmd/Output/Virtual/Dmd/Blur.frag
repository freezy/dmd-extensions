#version 130

in vec2 uv;

uniform sampler2D texture;
uniform vec2 direction;

out vec4 FragColor;

// The Kernels are derived from https://rastergrid.com/blog/2010/09/efficient-gaussian-blur-with-linear-sampling/
// They are designed to be used with a texture with linear sampling (see reference for explanation)

vec4 blur_level_1(sampler2D image, vec2 uv, vec2 direction) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(0.5) * direction;
	color += texture2D(image, uv + off1) * 0.5;
	color += texture2D(image, uv - off1) * 0.5;
	return color; 
}

vec4 blur_level_2(sampler2D image, vec2 uv, vec2 direction) {
	vec4 color = vec4(0.0);
	vec2 off1 = direction;
	color += texture2D(image, uv) * 0.5;
	color += texture2D(image, uv + off1) * 0.25;
	color += texture2D(image, uv - off1) * 0.25;
	return color; 
}

vec4 blur_level_2_tweaked(sampler2D image, vec2 uv, vec2 direction) {
	vec4 color = vec4(0.0);
	vec2 off1 = direction;
	color += texture2D(image, uv) * 0.75;
	color += texture2D(image, uv + off1) * 0.125;
	color += texture2D(image, uv - off1) * 0.125;
	return color; 
}

vec4 blur_level_4(sampler2D image, vec2 uv, vec2 direction) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(1.2) * direction;
	color += texture2D(image, uv) * 0.375;
	color += texture2D(image, uv + off1) * 0.3125;
	color += texture2D(image, uv - off1) * 0.3125;
	return color; 
}

vec4 blur_level_6(sampler2D image, vec2 uv, vec2 direction) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(1.28571428571429000) * direction;
	color += texture2D(image, uv) * 0.32258064516129000;
	color += texture2D(image, uv + off1) * 0.33870967741935500;
	color += texture2D(image, uv - off1) * 0.33870967741935500;
	return color; 
}

vec4 blur_level_8(sampler2D image, vec2 uv, vec2 direction) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(1.3333333333333333) * direction;
	color += texture2D(image, uv) * 0.29411764705882354;
	color += texture2D(image, uv + off1) * 0.35294117647058826;
	color += texture2D(image, uv - off1) * 0.35294117647058826;
	return color; 
}

vec4 blur_level_12(sampler2D image, vec2 uv, vec2 direction) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(1.3846153846) * direction;
	vec2 off2 = vec2(3.2307692308) * direction;
	color += texture2D(image, uv) * 0.2270270270;
	color += texture2D(image, uv + off1) * 0.3162162162;
	color += texture2D(image, uv - off1) * 0.3162162162;
	color += texture2D(image, uv + off2) * 0.0702702703;
	color += texture2D(image, uv - off2) * 0.0702702703;
	return color;
}
