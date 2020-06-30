
/// <summary>Segment Width</summary>
/// <type>Single</type>
/// <minValue>0</minValue>
/// <maxValue>1</maxValue>
/// <defaultValue>0.07</defaultValue>
float SegmentWidth : register(C0);

float TargetWidth : register(C1);
float TargetHeight : register(C2);

float NumLines : register(C3);
float NumChars : register(C4);
float NumSegments : register(C5);

// those are height percentages
static float InnerPaddingX = 0.5;
static float InnerPaddingY = 0.4;
static float OuterPaddingX = 0.2;
static float OuterPaddingY = 0.2;

static float SegmentGap = SegmentWidth * 1.2;

static float Skew = -0.2;
	
static float EdgeBlur = 0.1; // used to remove aliasing

static float SharpEdge = 0.7;
static float RoundEdge = 0.15;
	
static float InnerGlow = 2.0;

static float OuterGlowLevel = .15;
static float OuterGlowRange = 15.0;
	
static float On = 0.81;
static float Off = 0.025;

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

static float2 dtl = tl + float2(0.0, -SegmentWidth);
static float2 dtr = tr + float2(0.0, -SegmentWidth);
static float2 dtm = mm + float2(0.0, SegmentWidth);
static float2 dbm = mm + float2(0.0, -SegmentWidth);
static float2 dbl = bl + float2(0.0, SegmentWidth);
static float2 dbr = br + float2(0.0, SegmentWidth);

static float2 dp = br + float2(SegmentWidth * 4.0, SegmentGap);

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
	

float4 SplitAndBlur(float v)
{
	float be = clamp(EdgeBlur, 0, 9999);
	float bo = clamp(OuterGlowLevel, 0, 9999);
	float br = clamp(OuterGlowRange, 0, 9999);
	float re = clamp(RoundEdge, 0, 9999);
	float ig = clamp(InnerGlow, 0, 9999);
	float edge = SharpEdge - re - ig;
	br = clamp(OuterGlowRange, edge + be + 0.00001, 9999);
	float e1 = SharpEdge - RoundEdge;
	float e2 = e1 - ig;
	
	float r = smoothstep(e1 - be, e1 + be, v);
	float g = smoothstep(e2 - be, e1 - be, v);
	float b = smoothstep(-9999, e2 + be, v);
	g -= r;
	
	// a is only used for outer glow
	float a = smoothstep(e2 - OuterGlowRange, edge, v) * OuterGlowLevel;
	return float4(r, g, b, a);
}

float Rounder(float x, float y, float z)
{
	if (z < 0) {
		x = lerp(min(x, z), x, clamp(1 + z, 0, 1));
	}
	float d = min(x, y);

	if (x < SharpEdge) {
		x = SharpEdge - x;
		y = SharpEdge - y;
		x = clamp(x, 0, 9999);
		y = clamp(y, 0, 9999);
		d = SharpEdge - length(float2(x, y));
	
	} else if (y < SharpEdge) {
		x = SharpEdge - x;
		y = SharpEdge - y;
		x = clamp(x, 0, 9999);
		y = clamp(y, 0, 9999);
		d = SharpEdge - length(float2(x, y));
	}
	return d;
}

float Rounder2(float x, float y)
{

	float d = y;

	if (d < SharpEdge && x > y) {
		float a = x - y;
		float b = SharpEdge - x;
		if (x < SharpEdge && a < b) {
			d = SharpEdge - length(float2(a, b));
		
		} else if (SharpEdge - 1 + x - y > 1 - x) {
			a = SharpEdge - 1 + a;
			b = 1 - x;
			d = SharpEdge - length(float2(a, b));
		
		} else {
			d = SharpEdge + (d - SharpEdge) * 0.70710678118654752440084436210485;
		}
	}
	return d;
}

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
	float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap, 1.0 - SegmentGap);
	float2 v = abs(pa - ba * t) / SegmentWidth * 0.5;

	return Rounder2(1 - v.x, 1 - v.y - v.x);
}

float ShortLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap * 2.0, 1.0 - SegmentGap * 2.0);
	float2 v = abs(pa - ba * t) / SegmentWidth * 0.5;

	return Rounder2(1 - v.x, 1 - v.y - v.x);
}

float DiagLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = pa.x / ba.x;
	float2 intersectP = abs(pa - ba * t);
	float xl = clamp(1 - (p.x - a.x + SegmentGap + SegmentWidth) / SegmentWidth * 0.5, -9999, 1);
	float xr = clamp(0 + (p.x - b.x - SegmentGap + SegmentWidth) / SegmentWidth * 0.5, -9999, 1);
 

	float t2 = pa.y / ba.y;
	float yu = clamp(t2 + 1.0 - SegmentGap * 2, -9999, 1);
	float yd = clamp(2 - t2 - SegmentGap * 2, -9999, 1);
	
	return Rounder(
		1 - intersectP.y / (SegmentWidth * 3.0),
		xl * xr,
		(yd * yu) / SegmentGap * 0.5 - 4.0
	);
}

float DiagLine2(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = pa.x / ba.x;
	float2 intersectP = abs(pa - ba * t);
	float xr = clamp(0 + (p.x - a.x - SegmentGap + SegmentWidth) / SegmentWidth * 0.5, -9999, 1);
	float xl = clamp(1 - (p.x - b.x + SegmentGap + SegmentWidth) / SegmentWidth * 0.5, -9999, 1);
  
	float t2 = pa.y / ba.y;
	float yu = clamp(t2 + 1.0 - SegmentGap * 2, -9999, 1);
	float yd = clamp(2 - t2 - SegmentGap * 2, -9999, 1);
	
	return Rounder(
		1 - intersectP.y / (SegmentWidth * 3.0),
		xl * xr,
		(yd * yu) / SegmentGap * 0.5 - 4.0
	);
}


float3 Combine(float3 accu, float val, bool showSeg)
{
	float lev = (showSeg ? 1. : 0.) * (On - Off) + Off;
	float4 v = SplitAndBlur(val);
	v.a *= lev;

	return float3(
		max(accu.r, v.r * lev),
		length(float2(accu.g, v.g * lev)),
		length(float2(accu.b, v.b * v.a))
	);
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

float3 SegDisp(int charIndex, float2 p)
{
	float3 r = float3(0., 0., 0.);
	p.x -= p.y * Skew;
	
	r = Combine(r, ShortLine(tl, tr, p), ShowSeg(charIndex, 0));
	r = Combine(r, LongLine(tr, mr, p), ShowSeg(charIndex, 1));
	r = Combine(r, LongLine(mr, br, p), ShowSeg(charIndex, 2));
	r = Combine(r, ShortLine(br, bl, p), ShowSeg(charIndex, 3));
	r = Combine(r, LongLine(bl, ml, p), ShowSeg(charIndex, 4));
	r = Combine(r, LongLine(ml, tl, p), ShowSeg(charIndex, 5));
	r = Combine(r, ShortLine(mm, ml, p), ShowSeg(charIndex, 6));
	r = Combine(r, ShortLine(dp, dp + float2(SegmentGap * 0.5, 0.0), p), ShowSeg(charIndex, 7));
	r = Combine(r, DiagLine(dtl, dtm, p), ShowSeg(charIndex, 8));
	r = Combine(r, LongLine(tm, mm, p), ShowSeg(charIndex, 9));
	r = Combine(r, DiagLine(dtr, dtm, p), ShowSeg(charIndex, 10));
	r = Combine(r, ShortLine(mm, mr, p), ShowSeg(charIndex, 11));
	r = Combine(r, DiagLine(dbm, dbr, p), ShowSeg(charIndex, 12));
	r = Combine(r, LongLine(mm, bm, p), ShowSeg(charIndex, 13));
	r = Combine(r, DiagLine(dbm, dbl, p), ShowSeg(charIndex, 14));
	
	return r;
}

float4 main(float2 fragCoord : VPOS) : COLOR
{
	float2 uv = float2(
		(fragCoord.x / resolution.x) * (1. + (NumChars - 1.) * innerPadding.x + 4.0 * outerPadding.x) - 0.5 - outerPadding.x,
		(fragCoord.y / resolution.y * 2.) * (1. + SegmentWidth + 2. * outerPadding.y) - 1. - outerPadding.y
	);
		
	float2 pos = originPos;
	float3 d = float3(0., 0., 0.);
	
	int charIndex = 0;
	float2 f = float2(NumChars * (1. + innerPadding.x), NumLines);
	for (int currLine = 0; currLine < 2; currLine++) {
		for (int character = 0; character < 20; character++) {
		
			d += SegDisp(charIndex, (uv - pos) * f);
			pos.x += cellSize.x;
			charIndex++;
		
			if (character >= NumChars - 1.) {
				break;
			}
		}
		pos.x = originPos.x;
		pos.y -= cellSize.y;
		if (character >= NumLines - 1.) {
			break;
		}
	}
	
	float3 color = float3(1.0, 0.9, 0);
	float3 innerGlowColor = float3(0.51, 0.01, 0.01);
//	float3 outerGlowColor = float3(0.30, 0.60, 0.10);
	float3 outerGlowColor = float3(0.50, 0.30, 0.0);
	
	float3 col = d.r * color
		+ d.g * innerGlowColor
		+ d.b * outerGlowColor;

	return float4(col, 1.0);
}
