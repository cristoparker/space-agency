// Screen-space god rays: bright parts of the picture (the sky around the sun, sunlit ground) are smeared towards the sun's position
// on the screen and added back, so light seems to stream through gaps in the trees and mountains. Used by GodRaysFeature.
Shader "SpaceAgency/PostFX/GodRays"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "GodRays"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _GodRayParams;     // x intensity, y decay per step, z length (0..1 of the way to the sun), w brightness threshold
            float4 _GodRaySun;        // xy sun position in screen uv, z visibility 0..1
            float4 _GodRayTint;

            #define STEPS 40

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 scene = (half3)min(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb, 60.0);
                if (_GodRaySun.z <= 0.001) return half4(scene, 1);

                float2 toSun = _GodRaySun.xy - uv;
                float2 delta = toSun * (_GodRayParams.z / STEPS);
                float2 p = uv;
                float3 accum = 0;
                float weight = 1.0;
                [loop] for (int i = 0; i < STEPS; i++)
                {
                    p += delta;
                    float3 c = min(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, p).rgb, 3.0);   // the sky's sun disc can be far brighter than 1: keep the sum finite
                    float lum = dot(c, float3(0.3, 0.59, 0.11));
                    float mask = smoothstep(_GodRayParams.w, _GodRayParams.w + 0.3, lum);
                    accum += c * mask * weight;
                    weight *= _GodRayParams.y;
                }
                accum = min(accum * (1.0 / STEPS) * 3.0, 4.0);
                // rays are strongest close to the sun and fade away from it
                float dist = length(toSun);
                float falloff = saturate(1.35 - dist);
                return half4(scene + (half3)(accum * _GodRayParams.x * _GodRaySun.z * falloff * _GodRayTint.rgb), 1);
            }
            ENDHLSL
        }
    }
}
