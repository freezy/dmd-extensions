
/// <summary>Segment Width</summary>
/// <type>Single</type>
/// <minValue>0</minValue>
/// <maxValue>1</maxValue>
/// <defaultValue>0.07</defaultValue>
float SegmentWidth: register(C0);

int TargetWidth : register(C1);
int TargetHeight : register(C2);
int Segments[17] : register(C3);

int NumLines : register(C4);
int NumChars : register(C5);

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

static float gSegmentWidth = 0.07;
static float gSegmentGap = gSegmentWidth * 1.4;

static float2 dtl = tl + float2(0.0, -gSegmentWidth);
static float2 dtr = tr + float2(0.0, -gSegmentWidth);
static float2 dtm = mm + float2(0.0, gSegmentWidth);
static float2 dbm = mm + float2(0.0, -gSegmentWidth);
static float2 dbl = bl + float2(0.0, gSegmentWidth);
static float2 dbr = br + float2(0.0, gSegmentWidth);

static float2 dp = br + float2(gSegmentWidth * 4.0, gSegmentGap);

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
	return smoothstep(gSegmentWidth, gSegmentWidth * 0.9, Manhattan(pa - ba * t));
}

float ShortLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - a;
	float2 ba = b - a;
	float t = clamp(dot(pa, ba) / dot(ba, ba), gSegmentGap * 2.0, 1.0 - (gSegmentGap * 2.0));
	return smoothstep(gSegmentWidth, gSegmentWidth * 0.9, Manhattan(pa - ba * t));
}

float DiagLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - a;
	float2 ba = b - a;
	float t = clamp((pa.x * ba.x) / (ba.x * ba.x), gSegmentGap * 2.0, 1.0 - (gSegmentGap * 2.0));
	float2 intersectP = abs(pa - ba * t);
	return smoothstep(gSegmentWidth * 2.0, gSegmentWidth * 1.25, intersectP.y) * smoothstep(0.001, 0.0, intersectP.x);
}

float SegDisp(int key, float2 p)
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

float4 main(float2 fragCoord : TEXCOORD) : COLOR {

	float2 iResolution = float2(TargetWidth, TargetHeight);
	float fCharacterCount = 6.0; //float(NumChars);

	float width = 1.5;
	float height = 0.75 + gSegmentWidth * 2.0;
	float2 cellSize = float2(width / (fCharacterCount + 1.0), height / float(NumLines));
	float2 originPos = float2((-width / 2.0) + cellSize.x, (height / 2.0) - cellSize.y * 0.5);
	
	float2 uv = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;

	float2 pos = originPos + float2(1, 0);
	float d = 0.0;

	d += SegDisp(0x088CF, (uv/* - pos*/) * fCharacterCount);
	pos.x += cellSize.x;

	d = lerp(0.0, 1.0, clamp(d, 0.0, 1.0));

	float3 col = lerp(float3(.01, .01, .01), float3(1.0, 0.1, 0), d);

	return float4(col,1.0);
}