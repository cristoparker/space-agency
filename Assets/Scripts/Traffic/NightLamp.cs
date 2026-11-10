using UnityEngine;

/// <summary>
/// A lamp that only shines at night: swaps the lamp-glass material (sub-mesh slot) between on/off
/// and fades its Light(s). Night follows the same sun curve as DayNightCycle.
/// </summary>
public class NightLamp : MonoBehaviour
{
    public Renderer lampRenderer;
    [Tooltip("Material slot of the lamp glass (slot 1 in the Blender models).")]
    public int lampSlot = 1;
    public Material onMaterial;
    public Material offMaterial;
    public Light[] lights;
    [Tooltip("Light intensity while the lamp is on.")]
    public float onIntensity = 8f;
    public float fadeSeconds = 1.5f;
    [Tooltip("Sun elevation (-1..1) below which the lamps turn on. 0 = sunset / sunrise.")]
    public float turnOnBelowSunElevation = 0.12f;

    bool isOn;
    float scale = 1f;
    float level;                       // 0..1 light fade

    public static bool IsNight(float turnOnBelow)
    {
        if (!Application.isPlaying || GameClock.Instance == null) return false;
        float elevation = Mathf.Sin((GameClock.Instance.DayProgress - 0.25f) * Mathf.PI * 2f);
        return elevation < turnOnBelow;
    }

    void Start()
    {
        foreach (var l in lights) if (l != null) scale = Mathf.Max(scale, LightKit.SoftenLamp(l));
        Apply(false, true);
    }

    void Update()
    {
        bool night = IsNight(turnOnBelowSunElevation);
        if (night != isOn) Apply(night, false);
        float target = isOn ? 1f : 0f;
        // fade in real time so it still looks smooth when the game runs fast
        level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        foreach (var l in lights)
        {
            if (l == null) continue;
            l.intensity = onIntensity * scale * level;
            l.enabled = level > 0.001f;
        }
    }

    void Apply(bool on, bool first)
    {
        isOn = on;
        if (first) level = on ? 1f : 0f;
        if (lampRenderer == null) return;
        var mats = lampRenderer.sharedMaterials;
        if (lampSlot < mats.Length) mats[lampSlot] = on ? onMaterial : offMaterial;
        lampRenderer.sharedMaterials = mats;
    }
}
