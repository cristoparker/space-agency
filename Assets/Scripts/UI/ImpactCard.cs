using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The consequence card: before a big decision it lists what you gain (benefit), what it costs (cost) and what can go wrong (risk),
/// with an optional chance of success. Confirm runs the decision, Cancel or a click outside does nothing. The game clock is paused while it is open.
/// </summary>
public static class ImpactCard
{
    static GameObject current;
    static int prevSpeed = -1;
    public static bool IsOpen => current != null;
    public static void ResetStatics() { current = null; prevSpeed = -1; }

    static readonly Color Amber = new Color(1f, 0.72f, 0.3f, 1f);

    /// <param name="chance">0..1 chance of success, or a negative number to hide it.</param>
    public static void Show(string title, string[] benefits, string[] costs, string[] risks, float chance, Action onConfirm, Action onCancel = null, string confirmLabel = "Confirm")
    {
        Close();
        var hud = UnityEngine.Object.FindAnyObjectByType<HudController>();
        if (hud == null) { onConfirm?.Invoke(); return; }       // (no interface to show it in)

        benefits = benefits ?? new string[0]; costs = costs ?? new string[0]; risks = risks ?? new string[0];
        const float w = 214f, line = 10f;
        int rows = benefits.Length + costs.Length + risks.Length;
        int sections = (benefits.Length > 0 ? 1 : 0) + (costs.Length > 0 ? 1 : 0) + (risks.Length > 0 ? 1 : 0);
        float h = 38f + (chance >= 0f ? 20f : 0f) + sections * 14f + rows * line + 38f;

        var block = new GameObject("Impact Blocker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        block.SetParent((RectTransform)hud.transform, false);
        UIKit.Fill(block);
        var bi = block.GetComponent<Image>(); bi.color = new Color(0f, 0f, 0f, 0.5f);
        var outside = block.gameObject.AddComponent<Button>(); outside.transition = Selectable.Transition.None; outside.targetGraphic = bi;
        outside.onClick.AddListener(() => { Close(); onCancel?.Invoke(); });
        block.SetAsLastSibling();
        current = block.gameObject;

        var clock = GameClock.Instance;
        if (clock != null && clock.Speed > 0f) { prevSpeed = clock.SpeedIndex; clock.SetSpeedIndex(0); }

        var card = UIKit.At(block, "Card", 0, 0, w, h);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition = Vector2.zero;
        UIKit.Panel(card);
        card.gameObject.AddComponent<CanvasGroup>(); card.gameObject.AddComponent<UIPopIn>();
        var swallow = card.gameObject.AddComponent<Button>(); swallow.transition = Selectable.Transition.None;      // clicks on the card must not reach the outside blocker
        swallow.targetGraphic = card.GetComponent<Image>();

        UIKit.TextAt(card, "Title", 12, 9, w - 24, 14, title, 9.5f, TextAlignmentOptions.MidlineLeft, true).raycastTarget = false;
        float y = 30f;
        if (chance >= 0f)
        {
            Color cc = chance >= 0.75f ? UIKit.T.green : chance >= 0.45f ? Amber : UIKit.T.red;
            UIKit.TextAt(card, "Chance", 12, y, w - 24, 9, "Chance of success  " + Mathf.RoundToInt(chance * 100f) + "%", 6.4f, TextAlignmentOptions.MidlineLeft, true, cc).raycastTarget = false;
            var track = UIKit.At(card, "Track", 12, y + 10f, w - 24, 5); UIKit.Tint(track, UIKit.T.track).raycastTarget = false;
            var fill = UIKit.At(track, "Fill", 0, 0, 10, 5); UIKit.Fill(fill);
            UIKit.Tint(fill, cc).raycastTarget = false;
            fill.anchorMax = new Vector2(Mathf.Clamp01(chance), 1f);
            y += 20f;
        }
        y = Section(card, w, y, "BENEFIT", benefits, 0, UIKit.T.green);
        y = Section(card, w, y, "COST", costs, 1, Amber);
        y = Section(card, w, y, "RISK", risks, 2, UIKit.T.red);

        if (onConfirm == null)                                   // information only: one Close button
        {
            UIKit.Btn(card, "Close", 12, h - 28f, w - 24f, 18, UIKit.T.chip, () => { Close(); onCancel?.Invoke(); }, 7f);
            return;
        }
        float bw = (w - 28f) * 0.42f;
        UIKit.Btn(card, "Cancel", 12, h - 28f, bw, 18, UIKit.T.chip, () => { Close(); onCancel?.Invoke(); }, 7f);
        UIKit.Btn(card, confirmLabel, 12 + bw + 8f, h - 28f, (w - 28f) * 0.58f, 18, UIKit.Wash(UIKit.T.green, 0.75f), () => { Close(); onConfirm?.Invoke(); }, 7f);
    }

    static float Section(RectTransform card, float w, float y, string head, string[] lines, int sev, Color col)
    {
        if (lines.Length == 0) return y;
        UIKit.TextAt(card, head, 12, y, w - 24, 10, head, 5.4f, TextAlignmentOptions.MidlineLeft, true, col).raycastTarget = false;
        y += 13f;
        foreach (var l in lines)
        {
            LaunchResultUI.Chip(card, 12, y + 1f, sev, 8f);
            var t = UIKit.TextAt(card, "L", 24, y, w - 36, 10, l, 6.2f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.ink);
            t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
            y += 10f;
        }
        return y + 1f;
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
