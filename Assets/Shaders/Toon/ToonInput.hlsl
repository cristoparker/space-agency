#ifndef SPACEAGENCY_TOON_INPUT
#define SPACEAGENCY_TOON_INPUT

// Shared material properties for SpaceAgency/Toon/Lit. Every pass includes this so the CBUFFER layout is
// identical (SRP Batcher) and the DOTS metadata exists (GPU Resident Drawer / BatchRendererGroup).
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    float4 _ToonParams;
    float4 _ToonLevels;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _ToonParams)
    UNITY_DOTS_INSTANCED_PROP(float4, _ToonLevels)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_ToonParams;
static float4 unity_DOTS_Sampled_ToonLevels;

void SetupDOTSToonMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_ToonParams = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ToonParams);
    unity_DOTS_Sampled_ToonLevels = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ToonLevels);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSToonMaterialPropertyCaches()

#define _BaseColor  unity_DOTS_Sampled_BaseColor
#define _ToonParams unity_DOTS_Sampled_ToonParams
#define _ToonLevels unity_DOTS_Sampled_ToonLevels
#endif

#endif
