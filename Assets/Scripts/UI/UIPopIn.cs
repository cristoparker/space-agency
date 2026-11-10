using UnityEngine;

/// <summary>Pop-up menus ease in when they open: they fade, grow from 92% and rise a few units into place.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPopIn : MonoBehaviour
{
    public float duration = 0.22f, rise = 5f;
    CanvasGroup group;
    RectTransform rt;
    Vector2 home;
    bool homeSet;
    float t;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rt = (RectTransform)transform;
    }

    void OnEnable()
    {
        if (!homeSet) { home = rt.anchoredPosition; homeSet = true; }
        t = 0f; Step(0f);
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
        Step(t);
    }

    void Step(float k)
    {
        float e = 1f - Mathf.Pow(1f - k, 3f);                                   // ease-out
        float back = 1f + 0.06f * Mathf.Sin(k * Mathf.PI) * (1f - k);            // tiny overshoot
        group.alpha = e;
        rt.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e) * back;
        rt.anchoredPosition = home + new Vector2(0f, (1f - e) * -rise);
    }
}
