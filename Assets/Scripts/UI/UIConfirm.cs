using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A small modal question ("Build the Hangar on P3?") with Confirm and Cancel. Clicking outside it cancels.</summary>
public static class UIConfirm
{
    static GameObject current;

    public static void ResetStatics() { current = null; }

    public static void Show(string title, string body, string confirmLabel, Action onConfirm, Action onCancel = null)
    {
        Close();
        var hud = UnityEngine.Object.FindAnyObjectByType<HudController>();
        if (hud == null) { onConfirm?.Invoke(); return; }
        var canvas = (RectTransform)hud.transform;

        var block = new GameObject("Confirm Blocker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        block.SetParent(canvas, false);
        UIKit.Fill(block);
        var bi = block.GetComponent<Image>(); bi.color = new Color(0f, 0f, 0f, 0.38f);
        var outside = block.gameObject.AddComponent<Button>(); outside.transition = Selectable.Transition.None; outside.targetGraphic = bi;
        outside.onClick.AddListener(() => { Close(); onCancel?.Invoke(); });
        block.SetAsLastSibling();
        current = block.gameObject;

        const float w = 190f;
        var card = UIKit.At(block, "Card", 0, 0, w, 100);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition = Vector2.zero;
        UIKit.Panel(card);
        card.gameObject.AddComponent<UIPopIn>();
        card.gameObject.AddComponent<CanvasGroup>();

        UIKit.TextAt(card, "Title", 10, 8, w - 20, 14, title, 9f, TextAlignmentOptions.MidlineLeft, true);
        var t = UIKit.TextAt(card, "Body", 10, 25, w - 20, 40, body, 6.6f, TextAlignmentOptions.TopLeft);
        t.textWrappingMode = TextWrappingModes.Normal; t.lineSpacing = -4f; t.richText = true;
        float bh = Mathf.Min(70f, Mathf.Max(24f, t.GetPreferredValues(body, w - 20, 100f).y + 2f));
        ((RectTransform)t.transform.parent).sizeDelta = new Vector2(w - 20, bh);
        float h = 25 + bh + 8 + 16 + 8;
        card.sizeDelta = new Vector2(w, h);

        float by = h - 24f;
        UIKit.Btn(card, "Cancel", 10, by, (w - 28f) * 0.42f, 16, UIKit.T.chip, () => { Close(); onCancel?.Invoke(); }, 6.8f);
        UIKit.Btn(card, confirmLabel, 10 + (w - 28f) * 0.42f + 8, by, (w - 28f) * 0.58f, 16, UIKit.T.green, () => { Close(); onConfirm?.Invoke(); }, 6.8f);
    }

    public static void Close()
    {
        if (current != null) UnityEngine.Object.Destroy(current);
        current = null;
    }

    public static bool IsOpen => current != null;
}
