using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Procedural logo shapes (white, tinted at draw time) and a helper that draws a layered logo with UI Images.</summary>
public static class LogoArt
{
    public static readonly string[] ShapeNames = { "Circle", "Ring", "Square", "Triangle", "Diamond", "Star", "Hexagon", "Bar" };
    public static readonly Color[] Palette =
    {
        new Color(1f, 1f, 1f), new Color(0.97f, 0.35f, 0.4f), new Color(1f, 0.62f, 0.14f), new Color(1f, 0.9f, 0.3f),
        new Color(0.14f, 0.82f, 0.42f), new Color(0.3f, 0.76f, 0.88f), new Color(0.27f, 0.45f, 0.95f), new Color(0.72f, 0.48f, 1f),
        new Color(0.12f, 0.16f, 0.26f), new Color(0.6f, 0.65f, 0.72f),
    };

    static Sprite[] cache;
    const int N = 64;

    public static Sprite Shape(int i)
    {
        if (cache == null) cache = new Sprite[ShapeNames.Length];
        i = Mathf.Clamp(i, 0, ShapeNames.Length - 1);
        if (cache[i] != null) return cache[i];
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // sample 3x3 for soft edges
                float a = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 3f) / N * 2f - 1f, v = (y + (sy + 0.5f) / 3f) / N * 2f - 1f;
                        if (Inside(i, u, v)) a += 1f / 9f;
                    }
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px); tex.Apply();
        cache[i] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return cache[i];
    }

    static bool Inside(int shape, float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
        switch (shape)
        {
            case 0: return r <= 0.95f;
            case 1: return r <= 0.95f && r >= 0.62f;
            case 2: return Mathf.Abs(u) <= 0.85f && Mathf.Abs(v) <= 0.85f;
            case 3: return v >= -0.8f && v <= 0.9f && Mathf.Abs(u) <= (0.9f - v) * 0.56f;
            case 4: return Mathf.Abs(u) + Mathf.Abs(v) <= 0.95f;
            case 5:
                {
                    float k = 0.5f + 0.5f * Mathf.Cos(5f * (ang - Mathf.PI / 2f));
                    return r <= 0.5f + 0.45f * Mathf.Pow(k, 1.6f);
                }
            case 6:
                {
                    float a2 = Mathf.Abs(ang) % (Mathf.PI / 3f);
                    return r * Mathf.Cos(a2 - Mathf.PI / 6f) <= 0.85f * Mathf.Cos(Mathf.PI / 6f);
                }
            default: return Mathf.Abs(u) <= 0.95f && Mathf.Abs(v) <= 0.22f;
        }
    }

    /// <summary>Fills `parent` with one Image per logo layer (stretched to the parent, drawn bottom to top).</summary>
    public static void Draw(Transform parent, LogoDesign logo, float box)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) { var c = parent.GetChild(i).gameObject; c.SetActive(false); Object.Destroy(c); }
        foreach (var l in logo.layers)
        {
            var go = new GameObject("Layer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float s = box * (0.25f + 0.1875f * Mathf.Clamp(l.size, 1, 4));
            rt.sizeDelta = new Vector2(s, s); rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = Shape(l.shape); img.color = Palette[Mathf.Clamp(l.color, 0, Palette.Length - 1)]; img.raycastTarget = false;
        }
    }
}
