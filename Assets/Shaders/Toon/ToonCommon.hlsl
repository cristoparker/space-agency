#ifndef SPACEAGENCY_TOON_COMMON
#define SPACEAGENCY_TOON_COMMON

// Hard-banded (cel) lighting from the main directional light, with shadows and an ambient floor.
// toonParams: x = low threshold, y = high threshold, z = edge softness, w = ambient strength
// toonLevels: x = darkest band, y = middle band (the brightest band is 1)
half3 ToonShade(half3 albedo, float3 normalWS, float3 positionWS, float4 toonParams, float4 toonLevels)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    float ndl = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
    float soft = max(toonParams.z, 0.0001);
    float b1 = smoothstep(toonParams.x - soft, toonParams.x + soft, ndl);
    float b2 = smoothstep(toonParams.y - soft, toonParams.y + soft, ndl);
    float shade = lerp(lerp(toonLevels.x, toonLevels.y, b1), 1.0, b2);

    half3 ambient = SampleSH(normalWS) * toonParams.w;
    return albedo * (mainLight.color * shade + ambient);
}

#endif
