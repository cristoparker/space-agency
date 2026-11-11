using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Smooth mouse-wheel scrolling for a ScrollRect: the wheel sets a target and the view glides to it. Dragging the content or the scrollbar still works
/// at once. The slim scrollbar beside it is brighter while it is being used and calms down when idle.
/// </summary>
public class SmoothScroll : MonoBehaviour, IScrollHandler
{
    public ScrollRect sr;
    public CanvasGroup barGroup;
    const float UnitsPerTick = 16f, Follow = 16f;
    float target = 1f, last = 1f, activeUntil;

    void OnEnable() { if (sr != null) { target = last = sr.verticalNormalizedPosition; } }

    float Scrollable => sr != null && sr.content != null && sr.viewport != null ? Mathf.Max(1f, sr.content.rect.height - sr.viewport.rect.height) : 1f;

    public void OnScroll(PointerEventData e)
    {
        if (sr == null || Scrollable <= 1.01f) return;
        if (Mathf.Abs(sr.verticalNormalizedPosition - last) > 0.002f) target = sr.verticalNormalizedPosition;      // something else moved it: start from there
        target = Mathf.Clamp01(target + Mathf.Clamp(e.scrollDelta.y, -4f, 4f) * UnitsPerTick / Scrollable);
        activeUntil = Time.unscaledTime + 1.2f;
    }

    void Update()
    {
        if (sr == null) return;
        float v = sr.verticalNormalizedPosition;
        if (Mathf.Abs(v - last) > 0.002f) { target = v; activeUntil = Time.unscaledTime + 1.2f; }    // dragged by hand
        if (Mathf.Abs(target - v) > 0.0004f)
        {
            v = Mathf.Lerp(v, target, 1f - Mathf.Exp(-Follow * Time.unscaledDeltaTime));
            sr.verticalNormalizedPosition = v;
        }
        last = sr.verticalNormalizedPosition;
        if (barGroup != null) barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, Time.unscaledTime < activeUntil ? 1f : 0.5f, Time.unscaledDeltaTime * 4f);
    }
}
