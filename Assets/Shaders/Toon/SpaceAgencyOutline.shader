// Flat-colour back-face shader. Draw an inflated copy of a mesh with this and only the rim shows: an outline.
// Written like the other Toon shaders (SRP Batcher + DOTS instancing) so it works with the GPU Resident Drawer.
Shader "SpaceAgency/Toon/Outline"
{
    Properties
    {
        _OutlineColor ("Outline colour", Color) = (0.3, 0.85, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            static float4 unity_DOTS_Sampled_OutlineColor;
            void SetupDOTSOutlineMaterialPropertyCaches() { unity_DOTS_Sampled_OutlineColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OutlineColor); }
            #undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
            #define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSOutlineMaterialPropertyCaches()
            #define _OutlineColor unity_DOTS_Sampled_OutlineColor
            #endif

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
