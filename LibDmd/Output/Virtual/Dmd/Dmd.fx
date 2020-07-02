float Width : register(C0);
float Height : register(C1);
float Size : register(C2);

// Sampler
sampler2D input : register(S0);

static float AspectRatio = Width / Height;
static float2 Dimensions = float2(Width, Height);
static float2 DimensionsPerDot = 1.0 / Dimensions;

float4 Dmd(float2 uv : TEXCOORD) : COLOR
{
	// Calculate dot center
	float2 dotPos = floor(uv * Dimensions);
	float2 dotCenter = DimensionsPerDot * (dotPos + 0.5);

	// Scale coordinates back to original ratio for rounding
	float2 uvScaled = float2(uv.x * AspectRatio, uv.y);
	float2 dotCenterScaled = float2(dotCenter.x * AspectRatio, dotCenter.y);

	// Round the dot by testing the distance of the pixel coordinate to the center
	float dist = length(uvScaled - dotCenterScaled) * Dimensions;

	float4 insideColor = tex2D(input, dotCenter);
	float4 outsideColor = float4(0, 0, 0, 1);

	float distFromEdge = Size - dist;  // positive when inside the circle
	float thresholdWidth = .22;  // a constant you'd tune to get the right level of softness
	float antialiasedCircle = saturate((distFromEdge / thresholdWidth) + 0.5);

	return lerp(outsideColor, insideColor, antialiasedCircle);
}

// Shader
float4 main(float2 uv : TEXCOORD) : COLOR
{
	return Dmd(uv);
}