
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

sampler2D input : register(S0);
sampler2D inputSampler = sampler_state {
	Texture = input;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
};

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
	float2 pa = p - a;
	float2 ba = b - a;
	float t = clamp(dot(pa, ba) / dot(ba, ba), gSegmentGap, 1.0 - gSegmentGap);
	return smoothstep(SegmentWidth, SegmentWidth * 0.9, Manhattan(pa - ba * t));
}

float ShortLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - a;
	float2 ba = b - a;
	float t = clamp(dot(pa, ba) / dot(ba, ba), gSegmentGap * 2.0, 1.0 - (gSegmentGap * 2.0));
	return smoothstep(SegmentWidth, SegmentWidth * 0.9, Manhattan(pa - ba * t));
}

float DiagLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - a;
	float2 ba = b - a;
	float t = clamp((pa.x * ba.x) / (ba.x * ba.x), gSegmentGap * 2.0, 1.0 - (gSegmentGap * 2.0));
	float2 intersectP = abs(pa - ba * t);
	return smoothstep(SegmentWidth * 2.0, SegmentWidth * 1.25, intersectP.y) * smoothstep(0.001, 0.0, intersectP.x);
}

float SegDisp(float2 p)
{
	float r = 0.0;
	r += ShortLine(dp, dp + float2(gSegmentGap * 0.5, 0.0), p);
	r += ShortLine(mm, ml, p);
	r += DiagLine(dbm, dbl, p);
	r += LongLine(mm, bm, p);
	r += DiagLine(dbm, dbr, p); 
	r += ShortLine(mr, mm, p);
	r += DiagLine(dtr, dtm, p); 
	r += LongLine(tm, mm, p);
	r += DiagLine(dtl, dtm, p);
	r += LongLine(ml, tl, p);
	r += LongLine(bl, ml, p);
	r += ShortLine(bm, bl, p);
	r += ShortLine(br, bm, p);
	r += LongLine(mr, br, p);
	r += LongLine(tr, mr, p);
	r += ShortLine(tm, tr, p);
	r += ShortLine(tl, tm, p);
	return r;
}

bool ShowSeg(int charIndex, int segIndex)
{ 
	float2 d = float2(1. / NumSegments, 1. / NumChars);
	float2 pos = float2(float(segIndex), float(charIndex));
	float4 pixel = tex2Dlod(input, float4(d.x * pos.x, d.y * pos.y, 0., 0.));
	
	if (pixel.b > 0.0) {
		return true;
	}
	return false;
}

float Seg(int charIndex, float2 p)
{
	float r = 0.0;
	
	if (ShowSeg(charIndex, 0)) r += ShortLine(dp, dp + float2(gSegmentGap * 0.5, 0.0), p);
	if (ShowSeg(charIndex, 1)) r += ShortLine(mm, ml, p);
	if (ShowSeg(charIndex, 2)) r += DiagLine(dbm, dbl, p);
	if (ShowSeg(charIndex, 3)) r += LongLine(mm, bm, p);
	if (ShowSeg(charIndex, 4)) r += DiagLine(dbm, dbr, p);
	if (ShowSeg(charIndex, 5)) r += ShortLine(mr, mm, p);
	if (ShowSeg(charIndex, 6)) r += DiagLine(dtr, dtm, p);
	if (ShowSeg(charIndex, 7)) r += LongLine(tm, mm, p);
	if (ShowSeg(charIndex, 8)) r += DiagLine(dtl, dtm, p);
	if (ShowSeg(charIndex, 9)) r += LongLine(ml, tl, p);
	if (ShowSeg(charIndex, 10)) r += LongLine(bl, ml, p);
	if (ShowSeg(charIndex, 11)) r += ShortLine(bm, bl, p);
	if (ShowSeg(charIndex, 12)) r += ShortLine(br, bm, p);
	if (ShowSeg(charIndex, 13)) r += LongLine(mr, br, p);
	if (ShowSeg(charIndex, 14)) r += LongLine(tr, mr, p);
	if (ShowSeg(charIndex, 15)) r += ShortLine(tm, tr, p);
	r += ShortLine(tl, tm, p);
	
	return r;
}

float4 main(float2 fragCoord : VPOS) : COLOR
{
	float2 resolution = float2(TargetWidth, TargetHeight);
	float numChars = NumChars;
	float numLines = NumLines;
	
	/*return tex2Dlod(input, float4(
		fragCoord.x / resolution.x,
		fragCoord.y / resolution.y,
		0.,
		0.
	));*/

	float2 cellSize = float2(
		1 / numChars,
		1 / numLines * 2 // * 2.0 + linePadding * height * (numLines - 1) // + 2.0 * verticalPadding
	);
	
	float2 originPos = float2(
		-0.5 + cellSize.x / 2,
		0.0
	);
	
	float2 uv = float2(
		(fragCoord.x / resolution.x) - 0.5,
		(fragCoord.y / resolution.y * 2) - 1
	);

	float2 pos = originPos;
	float d = 0.0;
	
	int charIndex = 0;
	//for (int currLine = 0; currLine < numLines; currLine++) {
	for (int character = 0; character < 20; character++) {
			d += Seg(charIndex, (uv - pos) * float2(numChars, numLines));
			//d += SegDisp((uv - pos) * float2(numChars, numLines));
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