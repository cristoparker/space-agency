using UnityEngine;

/// <summary>
/// Drives the scene's directional light (the sun) from GameClock, so the day/night cycle
/// follows the in-game time and fast-forward. 06:00 sunrise, 12:00 noon, 18:00 sunset.
/// </summary>
[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    [Tooltip("Compass direction the sun travels along (yaw of the light).")]
    public float sunYaw = -30f;
    public float maxIntensity = 1.1f;
    [Tooltip("Hard shadows keep the crisp pixel look. Soft blurs the edges.")]
    public LightShadows shadowType = LightShadows.Hard;
    [Range(0f, 1f)] public float shadowStrength = 0.85f;
    [Tooltip("Highest the sun gets at noon (degrees). Lower = more light from the side.")]
    [Range(20f, 85f)] public float sunMaxElevation = 58f;
    [Tooltip("Turns the sun's path around the map: noon sun comes from this compass side (0 = south, 90 = west).")]
    [Range(-180f, 180f)] public float sunAzimuth = 90f;
    public Color dayColor = new Color32(0xFF, 0xF4, 0xE0, 255);
    public Color horizonColor = new Color32(0xFF, 0x8A, 0x3D, 255);
    [Header("Ambient (blended between night and day)")]
    public Color daySky = new Color32(0x7A, 0x98, 0xBF, 255);
    public Color dayEquator = new Color32(0x8A, 0x92, 0x9A, 255);
    public Color dayGround = new Color32(0x4A, 0x4A, 0x4A, 255);
    public Color nightSky = new Color32(0x3A, 0x4A, 0x7E, 255);
    public Color nightEquator = new Color32(0x30, 0x3E, 0x6A, 255);
    public Color nightGround = new Color32(0x1E, 0x26, 0x3C, 255);
    [Header("Moonlight")]
    public Color moonColor = new Color32(0x7C, 0x96, 0xFF, 255);
    [Range(0f, 1f)] public float moonIntensity = 0.35f;
    Light moon;

    [Header("Sky")]
    [Tooltip("Procedural sky (SpaceAgency/Sky/Procedural): gradient, sun, stars and drifting clouds.")]
    public Material skyMaterial;
    [Range(0f, 1f)] public float cloudCoverage = 0.62f;
    [Header("Distance haze (hides the far mountains and the long highway ends)")]
    public float fogStart = 800f, fogEnd = 1550f;
    [Tooltip("Colour the ambient light takes on at sunrise and sunset.")]
    public Color twilightAmbient = new Color32(0xFF, 0x9A, 0x6A, 255);

    [Tooltip("Used in the editor / when no GameClock exists. 0..1 of the day.")]
    [Range(0f, 1f)] public float previewDayProgress = 0.5f;

    /// <summary>Layer menu switch: turns the distance haze off.</summary>
    public static bool HazeEnabled = true;

    void OnEnable()
    {
        if (sun == null) sun = FindAnyObjectByType<Light>();
        if (sun != null) RenderSettings.sun = sun;
        if (skyMaterial != null) RenderSettings.skybox = skyMaterial;
    }

    void Update()
    {
        if (sun == null) return;
        float t = (Application.isPlaying && GameClock.Instance != null) ? GameClock.Instance.DayProgress : previewDayProgress;
        Apply(t);
    }

    void Apply(float dayProgress)
    {
        float angle = dayProgress * 360f - 90f;
        // the sun climbs only to sunMaxElevation at noon (not overhead) and its path is turned to sunAzimuth, so the light falls on the
        // side of the buildings and shadows stretch across the ground instead of lying under them
        float H = (dayProgress - 0.5f) * Mathf.PI * 2f;
        float lat = (90f - sunMaxElevation) * Mathf.Deg2Rad;
        Vector3 toSunDir = new Vector3(-Mathf.Sin(H), Mathf.Cos(lat) * Mathf.Cos(H), -Mathf.Sin(lat) * Mathf.Cos(H));
        toSunDir = Quaternion.Euler(0f, sunAzimuth, 0f) * toSunDir;
        sun.transform.rotation = Quaternion.LookRotation(-toSunDir.normalized);

        float elevation = toSunDir.y;
        float day = Mathf.Clamp01(elevation * 3f);
        sun.intensity = maxIntensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elevation + 0.08f) * 5f));   // gentle light even at sunrise and sunset
        sun.color = Color.Lerp(horizonColor, dayColor, Mathf.SmoothStep(0f, 1f, day));
        sun.shadows = day > 0.01f ? LightShadows.Soft : LightShadows.None;
        sun.shadowStrength = shadowStrength;

        // moon: a soft blue light from the opposite side of the sky, only when the sun is down
        if (moon == null)
        {
            var mg = GameObject.Find("Moon Light");
            if (mg == null) { mg = new GameObject("Moon Light"); mg.hideFlags = HideFlags.DontSave; }
            moon = mg.GetComponent<Light>();
            if (moon == null) moon = mg.AddComponent<Light>();
            moon.type = LightType.Directional; moon.shadows = LightShadows.None;
        }
        float night = Mathf.Clamp01(-elevation * 4f);
        moon.color = moonColor; moon.intensity = moonIntensity * night;
        moon.transform.rotation = Quaternion.Euler(Mathf.Clamp(-angle + 180f, 20f, 70f), sunYaw + 160f, 0f);
        moon.enabled = night > 0.01f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightSky, daySky, day);
        // warm the ambient light around sunrise and sunset
        float twilight = Mathf.Clamp01(1f - Mathf.Abs(elevation) * 4f);
        Color equator = Color.Lerp(nightEquator, dayEquator, day);
        RenderSettings.ambientEquatorColor = Color.Lerp(equator, twilightAmbient * 0.55f, twilight * 0.5f);
        RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, day);

        // feed the sky shader: where the sun is and how far the wind has blown the clouds
        Vector3 toSun = -sun.transform.forward;
        Shader.SetGlobalFloat("_SpaceNight", 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elevation + 0.02f) / 0.2f)));   // windows glow when it is dark
        Shader.SetGlobalVector("_SkySunDir", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
        Shader.SetGlobalFloat("_SkyTime", Application.isPlaying ? Time.time : (float)UnityEngine.Time.realtimeSinceStartupAsDouble);
        if (skyMaterial != null) skyMaterial.SetFloat("_CloudCoverage", cloudCoverage);

        // haze: exactly the sky shader's horizon colour for this sun height (same keyframes and blending, in linear space)
        float sh = toSun.y;
        Color fcl = SkyHorizon(sh);
        Color fc = QualitySettings.activeColorSpace == ColorSpace.Linear ? fcl.gamma : fcl;
        RenderSettings.fog = HazeEnabled; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd; RenderSettings.fogColor = fc;
        Shader.SetGlobalColor("_SpaceFogColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? fcl : fc);
        Shader.SetGlobalVector("_SpaceFogParams", new Vector4(fogStart, fogEnd, HazeEnabled ? 1f : 0f, 0f));

    }

    // mirrors SpaceAgencySky.shader: hor = Key4(sunH, night, twilight, golden hour, day), colours authored in sRGB and blended in linear
    static Color SkyHorizon(float h)
    {
        Color Lin(float r, float g, float b) => new Color(Mathf.Pow(r, 2.2f), Mathf.Pow(g, 2.2f), Mathf.Pow(b, 2.2f));
        Color n = Lin(0.04f, 0.06f, 0.14f), t = Lin(1f, 0.45f, 0.2f), gold = Lin(1f, 0.74f, 0.52f), d = Lin(0.66f, 0.82f, 0.97f);
        float w1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.18f, 0.02f, h));
        float w2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.24f, h));
        float w3 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.65f, h));
        Color c = Color.Lerp(n, t, w1);
        c = Color.Lerp(c, gold, w2);
        c = Color.Lerp(c, d, w3);
        c.a = 1f;
        return c;
    }
}
