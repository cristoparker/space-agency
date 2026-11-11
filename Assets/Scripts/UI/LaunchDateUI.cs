using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>After the rocket is built: pick its launch date from the coming days. Closing it with "Later" keeps the rocket waiting (the objective reopens this).</summary>
public static class LaunchDateUI
{
    static GameObject current;
    static int prevSpeed = -1;
    public static bool IsOpen => current != null;
    public static void ResetStatics() { current = null; prevSpeed = -1; }

    public static void Show()
    {
        var rc = RocketConstruction.Instance; var clock = GameClock.Instance;
        if (rc == null || !rc.WaitingForDate || clock == null) return;
        Close();
        var hud = UnityEngine.Object.FindAnyObjectByType<HudController>();
        if (hud == null) return;
        var block = new GameObject("Date Blocker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        block.SetParent((RectTransform)hud.transform, false);
        UIKit.Fill(block);
        block.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        block.SetAsLastSibling();
        current = block.gameObject;
        if (clock.Speed > 0f) { prevSpeed = clock.SpeedIndex; clock.SetSpeedIndex(0); }

        const float w = 236f, cols = 7f, cell = 28f, gap = 2f;
        int days = 35;
        float h = 78f + Mathf.Ceil(days / cols) * (cell + gap) + 40f;
        var card = UIKit.At(block, "Card", 0, 0, w, h);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition = Vector2.zero;
        UIKit.Panel(card);
        card.gameObject.AddComponent<CanvasGroup>(); card.gameObject.AddComponent<UIPopIn>();
        UIKit.Tint(UIKit.At(card, "Accent", 0, 0, w, 3), UIKit.T.green).raycastTarget = false;
        UIKit.TextAt(card, "Title", 12, 9, w - 24, 13, "Rocket complete", 9.5f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.green).raycastTarget = false;
        UIKit.TextAt(card, "Sub", 12, 23, w - 24, 10, "\"" + rc.Current.rocketName + "\" is ready. Choose the launch date.", 6.4f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim).raycastTarget = false;
        string dl = "";
        var mm = MissionManager.Instance;
        if (mm != null) foreach (var m in mm.Running) if (m.Definition.id == rc.Current.missionId) dl = "Contract deadline in about " + Mathf.Max(0, Mathf.RoundToInt((1f - m.Progress) * (float)m.Definition.DurationDays)) + " days";
        if (dl.Length > 0) UIKit.TextAt(card, "Deadline", 12, 34, w - 24, 9, dl, 6f, TextAlignmentOptions.MidlineLeft, true, new Color(1f, 0.72f, 0.3f, 1f)).raycastTarget = false;

        DateTime today = clock.Now.Date, picked = today.AddDays(1);
        TMP_Text pickedText = UIKit.TextAt(card, "Picked", 12, h - 52f, w - 24, 10, "", 6.8f, TextAlignmentOptions.MidlineLeft, true);
        var cellImgs = new Image[days]; var cellDates = new DateTime[days];
        void Refresh()
        {
            for (int i = 0; i < days; i++) cellImgs[i].color = cellDates[i] == picked ? UIKit.Wash(UIKit.T.green, 0.85f) : new Color(1f, 1f, 1f, 0.12f);
            pickedText.text = "Launch date: " + picked.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture);
        }
        for (int i = 0; i < days; i++)
        {
            int idx = i; var d = today.AddDays(i + 1); cellDates[i] = d;
            float x = 12f + (i % 7) * (cell + gap) + 2f, y = 50f + (i / 7) * (cell + gap);
            var rt = UIKit.At(card, "Day", x, y, cell, cell);
            var b = UIKit.MakeButton(rt, "", new Color(1f, 1f, 1f, 0.12f), () => { picked = cellDates[idx]; Refresh(); });
            cellImgs[i] = (Image)b.targetGraphic;
            UIKit.TextAt(rt, "D", 0, 3, cell, 13, d.Day.ToString(), 8f, TextAlignmentOptions.Center, true).raycastTarget = false;
            UIKit.TextAt(rt, "M", 0, 15, cell, 9, d.ToString("MMM ddd", CultureInfo.InvariantCulture), 4.6f, TextAlignmentOptions.Center, false, UIKit.T.dim).raycastTarget = false;
        }
        Refresh();
        UIKit.Btn(card, "Later", 12, h - 28f, 60, 18, UIKit.T.chip, Close, 7f);
        UIKit.SolidBtn(card, "Confirm launch date", 80, h - 28f, w - 92f, 18, UIKit.T.green, () => { Close(); rc.Finish(picked); }, 7f);
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
