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
    public Color dayColor = new Color32(0xFF, 0xF4, 0xE0, 255);
    public Color horizonColor = new Color32(0xFF, 0x8A, 0x3D, 255);
    [Header("Ambient (blended between night and day)")]
    public Color daySky = new Color32(0x7A, 0x98, 0xBF, 255);
    public Color dayEquator = new Color32(0x8A, 0x92, 0x9A, 255);
    public Color dayGround = new Color32(0x4A, 0x4A, 0x4A, 255);
    public Color nightSky = new Color32(0x28, 0x34, 0x5C, 255);
    public Color nightEquator = new Color32(0x20, 0x2A, 0x48, 255);
    public Color nightGround = new Color32(0x14, 0x18, 0x24, 255);

    [Header("Sky")]
    [Tooltip("Procedural sky (SpaceAgency/Sky/Procedural): gradient, sun, stars and drifting clouds.")]
    public Material skyMaterial;
    [Range(0f, 1f)] public float cloudCoverage = 0.62f;
    [Tooltip("Colour the ambient light takes on at sunrise and sunset.")]
    public Color twilightAmbient = new Color32(0xFF, 0x9A, 0x6A, 255);

    [Tooltip("Used in the editor / when no GameClock exists. 0..1 of the day.")]
    [Range(0f, 1f)] public float previewDayProgress = 0.5f;

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
        sun.transform.rotation = Quaternion.Euler(angle, sunYaw, 0f);

        float elevation = Mathf.Sin((dayProgress - 0.25f) * Mathf.PI * 2f);
        float day = Mathf.Clamp01(elevation * 3f);
        sun.intensity = maxIntensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elevation + 0.08f) * 5f));   // gentle light even at sunrise and sunset
        sun.color = Color.Lerp(horizonColor, dayColor, Mathf.SmoothStep(0f, 1f, day));
        sun.shadows = day > 0.01f ? shadowType : LightShadows.None;
        sun.shadowStrength = shadowStrength;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightSky, daySky, day);
        // warm the ambient light around sunrise and sunset
        float twilight = Mathf.Clamp01(1f - Mathf.Abs(elevation) * 4f);
        Color equator = Color.Lerp(nightEquator, dayEquator, day);
        RenderSettings.ambientEquatorColor = Color.Lerp(equator, twilightAmbient * 0.55f, twilight * 0.5f);
        RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, day);

        // feed the sky shader: where the sun is and how far the wind has blown the clouds
        Vector3 toSun = -sun.transform.forward;
        Shader.SetGlobalVector("_SkySunDir", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
        Shader.SetGlobalFloat("_SkyTime", Application.isPlaying ? Time.time : (float)UnityEngine.Time.realtimeSinceStartupAsDouble);
        if (skyMaterial != null) skyMaterial.SetFloat("_CloudCoverage", cloudCoverage);

    }
}
