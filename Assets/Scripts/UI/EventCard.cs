using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The event popup: a short text and two or three answers. Every answer lists its benefit, cost and risk. One has to be chosen, so there is no cancel.</summary>
public static class EventCard
{
    public class Row { public string label, note; public string[] benefits, costs, risks; public bool enabled = true; public Action onPick; }

    static GameObject current;
    static int prevSpeed = -1;
    public static bool IsOpen => current != null;
    public static void ResetStatics() { current = null; prevSpeed = -1; }

    static readonly Color Amber = new Color(1f, 0.72f, 0.3f, 1f);

    static float RowHeight(Row r) => 22f + (r.benefits.Length + r.costs.Length + r.risks.Length) * 9f;

    public static void Show(string kicker, string title, string body, bool client, List<Row> rows)
    {
        Close();
        var hud = UnityEngine.Object.FindAnyObjectByType<HudController>();
        if (hud == null) return;
        const float w = 236f;
        float h = 66f;
        foreach (var r in rows) { r.benefits = r.benefits ?? new string[0]; r.costs = r.costs ?? new string[0]; r.risks = r.risks ?? new string[0]; h += RowHeight(r) + 5f; }
        h += 1f;

        var block = new GameObject("Event Blocker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        block.SetParent((RectTransform)hud.transform, false);
        UIKit.Fill(block);
        block.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        block.SetAsLastSibling();
        current = block.gameObject;
        var clock = GameClock.Instance;
        if (clock != null && clock.Speed > 0f) { prevSpeed = clock.SpeedIndex; clock.SetSpeedIndex(0); }

        var card = UIKit.At(block, "Card", 0, 0, w, h);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition = Vector2.zero;
        UIKit.Panel(card);
        card.gameObject.AddComponent<CanvasGroup>(); card.gameObject.AddComponent<UIPopIn>();
        Color accent = client ? UIKit.T.blue : Amber;
        UIKit.Tint(UIKit.At(card, "Accent", 0, 0, w, 3), accent).raycastTarget = false;
        UIKit.IconAt(card, Resources.Load<Sprite>(client ? "UIIcons/icon_comms" : "UIIcons/icon_flag"), 12, 9, 10, 10, accent);
        UIKit.TextAt(card, "Kicker", 26, 9, w - 38, 10, kicker.ToUpperInvariant(), 5.6f, TextAlignmentOptions.MidlineLeft, true, accent).raycastTarget = false;
        UIKit.TextAt(card, "Title", 12, 21, w - 24, 13, title, 9f, TextAlignmentOptions.MidlineLeft, true).raycastTarget = false;
        var bt = UIKit.TextAt(card, "Body", 12, 36, w - 24, 26, body, 6.4f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
        bt.textWrappingMode = TextWrappingModes.Normal; bt.lineSpacing = -4f; bt.raycastTarget = false;

        float y = 66f;
        foreach (var r in rows)
        {
            var row = r;
            float rh = RowHeight(row);
            var box = UIKit.At(card, "Choice", 10, y, w - 20, rh);
            UIKit.Tint(box, UIKit.CardColor);
            var btn = UIKit.At(box, "Pick", w - 20 - 58, 5, 52, 14);
            var b = UIKit.Btn(btn, row.enabled ? "Choose" : "-", 0, 0, 52, 14, row.enabled ? UIKit.Soft(UIKit.T.green) : UIKit.T.chip, () => { Close(); row.onPick?.Invoke(); }, 6.4f, true, row.enabled ? UIKit.SoftInk(UIKit.T.green) : UIKit.T.dim);
            b.interactable = row.enabled;
            UIKit.TextAt(box, "Label", 8, 4, w - 20 - 70, 14, row.label, 7.2f, TextAlignmentOptions.MidlineLeft, true).raycastTarget = false;
            float ly = 20f;
            foreach (var t in row.benefits) { LaunchResultUI.Chip(box, 8, ly, 0, 7f); Line(box, ly, t, UIKit.T.ink, w); ly += 9f; }
            foreach (var t in row.costs) { LaunchResultUI.Chip(box, 8, ly, 1, 7f); Line(box, ly, t, UIKit.T.ink, w); ly += 9f; }
            foreach (var t in row.risks) { LaunchResultUI.Chip(box, 8, ly, 2, 7f); Line(box, ly, t, UIKit.T.ink, w); ly += 9f; }
            if (!string.IsNullOrEmpty(row.note)) UIKit.TextAt(box, "Note", w - 20 - 78, 18, 72, 8, row.note, 5.2f, TextAlignmentOptions.MidlineRight, false, UIKit.T.red).raycastTarget = false;
            y += rh + 5f;
        }
    }

    static void Line(RectTransform box, float y, string text, Color col, float w)
    {
        var t = UIKit.TextAt(box, "L", 19, y - 0.5f, w - 20 - 28, 9, text, 5.8f, TextAlignmentOptions.MidlineLeft, false, col);
        t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
    }

    public static void Close()
    {
        if (current != null) UnityEngine.Object.Destroy(current);
        current = null;
        var clock = GameClock.Instance;
        if (prevSpeed > 0 && clock != null) clock.SetSpeedIndex(prevSpeed);
        prevSpeed = -1;
    }
}
