// Full-screen backdrop for the main menu, drawn as OPAQUE geometry so the glass UI shader (which blurs URP's Opaque Texture) can see it.
// Two pictures cross-fade (_T), each with its own zoom/drift (xy = scale, zw = offset, in uv space) and a flat dark tint (_Dim).
Shader "SpaceAgency/MenuBackdrop"
{
    Properties
    {
        _A ("Picture A", 2D) = "black" {}
        _B ("Picture B", 2D) = "black" {}
        _TA ("A scale/offset", Vector) = (1,1,0,0)
        _TB ("B scale/offset", Vector) = (1,1,0,0)
        _T ("Cross-fade", Range(0,1)) = 0
        _Dim ("Dark tint", Range(0,1)) = 0.38
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_A); SAMPLER(sampler_A);
            TEXTURE2D(_B); SAMPLER(sampler_B);
            CBUFFER_START(UnityPerMaterial)
                float4 _TA; float4 _TB; float _T; float _Dim;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float2 p = IN.uv * 2.0 - 1.0;                       // the quad always covers the whole screen, wherever it sits
                o.positionCS = float4(p.x, p.y * _ProjectionParams.x, 0.5, 1.0);
                o.uv = IN.uv;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 ua = (IN.uv - 0.5) / _TA.xy + 0.5 + _TA.zw;
                float2 ub = (IN.uv - 0.5) / _TB.xy + 0.5 + _TB.zw;
                half3 a = SAMPLE_TEXTURE2D(_A, sampler_A, ua).rgb;
                half3 b = SAMPLE_TEXTURE2D(_B, sampler_B, ub).rgb;
                return half4(lerp(a, b, _T) * (1.0 - _Dim), 1.0);
            }
            ENDHLSL
        }
    }
}
