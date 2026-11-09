#ifndef SPACEAGENCY_TOON_COMMON
#define SPACEAGENCY_TOON_COMMON

// Hard-banded (cel) lighting from the main directional light, with shadows and an ambient floor.
// toonParams: x = low threshold, y = high threshold, z = edge softness, w = ambient strength
// screenUV: normalized screen position (needed by Forward+ light clusters)
// toonLevels: x = darkest band, y = middle band (the brightest band is 1)
half3 ToonShade(half3 albedo, float3 normalWS, float3 positionWS, float2 screenUV, float4 toonParams, float4 toonLevels)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    float ndl = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
    float soft = max(toonParams.z, 0.0001);
    float b1 = smoothstep(toonParams.x - soft, toonParams.x + soft, ndl);
    float b2 = smoothstep(toonParams.y - soft, toonParams.y + soft, ndl);
    float shade = lerp(lerp(toonLevels.x, toonLevels.y, b1), 1.0, b2);

    half3 ambient = SampleSH(normalWS) * toonParams.w;
    half3 lit = mainLight.color * shade + ambient;

    // Point / spot lights (lamps at night), banded like the sun.
#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = screenUV;
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light l = GetAdditionalLight(lightIndex, positionWS);
        half lum = dot(l.color, half3(0.3h, 0.59h, 0.11h));
        float x = saturate(dot(normalWS, l.direction)) * l.distanceAttenuation * lum;   // brightness at this surface
        float band = smoothstep(0.05, 0.1, x) * 0.4 + smoothstep(0.3, 0.35, x) * 0.35 + smoothstep(0.8, 0.85, x) * 0.35;
        lit += (l.color / max(lum, 0.0001h)) * band;                                     // tint only; strength is banded
    LIGHT_LOOP_END
#endif
    return albedo * lit;
}

#endif
