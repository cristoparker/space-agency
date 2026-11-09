// Procedural sky for URP: a gradient that follows the sun (night -> sunrise/sunset -> day), a sun disc and glow,
// stars at night and two layers of noise based clouds that drift with the wind and are lit by the sun
// (bright silver edges, shaded undersides, orange/pink at sunrise and sunset).
// DayNightCycle sets the globals _SkySunDir (xyz = direction towards the sun) and _SkyTime every frame.
Shader "SpaceAgency/Sky/Procedural"
{
    Properties
    {
        _CloudCoverage ("Cloud coverage", Range(0, 1)) = 0.55
        _CloudSoftness ("Cloud edge softness", Range(0.05, 0.6)) = 0.3
        _CloudScale ("Cloud scale (bigger = smaller clouds)", Float) = 1.5
        _CloudSpeed ("Wind speed", Float) = 0.02
        _StarStrength ("Star strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CloudCoverage;
                float _CloudSoftness;
                float _CloudScale;
                float _CloudSpeed;
                float _StarStrength;
            CBUFFER_END

            float4 _SkySunDir;     // xyz: towards the sun
            float _SkyTime;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            // ---------------------------------------------------------------- noise
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p, int octaves)
            {
                float s = 0.0, a = 0.5;
                float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
                for (int i = 0; i < octaves; i++)
                {
                    s += a * vnoise(p);
                    p = mul(m, p);
                    a *= 0.5;
                }
                return s;
            }

            // ---------------------------------------------------------------- colour helpers (authored in sRGB, shader works in linear)
            float3 SRGB(float3 c) { return pow(max(c, 0.0), 2.2); }

            // night -> twilight -> golden hour -> day, chosen by the height of the sun
            float3 Key4(float h, float3 n, float3 t, float3 g, float3 d)
            {
                float w1 = smoothstep(-0.18, 0.02, h);
                float w2 = smoothstep(0.0, 0.24, h);
                float w3 = smoothstep(0.2, 0.65, h);
                float3 c = lerp(n, t, w1);
                c = lerp(c, g, w2);
                return lerp(c, d, w3);
            }

            // One cloud layer. Returns coverage (x) and sets `lit` (how much sun reaches the cloud) and `core` (how thick it is).
            float CloudLayer(float3 d, float2 wind, float scale, float2 stretch, float cover, float soft, float3 sunDir, out float lit, out float core)
            {
                float2 uv = d.xz / (d.y + 0.1) * scale * stretch + wind;
                float n = fbm(uv, 5);
                float detail = vnoise(uv * 5.0 + wind * 3.0);
                float thr = lerp(0.82, 0.34, cover);
                float v = n + (detail - 0.5) * 0.12;
                float dens = smoothstep(thr, thr + soft, v);
                core = smoothstep(thr + soft * 0.5, thr + soft + 0.35, v);

                // sample a little towards the sun: more cloud there means this spot is in shadow
                float2 towardSun = normalize(sunDir.xz + 1e-4) * 0.09 * scale;
                float nl = fbm(uv + towardSun, 4);
                lit = saturate(1.0 - (nl - n) * 3.2);
                return dens;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float3 sunDir = normalize(_SkySunDir.xyz);
                float sunH = sunDir.y;

                // ---- sky gradient
                float3 zen = Key4(sunH, SRGB(float3(0.012, 0.02, 0.07)), SRGB(float3(0.16, 0.2, 0.46)), SRGB(float3(0.22, 0.44, 0.84)), SRGB(float3(0.14, 0.4, 0.88)));
                float3 hor = Key4(sunH, SRGB(float3(0.04, 0.06, 0.14)), SRGB(float3(1.0, 0.45, 0.2)), SRGB(float3(1.0, 0.74, 0.52)), SRGB(float3(0.66, 0.82, 0.97)));
                float up = saturate(d.y);
                float3 sky = lerp(zen, hor, pow(1.0 - up, 2.8));

                // warm glow on the horizon around the sun at sunrise / sunset
                float twi = saturate(1.0 - abs(sunH) * 4.2);
                float az = dot(normalize(d.xz + 1e-4), normalize(sunDir.xz + 1e-4)) * 0.5 + 0.5;
                float horizonMask = saturate(1.0 - up * 2.4);
                float3 glowCol = lerp(SRGB(float3(1.0, 0.32, 0.22)), SRGB(float3(1.0, 0.62, 0.32)), saturate(sunH * 5.0 + 0.5));
                sky += glowCol * pow(az, 3.0) * horizonMask * twi * 1.15;
                sky += SRGB(float3(0.75, 0.3, 0.55)) * pow(az, 2.0) * saturate(1.0 - abs(d.y - 0.18) * 4.5) * twi * 0.35;   // pink band above it

                // below the horizon: fade to a darker horizon colour (the terrain hides most of it)
                sky = lerp(sky, hor * 0.55, saturate(-d.y * 4.0));

                // ---- stars
                float night = smoothstep(0.02, -0.16, sunH);
                float3 sp = floor(d * 340.0);
                float h = hash31(sp);
                float star = saturate((h - 0.9978) / 0.0022) * smoothstep(0.03, 0.2, d.y) * night * _StarStrength;
                sky += star * float3(0.9, 0.95, 1.0);

                // ---- sun
                float cosA = dot(d, sunDir);
                float3 sunCol = lerp(SRGB(float3(1.0, 0.5, 0.2)), SRGB(float3(1.0, 0.97, 0.9)), smoothstep(0.0, 0.4, sunH));
                float sunVisible = smoothstep(-0.06, 0.02, sunH);
                float disc = smoothstep(0.99935, 0.99975, cosA);
                float halo = pow(saturate(cosA), 90.0) * 0.8 + pow(saturate(cosA), 8.0) * 0.18;
                sky += sunCol * (disc * 4.0 + halo) * sunVisible;

                // ---- clouds
                float dayLight = saturate(sunH * 4.0 + 0.25);
                float3 nightCloud = SRGB(float3(0.07, 0.085, 0.16));
                float3 litCol = lerp(nightCloud, sunCol * 1.1, dayLight);
                float3 shadeCol = lerp(nightCloud * 0.6, lerp(hor, SRGB(float3(0.38, 0.45, 0.56)), 0.55) * 0.75, dayLight);
                float2 wind = float2(1.0, 0.35) * _SkyTime * _CloudSpeed;

                float fade = smoothstep(0.0, 0.2, d.y);
                float litA, coreA, litB, coreB;
                float low = CloudLayer(d, wind, _CloudScale, float2(1, 1), _CloudCoverage, _CloudSoftness, sunDir, litA, coreA);
                float high = CloudLayer(d, wind * 1.7 + 11.0, _CloudScale * 0.7, float2(0.6, 2.4), _CloudCoverage * 0.7, 0.45, sunDir, litB, coreB) * 0.45;

                float3 cirrus = lerp(shadeCol, litCol, 0.85);
                sky = lerp(sky, cirrus, high * fade * (1.0 - low));

                float3 cloud = lerp(shadeCol, litCol, litA * (1.0 - 0.6 * coreA));
                cloud += sunCol * pow(saturate(cosA), 5.0) * (1.0 - coreA) * low * 0.9 * dayLight;       // silver lining facing the sun
                cloud = lerp(cloud, hor, saturate(1.0 - fade) * 0.7);                                       // merge into the haze at the horizon
                sky = lerp(sky, cloud, low * fade);

                // stars hide behind clouds
                return half4(max(sky, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
