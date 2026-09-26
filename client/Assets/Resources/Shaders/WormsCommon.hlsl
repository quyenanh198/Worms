#ifndef WORMS_COMMON_INCLUDED
#define WORMS_COMMON_INCLUDED

// Cheap procedural noise so the game needs no texture assets.
float WormsHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float WormsValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = WormsHash21(i);
    float b = WormsHash21(i + float2(1, 0));
    float c = WormsHash21(i + float2(0, 1));
    float d = WormsHash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float WormsFbm(float2 p)
{
    float v = 0;
    float a = 0.5;
    for (int k = 0; k < 4; k++)
    {
        v += a * WormsValueNoise(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return v;
}

#endif
