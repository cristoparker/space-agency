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

    public Color todayColor = new Color(0.25f, 0.8f, 1f, 0.55f);
    public Color normalColor = new Color(1f, 1f, 1f, 0f);
    [Tooltip("Day a rocket on the launch pad is scheduled to launch.")]
    public Color scheduledColor = new Color(1f, 0.6f, 0.35f, 0.55f);

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
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (GameClock.Instance != null) GameClock.Instance.DayChanged -= OnDayChanged;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed -= Refresh;
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
                bool launch = inMonth && DeploymentManager.Instance != null && DeploymentManager.Instance.IsScheduledFor(new DateTime(viewMonth.Year, viewMonth.Month, day));
                dayCellBackgrounds[i].color = isToday ? todayColor : launch ? scheduledColor : normalColor;
            }
        }
    }
}
