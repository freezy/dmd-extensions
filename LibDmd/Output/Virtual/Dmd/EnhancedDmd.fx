// Compile with FXC effect compiler using the following command line from a developper powershell:
// fxc /T ps_3_0 /E main .\EnhancedDmd.fx /Fo"EnhancedDmd.ps"

// DMD size
float2 Size : register(C0) = float2(128.0, 32.0);

// Offset and scale of DMD inside Glass
float2 GlassTexOffset : register(C1) = float2(0.0, 0.0);

// Scale for margin glass
float2 GlassTexScale : register(C2) = float2(1.0, 1.0);

// DMD image, with mipmaps levels 3 and 4 (since WPF does not allow us to use direct GPU generation)
sampler2D Dmd : register(S0);
sampler2D DmdLevel1 : register(S1);
sampler2D DmdLevel2 : register(S2);
sampler2D DmdLevel3 : register(S3);
sampler2D DmdLevel4 : register(S4);

// Background reflectiveness
float BackGlow : register(C3) = 0.2;

// Dot brightness
float Brightness : register(C4) = 1.0;

// Dot size
float DotSize : register(C5) = 0.8;

// Dot glow
float DotGlow : register(C6) = 0.0;

// Glass additive overlay image
sampler2D Glass : register(S5);

// Glass color: RGB components are used to tint the glass, the A component is the amount of light from the DMD that the glass reflects
float4 GlassColor : register(C7) = float4(0.0, 0.0, 0.0, 0.0);

// Since WPF won't let us choose clamp mode of the textures, we need to do it in the shader to avoid light leaking
float4 clampTex2D(sampler2D smp, float2 uv) : COLOR
{
	if (uv.x < 0.0 || uv.x >= 1.0 || uv.y < 0.0 || uv.y >= 1.0)
		return float4(0.0,0.0,0.0,0.0);
	else
		return tex2D(smp, uv);
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    // DMD texture coordinates are computed from the glass texture coordinates to allow padding around the DMD with fake dots
    float2 tc = uv * GlassTexScale - GlassTexOffset;

    // base background diffuse light (very blurry from 3rd level of the DMD mipmap)
	float3 dotColor = tex2D(DmdLevel3, uv).rgb * BackGlow;

	// glow from nearby lamps (taken from mipmap level 1). Note that it used to be a smoothstep with texture fetch, but it did not give any visual benefit.
	dotColor += tex2D(DmdLevel2, uv).rgb * DotGlow;

	// Nearest filtering for base dots
	float2 nearest = (floor(tc * Size) + float2(0.5, 0.5)) / Size;
	// Sampling position as (0,0) at dot center, (-1,-1) to (1,1) in dot corners
	float2 pos = 4.4 * (frac(tc * Size) - float2(0.5, 0.5));
	// dots from the lamp (we add a little shadow for unlit dots which are lightly visible on real DMDs)
	dotColor += (float3(0.05, 0.05, 0.05) + clampTex2D(Dmd, nearest).rgb) * smoothstep(-DotSize - 0.8, -DotSize, -length(pos));

	// Apply the overall brightness
	dotColor = dotColor * Brightness;

	// Apply the glass as a tinted (lighten by the DMD and the base color) additive blend.
	float4 glass = tex2D(Glass, uv);
	float3 glassLight = GlassColor.rgb + 2.5 * GlassColor.a * tex2D(DmdLevel4, uv).rgb * Brightness;
	dotColor += glass.rgb * glassLight.rgb * glass.a;

	return float4(dotColor, 1.0);
}