// Compile with FXC effect compiler using the following command line from a developper powershell:
// fxc /T ps_3_0 /E main .\BaseDmd.fx /Fo"BaseDmd.ps"

// DMD size
float2 Size : register(C0) = float2(128.0, 32.0);

// DMD image, with mipmaps levels 3 and 4 (since WPF does not allow us to use direct GPU generation)
sampler2D Dmd : register(S0);

// Dot brightness
float Brightness : register(C4) = 1.0;

// Dot size
float DotSize : register(C5) = 0.8;


float4 main(float2 uv : TEXCOORD) : COLOR
{
	// Nearest filtering for base dots
	float2 nearest = (floor(uv * Size) + float2(0.5, 0.5)) / Size;
	// Sampling position as (0,0) at dot center, (-1,-1) to (1,1) in dot corners
	float2 pos = 4.4 * (frac(uv * Size) - float2(0.5, 0.5));
	// dots from the lamp (we add a little shadow for unlit dots which are lightly visible on real DMDs)
	float3 dotColor = Brightness * (float3(0.05, 0.05, 0.05) + tex2D(Dmd, nearest).rgb) * smoothstep(-DotSize - 0.8, -DotSize, -length(pos));

	return float4(dotColor, 1.0);
}