// Cel-shaded terrain: blends flat layer colours by the terrain splat (control) map. No textures, no pixel art.
Shader "SpaceAgency/Toon/Terrain"
{
    Properties
    {
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "white" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "white" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "white" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "white" {}
        _Color0 ("Layer 0 colour", Color) = (0.36, 0.62, 0.28, 1)
        _Color1 ("Layer 1 colour", Color) = (0.50, 0.50, 0.52, 1)
        _Color2 ("Layer 2 colour", Color) = (0.85, 0.74, 0.50, 1)
        _Color3 ("Layer 3 colour", Color) = (0.30, 0.30, 0.30, 1)
        _ToonParams ("Thresholds (low, high, softness, ambient)", Vector) = (0.05, 0.5, 0.02, 0.4)
        _ToonLevels ("Band levels (dark, mid)", Vector) = (0.15, 0.6, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-100" "RenderPipeline" = "UniversalPipeline" "TerrainCompatible" = "True" "SplatCount" = "4" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "ToonCommon.hlsl"

            TEXTURE2D(_Control); SAMPLER(sampler_Control);

            CBUFFER_START(UnityPerMaterial)
                float4 _Control_ST;
                half4 _Color0, _Color1, _Color2, _Color3;
                float4 _ToonParams;
                float4 _ToonLevels;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 w = SAMPLE_TEXTURE2D(_Control, sampler_Control, IN.uv);
                half sum = max(w.r + w.g + w.b + w.a, 0.0001h);
                w /= sum;
                half3 albedo = _Color0.rgb * w.r + _Color1.rgb * w.g + _Color2.rgb * w.b + _Color3.rgb * w.a;
                half3 col = ToonShade(albedo, normalize(IN.normalWS), IN.positionWS, GetNormalizedScreenSpaceUV(IN.positionCS), _ToonParams, _ToonLevels);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback "Universal Render Pipeline/Unlit"
}
