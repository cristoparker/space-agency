// Glassmorphism UI shader for URP.
//
// Glass mode (_Flat = 0): blurs the scene behind the element (URP "Opaque Texture"), tints it and draws a light rim.
//   Needs: Canvas = Screen Space - Camera (or World Space) and "Opaque Texture" enabled in the URP asset.
//   Use it for TOP-LEVEL panels only (a glass element cannot see other UI behind it).
// Flat mode (_Flat = 1): same rounded shape and rim, but a plain translucent fill. Use it for anything inside a panel.
//
// Sprite channels (see ui-glass-round.png): A = outer rounded shape, G = inset shape (rim = A - G).
// Image.color: RGB = tint colour, A = overall opacity (so CanvasGroup fades work).
Shader "SpaceAgency/UI/Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Shape (A outer, G inner)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Toggle] _Flat ("Flat (no backdrop blur)", Float) = 0

        _TintStrength ("Glass Tint Strength", Range(0, 1)) = 0.5
        _BlurRadius ("Blur Radius (px at 360p)", Range(0, 12)) = 4
        _BackdropBrightness ("Backdrop Brightness", Range(0, 2)) = 0.9
        _BackdropSaturation ("Backdrop Saturation", Range(0, 2)) = 1.2
        _FlatFill ("Flat Fill Opacity", Range(0, 1)) = 1
        _BorderColor ("Rim Colour", Color) = (1,1,1,0.35)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Flat;
                float _TintStrength;
                float _BlurRadius;
                float _BackdropBrightness;
                float _BackdropSaturation;
                float _FlatFill;
                float4 _BorderColor;
                float4 _ClipRect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 positionOSRaw : TEXCOORD2;
            };

            float UIClip(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionOSRaw = IN.positionOS;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // 24-tap golden-angle disc blur of the opaque scene texture.
            float3 BlurBackdrop(float2 uv, float radiusPixels)
            {
                float2 px = 1.0 / _ScreenParams.xy;
                float radius = radiusPixels * (_ScreenParams.y / 360.0);
                float3 sum = 0;
                const int taps = 24;
                [unroll]
                for (int i = 0; i < taps; i++)
                {
                    float r = sqrt((i + 0.5) / taps) * radius;
                    float a = i * 2.39996323;
                    float2 o = float2(cos(a), sin(a)) * r * px;
                    sum += SampleSceneColor(saturate(uv + o));
                }
                return sum / taps;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float outer = shape.a;
                float inner = shape.g;
                float rim = saturate(outer - inner);

                float3 tint = IN.color.rgb;
                float3 col;
                float alpha;

                if (_Flat > 0.5)
                {
                    col = lerp(tint, _BorderColor.rgb, rim * _BorderColor.a);
                    alpha = saturate(outer * _FlatFill + rim * _BorderColor.a) * IN.color.a;
                }
                else
                {
                    float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);
                    float3 bg = BlurBackdrop(suv, _BlurRadius);
                    float lum = dot(bg, float3(0.299, 0.587, 0.114));
                    bg = lerp(lum.xxx, bg, _BackdropSaturation) * _BackdropBrightness;
                    col = lerp(bg, tint, _TintStrength);
                    col = lerp(col, _BorderColor.rgb, rim * _BorderColor.a);
                    alpha = outer * IN.color.a;
                }

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UIClip(IN.positionOSRaw.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
