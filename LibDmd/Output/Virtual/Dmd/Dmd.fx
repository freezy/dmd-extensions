float Width : register(C0);
float Height : register(C1);
float NumRows : register(C2);
float NumCols : register(C3);
float Size : register(C4);

sampler2D Input : register(S0);

static float2 Resolution = float2(Width, Height);
static float AspectRatio = NumCols / NumRows;
static float2 Dimensions = float2(NumCols, NumRows);
static float2 DotSize = 1.0 / Dimensions;

float RoundBox(float2 p, float2 b, float r)
{
	return length(max(abs(p) - b + r, 0.0)) - r;
}

float circle(in float2 _st, in float _radius)
{
	float2 dist = _st - float2(0.5, 0.5);
	return 1. - smoothstep(_radius - (_radius * 0.01),
                         _radius + (_radius * 0.01),
                         dot(dist, dist) * 4.0);
}

float4 Dmd(float2 uv : TEXCOORD) : COLOR
{
	float uvScaled = uv / Dimensions;
	float c = circle(uvScaled, 0.9);
	return float4(c, c, c, 1.0);
	
	/*
	// Calculate dot center
	float2 dotPos = floor(uv * Dimensions);
	float2 dotCenter = DotSize * (dotPos + 0.5);
	
	// Scale coordinates back to original ratio for rounding
	float2 uvScaled = float2(uv.x * AspectRatio, uv.y);
	float2 dotCenterScaled = float2(dotCenter.x * AspectRatio, dotCenter.y);
	
	// setup
	float t = -1.2;
	float radius = min(DotSize.x, DotSize.y) * (0.05 + t);
	float2 halfRes = 0.5 * DotSize;

	// compute box
	float b = RoundBox(dotPos - halfRes, halfRes, radius);

	// colorize
	float4 insideColor = tex2D(Input, dotCenter);
	float4 outsideColor = float4(0, 0, 0, 1);
	float3 c = lerp(insideColor, outsideColor, smoothstep(0.0, 1.0, b));

	return float4(c, 1.0);
	/*

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

	return lerp(outsideColor, insideColor, antialiasedCircle);*/
}

// Shader
float4 main(float2 uv : TEXCOORD) : COLOR
{
	return Dmd(uv);
}