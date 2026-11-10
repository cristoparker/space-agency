// Additive, unlit, HDR light trail (vehicle tail lights at high game speed). Vertex colour x _Intensity, so it blooms.
Shader "SpaceAgency/Trail"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 12)) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            float _Intensity;
            V vert(A a) { V v; v.pos = TransformObjectToHClip(a.pos.xyz); v.col = a.col; v.uv = a.uv; return v; }
            half4 frag(V v) : SV_Target
            {
                // bright core across the width, soft edges
                float across = 1.0 - abs(v.uv.y * 2.0 - 1.0);
                float soft = smoothstep(0.0, 1.0, across);
                float core = pow(across, 3.0);
                half3 c = v.col.rgb * (soft * 0.6 + core * 1.4) * _Intensity;
                return half4(c, v.col.a * soft);
            }
            ENDHLSL
        }
    }
}
