using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// macOS-style hover feedback for any UI element: a soft glass glow that stays inside the element's own box, plus a very small, limited
/// scale (never more than 2.5 %), so nothing ever pushes out of its content box. It presses in a hair while clicked. Added automatically to every UIKit button.
/// </summary>
public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.015f, pressScale = 0.985f;
    public float glowAlpha = 0.20f;
    [Tooltip("Extra size of the glow around the element, in canvas units (0 = exactly the element's box).")]
    public float glowSpread = 0f;
    const float MaxHoverScale = 1.025f;

    Image glow;
    float scale = 1f, velocity, glowLevel;
    bool over, down;
    Vector3 baseScale = Vector3.one;

    /// <summary>Adds hover feedback; if the element has nothing that can be hit, an invisible hit area is added.</summary>
    public static UIHover Attach(GameObject go, float hoverScale = 1.015f, float glowAlpha = 0.20f)
    {
        var h = go.GetComponent<UIHover>();
        if (h == null) h = go.AddComponent<UIHover>();
        h.hoverScale = Mathf.Min(hoverScale, MaxHoverScale); h.glowAlpha = Mathf.Max(glowAlpha, 0.16f);
        if (go.GetComponent<Graphic>() == null)
        {
            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); hit.raycastTarget = true;
        }
        return h;
    }

    void Awake()
    {
        baseScale = transform.localScale;
        var t = UIKit.T;
        var g = new GameObject("Hover Glow", typeof(RectTransform)).GetComponent<RectTransform>();
        g.SetParent(transform, false);
        g.SetAsFirstSibling();
        g.anchorMin = Vector2.zero; g.anchorMax = Vector2.one;
        g.offsetMin = new Vector2(-glowSpread, -glowSpread); g.offsetMax = new Vector2(glowSpread, glowSpread);
        glow = g.gameObject.AddComponent<Image>();
        if (t != null && t.round != null) { glow.sprite = t.round; glow.type = Image.Type.Sliced; glow.pixelsPerUnitMultiplier = 1f; }
        glow.color = new Color(1f, 1f, 1f, 0f); glow.raycastTarget = false;
        UIRound.Attach(glow);
        if (t != null && t.flat != null) glow.material = t.flat;
        var le = g.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;     // never take part in a layout group
    }

    void OnEnable() { over = down = false; scale = 1f; velocity = 0f; glowLevel = 0f; Apply(); }

    public void OnPointerEnter(PointerEventData e) => over = true;
    public void OnPointerExit(PointerEventData e) { over = false; down = false; }
    public void OnPointerDown(PointerEventData e) => down = true;
    public void OnPointerUp(PointerEventData e) => down = false;

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
        float target = down ? pressScale : over ? hoverScale : 1f;
        // a well damped spring: it settles at once, no wobble
        velocity += ((target - scale) * 420f - velocity * 34f) * dt;
        scale += velocity * dt;
        glowLevel = Mathf.MoveTowards(glowLevel, over ? 1f : 0f, dt * 7f);
        Apply();
    }

    void Apply()
    {
        transform.localScale = baseScale * scale;
        if (glow != null) glow.color = new Color(1f, 1f, 1f, glowAlpha * glowLevel * (down ? 1.4f : 1f));
    }
}
