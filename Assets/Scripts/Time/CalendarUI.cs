using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Month calendar grid (Sunday first). Highlights the current in-game day.</summary>
public class CalendarUI : MonoBehaviour
{
    public TMP_Text monthTitle;
    public TMP_Text[] dayCells = new TMP_Text[42];
    public Image[] dayCellBackgrounds = new Image[42];
    public Button previousMonthButton;
    public Button nextMonthButton;

    public Color todayColor = new Color32(0xFF, 0xEB, 0x3B, 255);
    public Color normalColor = new Color(1f, 1f, 1f, 0f);

    DateTime viewMonth;

    void Awake()
    {
        if (previousMonthButton != null) previousMonthButton.onClick.AddListener(() => ShiftMonth(-1));
        if (nextMonthButton != null) nextMonthButton.onClick.AddListener(() => ShiftMonth(1));
    }

    void OnEnable()
    {
        var clock = GameClock.Instance;
        if (clock != null)
        {
            viewMonth = new DateTime(clock.Now.Year, clock.Now.Month, 1);
            clock.DayChanged += OnDayChanged;
        }
        Refresh();
    }

    void OnDisable()
    {
        if (GameClock.Instance != null) GameClock.Instance.DayChanged -= OnDayChanged;
    }

    void OnDayChanged(DateTime _) => Refresh();

    void ShiftMonth(int delta)
    {
        viewMonth = viewMonth.AddMonths(delta);
        Refresh();
    }

    void Refresh()
    {
        if (viewMonth == default) return;
        if (monthTitle != null) monthTitle.text = viewMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        int offset = (int)viewMonth.DayOfWeek;
        int days = DateTime.DaysInMonth(viewMonth.Year, viewMonth.Month);
        var today = GameClock.Instance != null ? GameClock.Instance.Now.Date : DateTime.MinValue;

        for (int i = 0; i < dayCells.Length; i++)
        {
            int day = i - offset + 1;
            bool inMonth = day >= 1 && day <= days;
            if (dayCells[i] != null) dayCells[i].text = inMonth ? day.ToString() : string.Empty;
            if (i < dayCellBackgrounds.Length && dayCellBackgrounds[i] != null)
            {
                bool isToday = inMonth && new DateTime(viewMonth.Year, viewMonth.Month, day) == today;
                dayCellBackgrounds[i].color = isToday ? todayColor : normalColor;
            }
        }
    }
}
