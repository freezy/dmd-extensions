/// <summary>The number pixel blocks.</summary>
/// <type>Single</type>
/// <minValue>2</minValue>
/// <maxValue>500</maxValue>
/// <defaultValue>128</defaultValue>
float BlockCount : register(C0);

/// <summary>The rounding of a pixel block.</summary>
/// <type>Single</type>
/// <minValue>0</minValue>
/// <maxValue>5</maxValue>
/// <defaultValue>1.88</defaultValue>
float Max : register(C2);

/// <summary>The aspect ratio of the image.</summary>
/// <type>Single</type>
/// <minValue>0</minValue>
/// <maxValue>10</maxValue>
/// <defaultValue>4</defaultValue>
float AspectRatio : register(C3);

/// <summary>The monochrome color used to tint the input.</summary>
/// <defaultValue>Red</defaultValue>
float4 FilterColor : register(C1);

/// <summary>monochrome.</summary>
/// <defaultValue>0</defaultValue>
float IsMonochrome : register(C4);

// Sampler
sampler2D input : register(S0);

// Static computed vars for optimization
static float2 BlockCount2 = float2(BlockCount, BlockCount / AspectRatio);
static float2 BlockSize2 = 1.0f / BlockCount2;

float4 setMonochrome(float4 color) : COLOR
{
	float4 monochrome = color;
	if (((int)IsMonochrome) == 1)
	{
		float3 rgb = color.rgb;
		float3 luminance = dot(rgb, float3(0.30, 0.59, 0.11));
		monochrome = float4(luminance * FilterColor.rgb, color.a);
	}
	return monochrome;
}

float4 SetDMD(float2 uv : TEXCOORD, sampler2D samp) : COLOR
{
	// Calculate block center
	float2 blockPos = floor(uv * BlockCount2);
	float2 blockCenter = blockPos * BlockSize2 + BlockSize2 * 0.5;

	// Scale coordinates back to original ratio for rounding
	float2 uvScaled = float2(uv.x * AspectRatio, uv.y);
	float2 blockCenterScaled = float2(blockCenter.x * AspectRatio, blockCenter.y);

	// Round the block by testing the distance of the pixel coordinate to the center
	float dist = length(uvScaled - blockCenterScaled) * BlockCount2;

	float4 insideColor = tex2D(samp, blockCenter);

	float4 outsideColor = insideColor;
	outsideColor.r = 0;
	outsideColor.g = 0;
	outsideColor.b = 0;
	outsideColor.a = 1;

	float distFromEdge = Max - dist;  // positive when inside the circle
	float thresholdWidth = .22;  // a constant you'd tune to get the right level of softness
	float antialiasedCircle = saturate((distFromEdge / thresholdWidth) + 0.5);

	return lerp(outsideColor, insideColor, antialiasedCircle);
}

// Shader
float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 DMD = SetDMD(uv, input);
	DMD = setMonochrome(DMD);

	return DMD;
}