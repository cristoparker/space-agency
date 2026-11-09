using System;
using UnityEngine;

/// <summary>
/// In-game clock. Starts from the real current time in Dhaka (GMT+6, no DST)
/// and then runs faster than real time. One day passes in 1440 / gameMinutesPerSecond real seconds.
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    [Tooltip("In-game minutes that pass per real second. 1 = a full day takes 24 real minutes.")]
    public float gameMinutesPerSecond = 1f;

    [Tooltip("Start from the real current Dhaka time. If off, starts on the date/time below.")]
    public bool startFromRealDhakaTime = true;

    public int startYear = 2026, startMonth = 1, startDay = 1;
    [Range(0, 23)] public int startHour = 6;

    public bool paused;

    public DateTime Now { get; private set; }

    /// <summary>0..1 progress through the current day (00:00 -> 24:00).</summary>
    public float DayProgress => (float)(Now.TimeOfDay.TotalMinutes / 1440.0);

    public event Action<DateTime> MinuteChanged;
    public event Action<DateTime> DayChanged;

    const double DhakaOffsetHours = 6.0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Now = startFromRealDhakaTime
            ? DateTime.UtcNow.AddHours(DhakaOffsetHours)
            : new DateTime(startYear, startMonth, startDay, startHour, 0, 0);
    }

    void Update()
    {
        if (paused) return;
        var before = Now;
        Now = Now.AddMinutes(gameMinutesPerSecond * Time.deltaTime);
        if (Now.Minute != before.Minute || Now.Hour != before.Hour || Now.Date != before.Date)
            MinuteChanged?.Invoke(Now);
        if (Now.Date != before.Date)
            DayChanged?.Invoke(Now);
    }
}
