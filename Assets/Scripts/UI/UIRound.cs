using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the corners of a rounded (sliced) image gently curved on every size. The glass sprite has a fixed corner, which on a small
/// or flat element is squeezed into a stretched, trapezium-like shape. This sets the sprite scale from the element's real size so the
/// corner is always a true circle arc: a capsule for controls, a soft rounded rectangle for cards, a larger radius for panels.
/// </summary>
[RequireComponent(typeof(Image))]
public class UIRound : MonoBehaviour
{
    /// <summary>Corner radius in canvas units. 0 or less = automatic from the height (capsule for controls, soft corners for cards).</summary>
    public float radius = 0f;

    Image img;
    RectTransform rt;
    Vector2 last;

    public static void Attach(Image image, float radius = 0f)
    {
        if (image == null) return;
        var u = image.GetComponent<UIRound>() ?? image.gameObject.AddComponent<UIRound>();
        u.radius = radius;
        u.Apply();
    }

    /// <summary>Recomputes the corner now (even if the size did not change), e.g. after the radius was changed.</summary>
    public void Force() { last = Vector2.zero; Apply(); }

    void Awake() { img = GetComponent<Image>(); rt = (RectTransform)transform; }
    void OnEnable() { Apply(); }
    void OnRectTransformDimensionsChange() { Apply(); }

    static float AutoRadius(float h) => h <= 22f ? h * 0.5f : h <= 60f ? 8f : 10f;

    public void Apply()
    {
        if (img == null) img = GetComponent<Image>();
        if (rt == null) rt = (RectTransform)transform;
        if (img == null || img.sprite == null || img.type != Image.Type.Sliced) return;
        var rect = rt.rect;
        float m = Mathf.Min(rect.width, rect.height);
        if (m < 0.5f) return;
        var size = new Vector2(rect.width, rect.height);
        if ((size - last).sqrMagnitude < 0.0001f && img.pixelsPerUnitMultiplier != 1f) return;
        last = size;

        float want = radius > 0f ? radius : AutoRadius(rect.height);
        float r = Mathf.Clamp(Mathf.Min(want, m * 0.5f), 1.2f, 40f);                    // never more than half of the shorter side
        float refPpu = img.canvas != null ? img.canvas.referencePixelsPerUnit : 16f;
        float baseCorner = img.sprite.border.x / (img.sprite.pixelsPerUnit / refPpu);    // corner size at multiplier 1
        float mult = Mathf.Clamp(baseCorner / r, 0.3f, 12f);
        if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, mult)) img.pixelsPerUnitMultiplier = mult;
    }
}
