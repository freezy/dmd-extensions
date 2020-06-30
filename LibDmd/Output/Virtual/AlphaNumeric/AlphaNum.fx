
/// <summary>Segment Width</summary>
/// <type>Single</type>
/// <minValue>0</minValue>
/// <maxValue>1</maxValue>
/// <defaultValue>0.07</defaultValue>
float SegmentWidth: register(C0);

float TargetWidth : register(C1);
float TargetHeight : register(C2);

float NumLines : register(C3);
float NumChars : register(C4);
float NumSegments : register(C5);

float4 Color : register(C6);
float4 InnerColor : register(C7);
float4 OuterColor : register(C8);
float4 UnlitColor : register(C9);

static float InnerPaddingX = 0.5;
static float InnerPaddingY = 0.4;
static float OuterPaddingX = 0.2;
static float OuterPaddingY = 0.2;

sampler2D input : register(S0);

// Static computed vars for optimization
static float2 tl = float2(-.5, 1) ; // top    left  corner
static float2 tr = float2(.5, 1);   // top    right corner
static float2 ml = float2(-.5, 0);  // mid    left  corner
static float2 mr = float2(.5, 0);   // mid    right corner
static float2 bl = float2(-.5, -1); // bottom left  corner
static float2 br = float2(.5, -1);  // bottom right corner
static float2 tm = float2(.0, 1);
static float2 mm = float2(.0, 0);   // middle
static float2 bm = float2(.0, -1);

static float gSegmentGap = SegmentWidth * 1.4;

static float2 dtl = tl + float2(0.0, -SegmentWidth);
static float2 dtr = tr + float2(0.0, -SegmentWidth);
static float2 dtm = mm + float2(0.0, SegmentWidth);
static float2 dbm = mm + float2(0.0, -SegmentWidth);
static float2 dbl = bl + float2(0.0, SegmentWidth);
static float2 dbr = br + float2(0.0, SegmentWidth);

static float2 dp = br + float2(SegmentWidth * 4.0, gSegmentGap);

static float2 resolution = float2(TargetWidth, TargetHeight);

static float2 outerPadding = float2(OuterPaddingX * resolution.y / resolution.x, OuterPaddingY);
static float2 innerPadding = float2(InnerPaddingX * resolution.y / resolution.x, InnerPaddingY);
	
static float2 cellSize = float2(
	1. / NumChars + innerPadding.x,
	1. / NumLines * 2. + SegmentWidth * 2.
);
	
static float2 originPos = float2(
	-.5 + cellSize.x / 2. - innerPadding.x / 2. + outerPadding.x,
	SegmentWidth + outerPadding.y
);
	

float Manhattan(float2 v)
{
	return abs(v.x) + abs(v.y);
}

float DiagDist(float2 v)
{
	return abs(v.x);
}

float LongLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp(dot(pa, ba) / dot(ba, ba), gSegmentGap, 1.0 - gSegmentGap);
	return smoothstep(SegmentWidth, SegmentWidth * 0.9, Manhattan(pa - ba * t));
}

float ShortLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp(dot(pa, ba) / dot(ba, ba), gSegmentGap * 2.0, 1.0 - (gSegmentGap * 2.0));
	return smoothstep(SegmentWidth, SegmentWidth * 0.9, Manhattan(pa - ba * t));
}

float DiagLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp((pa.x * ba.x) / (ba.x * ba.x), gSegmentGap * 2.0, 1.0 - (gSegmentGap * 2.0));
	float2 intersectP = abs(pa - ba * t);
	return smoothstep(SegmentWidth * 2.0, SegmentWidth * 1.25, intersectP.y) * smoothstep(0.001, 0.0, intersectP.x);
}

bool ShowSeg(int charIndex, int segIndex)
{ 
	float2 d = float2(1. / NumSegments, 1. / NumChars);
	float2 pos = float2(float(segIndex), float(charIndex));
	float4 pixel = tex2Dlod(input, float4(d.x * (pos.x + .5), d.y * (pos.y + .5), 0., 0.));
	if (pixel.b > .5) {
		return true;
	}
	return false;
}

float Seg(int charIndex, float2 p)
{
	float r = 0.0;
	if (ShowSeg(charIndex, 0)) r += ShortLine(tl, tr, p);
	if (ShowSeg(charIndex, 1)) r += LongLine(tr, mr, p);
	if (ShowSeg(charIndex, 2)) r += LongLine(mr, br, p);
	if (ShowSeg(charIndex, 3)) r += ShortLine(br, bl, p);
	if (ShowSeg(charIndex, 4)) r += LongLine(bl, ml, p);
	if (ShowSeg(charIndex, 5)) r += LongLine(ml, tl, p);
	if (ShowSeg(charIndex, 6)) r += ShortLine(mm, ml, p);
	if (ShowSeg(charIndex, 7)) r += ShortLine(dp, dp + float2(gSegmentGap * 0.5, 0.0), p);
	if (ShowSeg(charIndex, 8)) r += DiagLine(dtl, dtm, p);
	if (ShowSeg(charIndex, 9)) r += LongLine(tm, mm, p);
	if (ShowSeg(charIndex, 10)) r += DiagLine(dtr, dtm, p);
	if (ShowSeg(charIndex, 11)) r += ShortLine(mm, mr, p);
	if (ShowSeg(charIndex, 12)) r += DiagLine(dbm, dbr, p);
	if (ShowSeg(charIndex, 13)) r += LongLine(mm, bm, p);
	if (ShowSeg(charIndex, 14)) r += DiagLine(dbm, dbl, p);
	return r;
}

float4 main(float2 fragCoord : VPOS) : COLOR
{

	float2 uv = float2(
		(fragCoord.x / resolution.x) * (1 + (NumChars - 1) * innerPadding.x + 4.0 * outerPadding.x) - 0.5 - outerPadding.x,
		(fragCoord.y / resolution.y * 2.) * (1 + SegmentWidth + 2. * outerPadding.y) - 1. - outerPadding.y
	);
		
	float2 pos = originPos;
	float d = 0.0;
	
	int charIndex = 0;
	//for (int currLine = 0; currLine < numLines; currLine++) {
	for (int character = 0; character < 20; character++) {
		d += Seg(charIndex, (uv - pos) * float2(NumChars * (1. + innerPadding.x), NumLines));
		pos.x += cellSize.x;
		charIndex++;
	}
	pos.x = originPos.x;
	pos.y -= cellSize.y;
	//}
	
	float g = 0;
	float b = 0;

	d = lerp(0.0, 1.0, clamp(d, 0.0, 1.0));

	float3 col = float3(d, g, b);
	return float4(col, 1.0);
}