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
    [Header("Compact look")]
    public float fontSize = 5.4f;
    [Tooltip("Longer lines wrap at this width (canvas units).")]
    public float maxWidth = 92f;
    public float padX = 4f, padY = 2.5f;

    RectTransform rect;
    CanvasGroup group;
    Canvas canvas;

    void Awake()
    {
        Instance = this;
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        var bg = GetComponent<Image>();
        if (bg != null && bg.sprite != null && bg.type == Image.Type.Sliced) UIRound.Attach(bg, 7f);        // the same gentle corner as the rest of the UI
        if (bg != null && UIKit.T != null && UIKit.T.flat != null)
        {
            // a solid card, not see-through glass: the glass material shows the game behind the interface, which looked like a hole in the UI
            bg.material = UIKit.T.flat; bg.color = UIKit.TooltipColor;
            UIShadow.Attach(rect);
        }
        canvas = GetComponentInParent<Canvas>();
        // small and tidy: tiny text, tight padding, wraps long lines
        if (label != null) { label.fontSize = fontSize; label.enableAutoSizing = false; label.lineSpacing = -6f; label.richText = true; }
        var lg = GetComponent<HorizontalLayoutGroup>();
        if (lg != null) lg.padding = new RectOffset(Mathf.RoundToInt(padX), Mathf.RoundToInt(padX), Mathf.RoundToInt(padY), Mathf.RoundToInt(padY));
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
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = text;
        var le = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
        if (label.GetPreferredValues(text).x > maxWidth) { label.textWrappingMode = TextWrappingModes.Normal; le.preferredWidth = maxWidth; }
        else le.preferredWidth = -1f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        var root = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        var rc = new Vector3[4];
        if (root != null) root.GetWorldCorners(rc); else { rc[0] = Vector3.zero; rc[2] = new Vector3(Screen.width, Screen.height, 0f); }
        float scale = canvas != null ? canvas.rootCanvas.transform.lossyScale.x : 1f;
        // always BELOW the cursor (its top-left corner just under the pointer tip), so the tooltip never hides what is being clicked
        Vector3 anchor = target.position;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && root != null)
            RectTransformUtility.ScreenPointToWorldPointInRectangle(root, mouse.position.ReadValue(), canvas.rootCanvas.worldCamera, out anchor);
        rect.pivot = new Vector2(0f, 1f);
        float w = rect.rect.width * scale, h = rect.rect.height * scale;
        float x = Mathf.Clamp(anchor.x + 4f * scale, rc[0].x + 2f * scale, rc[2].x - w - 2f * scale);
        float y = anchor.y - 12f * scale;                                              // 12 units: clear of the pointer image
        if (y - h < rc[0].y + 2f * scale) { y = rc[0].y + h + 2f * scale; x = Mathf.Clamp(anchor.x + 14f * scale, rc[0].x + 2f * scale, rc[2].x - w - 2f * scale); }   // no room below: beside the cursor, still not over it
        rect.position = new Vector3(x, y, target.position.z);

        transform.SetAsLastSibling();
        group.alpha = 1f;
        group.blocksRaycasts = false;
    }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
    }
}
