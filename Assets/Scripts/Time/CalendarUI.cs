using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The month calendar that drops down from the date in the top bar (Sunday first). Built in code. Today is highlighted,
/// and so is the day a rocket on the pad is scheduled to launch.
/// </summary>
public class CalendarUI : MonoBehaviour
{
    public const float W = 126f;
    TMP_Text monthTitle;
    readonly TMP_Text[] dayText = new TMP_Text[42];
    readonly Image[] dayBg = new Image[42];
    DateTime viewMonth;
    static CalendarUI inst;

    public static CalendarUI Create(RectTransform canvas)
    {
        var rt = new GameObject("Calendar", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(96f, -30f); rt.sizeDelta = new Vector2(W, 124f);
        UIKit.Glass(rt);
        rt.gameObject.SetActive(false);                      // build first, OnEnable runs when the calendar is opened
        var ui = rt.gameObject.AddComponent<CalendarUI>();
        inst = ui;
        ui.Build(rt);
        rt.gameObject.AddComponent<CanvasGroup>();
        rt.gameObject.AddComponent<UIPopIn>();
        return ui;
    }

    public static void Toggle() { if (inst != null) inst.gameObject.SetActive(!inst.gameObject.activeSelf); }

    void Build(RectTransform r)
    {
        var T = UIKit.T;
        UIKit.Btn(r, "<", 5, 4, 14, 13, T.chip, () => Shift(-1), 7.5f);
        UIKit.Btn(r, ">", W - 19, 4, 14, 13, T.chip, () => Shift(1), 7.5f);
        monthTitle = UIKit.TextAt(r, "Month", 20, 4, W - 40, 13, "", 7.2f, TextAlignmentOptions.Center, true);
        string[] wd = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        float cw = (W - 10f) / 7f;
        for (int i = 0; i < 7; i++) UIKit.TextAt(r, "W" + i, 5 + i * cw, 19, cw, 9, wd[i], 5.5f, TextAlignmentOptions.Center, true, T.dim);
        for (int i = 0; i < 42; i++)
        {
            var cell = UIKit.At(r, "Day" + i, 5 + (i % 7) * cw + 0.5f, 29 + (i / 7) * 15f, cw - 1f, 14f);
            dayBg[i] = UIKit.Tint(cell, Color.clear);
            dayBg[i].raycastTarget = false;
            dayText[i] = UIKit.Text(cell, "", 6.2f, TextAlignmentOptions.Center);
        }
    }

    void OnEnable()
    {
        transform.SetAsLastSibling();
        var clock = GameClock.Instance;
        if (clock != null) { viewMonth = new DateTime(clock.Now.Year, clock.Now.Month, 1); clock.DayChanged += OnDay; }
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (GameClock.Instance != null) GameClock.Instance.DayChanged -= OnDay;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed -= Refresh;
    }

    void OnDay(DateTime _) => Refresh();

    void Shift(int d) { viewMonth = viewMonth.AddMonths(d); Refresh(); }

    void Refresh()
    {
        if (viewMonth == default) return;
        monthTitle.text = viewMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        int offset = (int)viewMonth.DayOfWeek;
        int days = DateTime.DaysInMonth(viewMonth.Year, viewMonth.Month);
        var today = GameClock.Instance != null ? GameClock.Instance.Now.Date : DateTime.MinValue;
        var dep = DeploymentManager.Instance;
        for (int i = 0; i < 42; i++)
        {
            int day = i - offset + 1;
            bool inMonth = day >= 1 && day <= days;
            dayText[i].text = inMonth ? day.ToString() : "";
            var date = inMonth ? new DateTime(viewMonth.Year, viewMonth.Month, day) : default;
            bool isToday = inMonth && date == today, launch = inMonth && dep != null && dep.IsScheduledFor(date);
            dayBg[i].color = isToday ? new Color(0.25f, 0.8f, 1f, 0.55f) : launch ? new Color(1f, 0.6f, 0.35f, 0.65f) : Color.clear;
        }
    }
}
