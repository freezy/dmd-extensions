float DotWidth : register(C0);
float DotHeight : register(C1);
float PixelWidth : register(C2);
float PixelHeight : register(C3);
float Size : register(C4);

sampler2D Input : register(S0);

static float2 Resolution = float2(PixelWidth, PixelHeight);
static float2 Dimensions = float2(DotWidth, DotHeight);
static float AspectRatio = DotWidth / DotHeight;
static float2 DotSize = 1.0 / Dimensions;

static float radius = 0.5;

float RoundBox(float2 p, float2 b, float r)
{
	return length(max(abs(p) - b + r, 0.0)) - r;
}

float4 Dot(float2 fragCoord, float4 innerColor, float4 outerColor, float scale)
{
	float2 Dim = Dimensions;
	float2 Res = Resolution;

	float2 uv = fragCoord / Resolution;
	float2 dotSize = Res / Dim;
	float2 dotSizeScaled = dotSize * scale;
	float2 padding = 0.5 * (dotSize - dotSizeScaled);
	float2 uvDot = ((fragCoord % dotSize) - padding) / dotSize;

	float2 halfRes = 0.5 * dotSizeScaled;

	float col = RoundBox(
		uvDot * dotSize - halfRes,
		halfRes,
		radius * scale * min(dotSize.x, dotSize.y) * 0.5
	);

	return lerp(innerColor, outerColor, smoothstep(0.0, 1.0, col));
}
float4 Dmd(float2 fragCoord)
{
	const float4 outerColor = float4(0., 0., 0., 1.);
	 
	const float2 uv = fragCoord / Resolution;
	const float2 dotPos = floor(uv * Dimensions);
	const float2 dotCenter = DotSize * (dotPos + 0.5);
	const float4 innerColor = tex2D(Input, dotCenter);
	
	return Dot(fragCoord, innerColor, outerColor, .65);
}

// Shader
float4 main(float2 fragCoord : VPOS) : COLOR
{
	//return float4(uv.x, uv.y, 0., 1.);
	return Dmd(fragCoord);
}