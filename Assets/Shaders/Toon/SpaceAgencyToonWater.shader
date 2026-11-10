// Cartoon water: banded shallow-to-deep colour from the scene depth, a foam line along the shore, stepped ripple lines, a hard sun glint,
// and a posterised planar reflection (rendered by WaterReflection) that is wobbled by the ripples and faded by a stepped fresnel.
Shader "SpaceAgency/Toon/Water"
{
    Properties
    {
        _Shallow ("Shallow colour", Color) = (0.30, 0.82, 0.82, 1)
        _Deep ("Deep colour", Color) = (0.05, 0.34, 0.66, 1)
        _DepthRange ("Depth for full deep colour (m)", Float) = 16
        _Bands ("Colour bands", Range(2, 8)) = 4
        _FoamColor ("Foam colour", Color) = (1, 1, 1, 1)
        _FoamWidth ("Shore foam width (m)", Float) = 2.6
        _ReflectStrength ("Reflection strength", Range(0, 1)) = 0.8
        _ReflectLevels ("Reflection colour levels", Range(2, 10)) = 5
        _Distort ("Reflection wobble", Range(0, 0.1)) = 0.025
        _WaveScale ("Ripple scale", Float) = 0.07
        _WaveSpeed ("Ripple speed", Float) = 0.35
        _Glint ("Sun glint", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_WaterReflectionTex); SAMPLER(sampler_WaterReflectionTex);
            float4 _SkySunDir;
            float4 _SpaceFogColor;
            float4 _SpaceFogParams;

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _FoamColor;
                float _DepthRange, _Bands, _FoamWidth, _ReflectStrength, _ReflectLevels, _Distort, _WaveScale, _WaveSpeed, _Glint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 wp = IN.positionWS;
                float2 suv = IN.positionCS.xy / _ScaledScreenParams.xy;
                float t = _Time.y * _WaveSpeed;

                float2 p = wp.xz * _WaveScale;
                float n1 = vnoise(p + float2(t, t * 0.6));
                float n2 = vnoise(p * 2.1 - float2(t * 0.8, t * 0.4) + 7.3);
                float n3 = vnoise(p * 0.5 + float2(-t * 0.3, t * 0.5) + 3.1);
                float ripple = n1 * 0.5 + n2 * 0.5;

                // depth below the surface -> banded colour
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float diff = max(0.0, sceneZ - IN.positionCS.w);
                float depthT = saturate(diff / _DepthRange);
                float banded = floor(depthT * _Bands) / max(1.0, _Bands - 1.0);
                float ct = saturate(lerp(depthT, banded, 0.75));
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, ct);
                col *= 0.9 + 0.18 * step(0.55, n3);                                   // large soft patches

                // planar reflection, wobbled by the ripples and posterised
                float3 V = normalize(_WorldSpaceCameraPos - wp);
                float fres = pow(1.0 - saturate(V.y), 2.5);
                float fresStep = floor(fres * 3.0 + 0.5) / 3.0;
                float2 wob = (float2(n1, n2) - 0.5) * _Distort;
                half3 refl = SAMPLE_TEXTURE2D(_WaterReflectionTex, sampler_WaterReflectionTex, suv + wob).rgb;
                refl = floor(refl * _ReflectLevels + 0.5) / _ReflectLevels;
                float lum = dot(refl, half3(0.3, 0.59, 0.11));
                refl = lerp(lum.xxx, refl, 1.25);                                        // slightly more colourful than the real thing
                float ra = _ReflectStrength * lerp(0.4, 1.0, fresStep);
                col = lerp(col, refl * lerp(1.0, col * 1.6, 0.25), ra);

                // stepped ripple highlights
                float rl = step(0.70, ripple) * (1.0 - step(0.76, ripple));
                col = lerp(col, 1.0, rl * 0.28);

                // hard sun glint
                float3 sun = normalize(_SkySunDir.xyz);
                float3 R = reflect(-V, normalize(float3((n1 - 0.5) * 0.25, 1.0, (n2 - 0.5) * 0.25)));
                float glint = step(0.992, dot(R, sun)) * _Glint * saturate(sun.y * 4.0);
                col = lerp(col, half3(1, 0.97, 0.85), glint);

                // shore foam: a bright band that wobbles with the ripples, plus a thin outer line
                float shore = 1.0 - saturate(diff / _FoamWidth);
                float foam = step(0.55, shore + (n1 - 0.5) * 0.7 + sin(_Time.y * 1.3 + diff * 1.5) * 0.06);
                float foam2 = step(0.18, shore) * (1.0 - step(0.26, shore + (n2 - 0.5) * 0.4));
                col = lerp(col, _FoamColor.rgb, saturate(foam + foam2 * 0.7));

                // sky colour: the water takes on the horizon colour (the same one the fog uses), strongest at sunrise / sunset
                float sunH = _SkySunDir.y;
                float twi = saturate(1.0 - abs(sunH) * 2.6);
                half lumW = dot(col, half3(0.3, 0.59, 0.11));
                half3 skyTint = _SpaceFogColor.rgb * (lumW * 1.9 + 0.2);               // keeps the depth shading, swaps the hue
                col = lerp(col, skyTint, saturate(twi * 1.25));
                col = lerp(col, _SpaceFogColor.rgb * 0.9, saturate(fres) * twi * 0.35);   // grazing angles mirror the sky glow
                col += _SpaceFogColor.rgb * 0.05 * (1.0 - saturate(sunH * 6.0 + 0.2));    // a little sky light even after dark

                // night: the whole water goes dark and blue
                float day = saturate(_SkySunDir.y * 3.0 + 0.3);
                col *= lerp(0.22, 1.0, day);
                col = lerp(col, col * half3(0.7, 0.8, 1.3), 1.0 - day);

                float fogF = saturate((IN.positionCS.w - _SpaceFogParams.x) / max(1.0, _SpaceFogParams.y - _SpaceFogParams.x)) * _SpaceFogParams.z;
                col = lerp(col, _SpaceFogColor.rgb, fogF);
                float alpha = lerp(0.55, 0.96, saturate(depthT * 4.0));
                alpha = saturate(alpha + foam + glint);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
