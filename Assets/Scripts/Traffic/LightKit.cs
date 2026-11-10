using UnityEngine;

/// <summary>
/// Makes the game's lights look like real lights instead of round flashlight discs: every lamp becomes a wide spot light with a
/// soft gradient cookie (no visible edge), car headlights get a dipped, asymmetric beam that fades out smoothly, and lamp / beam
/// colours use a warm colour temperature. Procedural textures, nothing to import.
/// </summary>
public static class LightKit
{
    static Texture2D pool, beam;

    /// <summary>Soft round pool of light: bright in the middle, falling off smoothly to nothing at the cone edge.</summary>
    public static Texture2D Pool
    {
        get
        {
            if (pool != null) return pool;
            const int n = 128;
            pool = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "LightPool" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt(Mathf.Pow((x + 0.5f) / n * 2f - 1f, 2) + Mathf.Pow((y + 0.5f) / n * 2f - 1f, 2));
                    float f = Mathf.Exp(-r * r * 3.4f) * Mathf.Clamp01(1f - r * r * r * r);   // gaussian glow, no visible rim
                    px[y * n + x] = new Color(f, f, f, f);
                }
            pool.SetPixels(px); pool.Apply(false, true);
            return pool;
        }
    }

    /// <summary>Dipped headlight beam: wide and bright on the road, fading out upwards and to the sides.</summary>
    public static Texture2D Beam
    {
        get
        {
            if (beam != null) return beam;
            const int n = 128;
            beam = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "LightBeam" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;       // v < 0 is the lower half (the road)
                    float r = Mathf.Sqrt(u * u + v * v);
                    float edge = Mathf.Clamp01(1f - r); edge = edge * edge * (3f - 2f * edge);
                    float side = Mathf.Exp(-u * u * 1.6f);
                    float cut = Mathf.Lerp(0.04f, 1f, Mathf.SmoothStep(0.45f, -0.35f, v));   // soft cut-off above the horizon of the beam
                    float hot = 0.65f + 0.55f * Mathf.Exp(-(u * u * 2.5f + (v + 0.3f) * (v + 0.3f) * 3f));
                    float f = Mathf.Clamp01(edge * side * cut * hot);
                    px[y * n + x] = new Color(f, f, f, f);
                }
            beam.SetPixels(px); beam.Apply(false, true);
            return beam;
        }
    }

    /// <summary>Warm sodium-ish street light colour from a temperature, so all lamps share one believable white.</summary>
    public static readonly Color Warm = Mathf.CorrelatedColorTemperatureToRGB(3900f);
    public static readonly Color Headlight = Mathf.CorrelatedColorTemperatureToRGB(5600f);

    /// <summary>
    /// Turns a (point) lamp light into a soft downward cone. Returns the intensity multiplier to use, because the cookie
    /// swallows part of the light.
    /// </summary>
    public static float SoftenLamp(Light l)
    {
        if (l == null) return 1f;
        if (l.name.StartsWith("Accent")) { l.innerSpotAngle = l.spotAngle * 0.2f; l.cookie = Pool; return 1f; }      // coloured accent light: keep its colour and aim
        if (l.type == LightType.Point)
        {
            l.type = LightType.Spot;
            l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            l.spotAngle = 140f; l.innerSpotAngle = 40f;
            l.range *= 1.5f;
            l.cookie = Pool;
            l.color = Warm;
            l.shadows = LightShadows.None;
            return 12f;
        }
        if (l.type == LightType.Spot)                                       // floodlights: keep the aim, lose the hard edge
        {
            l.innerSpotAngle = l.spotAngle * 0.25f; l.cookie = Pool; l.color = Color.Lerp(l.color, Warm, 0.35f);
            return 3f;
        }
        return 1f;
    }
}
