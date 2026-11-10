#ifndef SPACEAGENCY_TOON_COMMON
#define SPACEAGENCY_TOON_COMMON

// Distance haze shared by all toon shaders (DayNightCycle sets the globals: colour follows the sky's horizon, x = start, y = end in metres)
float _SpaceNight;                                   // 0 = day, 1 = night (DayNightCycle)
float4 _SpaceFogColor;
float4 _SpaceFogParams;
half3 SpaceFog(half3 col, float3 positionWS)
{
    float viewZ = -TransformWorldToView(positionWS).z;
    float f = saturate((viewZ - _SpaceFogParams.x) / max(1.0, _SpaceFogParams.y - _SpaceFogParams.x));
    return lerp(col, _SpaceFogColor.rgb, saturate(f * _SpaceFogParams.z));
}

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

    // Point / spot lights (lamps, headlights): smooth, so they read as real light rather than a round flashlight disc.
#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = screenUV;
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light l = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));       // includes the light cookie (soft pools, dipped beams)
        float ndl = saturate(dot(normalWS, l.direction));
        half3 e = l.color * (l.distanceAttenuation * ndl);                              // smooth, physically falling off
        lit += 1.0h - exp(-1.5h * e);                                                   // soft shoulder instead of hard bands: no visible rim
    LIGHT_LOOP_END
#endif
    return SpaceFog(albedo * lit, positionWS);
}

#endif

// Lit windows at night: the window cell of the shared palette (row 0, column 4) glows, a few windows stay dark, each in a slightly different warm / cool white.
half3 WindowGlow(float2 uv, float3 positionWS, float4 baseMapST)
{
    if (_SpaceNight < 0.01 || abs(baseMapST.z) + abs(baseMapST.w) > 0.0001) return 0;      // materials that shift the palette (cars) are skipped
    float isWin = step(uv.y, 0.125) * step(0.5, uv.x) * step(uv.x, 0.625);
    if (isWin < 0.5) return 0;
    float2 cell = floor(positionWS.xz * 0.37 + positionWS.y * 0.55);
    float h = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
    float lit = step(0.2, h);
    half3 col = lerp(half3(1.0, 0.72, 0.36), half3(0.85, 0.93, 1.0), step(0.78, frac(h * 7.31)));
    return col * (lit * _SpaceNight * (2.0 + 0.8 * frac(h * 3.7)));
}
