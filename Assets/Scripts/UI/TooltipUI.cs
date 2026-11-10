using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A single shared tooltip box. Show it next to any RectTransform; hide it again on exit.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    public TMP_Text label;
    [Tooltip("Gap between the target and the tooltip, in canvas units.")]
    public float gap = 3f;

    RectTransform rect;
    CanvasGroup group;
    Canvas canvas;

    void Awake()
    {
        Instance = this;
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Shows the tooltip beside the target without covering it (or the bar it belongs to): below it for things in the upper half of the
    /// screen (top bar), above it for things in the lower half (bottom bar). It is centred on the target and kept inside the screen.
    /// </summary>
    public void Show(string text, RectTransform target)
    {
        if (label == null || target == null) return;
        label.text = text;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        var corners = new Vector3[4];
        target.GetWorldCorners(corners);                                   // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
        var root = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        var rc = new Vector3[4];
        if (root != null) root.GetWorldCorners(rc); else { rc[0] = Vector3.zero; rc[2] = new Vector3(Screen.width, Screen.height, 0f); }
        float scale = canvas != null ? canvas.rootCanvas.transform.lossyScale.x : 1f;

        float midY = (rc[0].y + rc[2].y) * 0.5f;
        bool below = (corners[0].y + corners[1].y) * 0.5f > midY;           // target in the upper half -> tooltip goes below it
        float cx = (corners[0].x + corners[3].x) * 0.5f;
        float g = gap * scale;
        rect.pivot = new Vector2(0.5f, below ? 1f : 0f);
        float w = rect.rect.width * scale, h = rect.rect.height * scale;
        float x = Mathf.Clamp(cx, rc[0].x + w * 0.5f + 2f * scale, rc[2].x - w * 0.5f - 2f * scale);
        float y = below ? corners[0].y - g : corners[1].y + g;
        rect.position = new Vector3(x, y, target.position.z);

        transform.SetAsLastSibling();
        group.alpha = 1f;
    }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
    }
}
