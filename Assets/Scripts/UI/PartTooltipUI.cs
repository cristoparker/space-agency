using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>What the part tooltip shows: a title with a category icon, then one line per fact, each with its own universal icon (fuel, mass, push, price...).</summary>
public class PartTip
{
    public string title; public Sprite icon;
    public readonly List<(Sprite icon, string text, Color color)> rows = new List<(Sprite, string, Color)>();
    public string note;                                        // a short extra line, e.g. the engine's special ability
    public void Row(string iconName, string text, Color? color = null) => rows.Add((Resources.Load<Sprite>("UIIcons/" + iconName), text, color ?? UIKit.T.ink));
}

/// <summary>Put on a UI element to show a <see cref="PartTip"/> while the pointer is over it.</summary>
public class PartTipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Func<PartTip> build;
    public void OnPointerEnter(PointerEventData e) { if (build != null) PartTooltipUI.Show(build(), (RectTransform)transform); }
    public void OnPointerExit(PointerEventData e) => PartTooltipUI.Hide();
    void OnDisable() => PartTooltipUI.Hide();
}

/// <summary>A small card with icons instead of paragraphs: easy to read at a glance. Placed beside the element, kept on screen.</summary>
public static class PartTooltipUI
{
    static RectTransform box;
    public static void ResetStatics() { box = null; }

    public static void Show(PartTip tip, RectTransform target)
    {
        if (tip == null || target == null) return;
        var hud = UnityEngine.Object.FindAnyObjectByType<HudController>();
        if (hud == null) return;
        var canvasRt = (RectTransform)hud.transform;
        if (box == null)
        {
            box = new GameObject("Part Tooltip", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            box.SetParent(canvasRt, false);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var bgImg = UIKit.Panel(box, true); bgImg.color = UIKit.TooltipColor;
        }
        for (int i = box.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(box.GetChild(i).gameObject);

        const float w = 112f, rowH = 10f;
        float h = 8f + 14f + tip.rows.Count * rowH + (string.IsNullOrEmpty(tip.note) ? 0f : 16f) + 6f;
        box.sizeDelta = new Vector2(w, h);
        if (tip.icon != null) UIKit.IconAt(box, tip.icon, 7, 7, 11, 11, UIKit.T.ink);
        var t = UIKit.TextAt(box, "Title", tip.icon != null ? 21 : 8, 6, w - 28, 13, tip.title, 7.4f, TextAlignmentOptions.MidlineLeft, true);
        t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
        float y = 22f;
        foreach (var r in tip.rows)
        {
            if (r.icon != null) UIKit.IconAt(box, r.icon, 8, y + 0.5f, 8.5f, 8.5f, r.color);
            var rt = UIKit.TextAt(box, "R", 20, y, w - 26, rowH, r.text, 6.2f, TextAlignmentOptions.MidlineLeft, false, r.color);
            rt.overflowMode = TextOverflowModes.Ellipsis; rt.raycastTarget = false;
            y += rowH;
        }
        if (!string.IsNullOrEmpty(tip.note))
        {
            var n = UIKit.TextAt(box, "Note", 8, y + 1f, w - 16, 14, tip.note, 5.6f, TextAlignmentOptions.TopLeft, false, new Color(0.5f, 0.86f, 1f, 1f));
            n.textWrappingMode = TextWrappingModes.Normal; n.lineSpacing = -6f; n.raycastTarget = false;
        }

        // beside the element: above it when it is in the lower half of the screen, below it otherwise; always inside the screen
        var corners = new Vector3[4]; target.GetWorldCorners(corners);
        var rc = new Vector3[4]; canvasRt.GetWorldCorners(rc);
        float scale = canvasRt.lossyScale.x, g = 3f * scale;
        bool below = (corners[0].y + corners[1].y) * 0.5f > (rc[0].y + rc[2].y) * 0.5f;
        float cx = (corners[0].x + corners[3].x) * 0.5f;
        box.pivot = new Vector2(0.5f, below ? 1f : 0f);
        float x = Mathf.Clamp(cx, rc[0].x + w * 0.5f * scale + 2f * scale, rc[2].x - w * 0.5f * scale - 2f * scale);
        float yy = below ? corners[0].y - g : corners[1].y + g;
        box.position = new Vector3(x, yy, target.position.z);
        box.SetAsLastSibling();
        box.GetComponent<CanvasGroup>().alpha = 1f;
    }

    public static void Hide() { if (box != null) box.GetComponent<CanvasGroup>().alpha = 0f; }
}
