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
    public float gap = 4f;

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

    /// <summary>Shows the tooltip to the right of the target, vertically centered on it.</summary>
    public void Show(string text, RectTransform target)
    {
        if (label == null || target == null) return;
        label.text = text;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 rightMiddle = (corners[2] + corners[3]) * 0.5f;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        rect.position = rightMiddle + new Vector3(gap * scale, 0f, 0f);

        transform.SetAsLastSibling();
        group.alpha = 1f;
    }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
    }
}
