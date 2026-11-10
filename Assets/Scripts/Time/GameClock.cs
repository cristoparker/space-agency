using System;
using UnityEngine;

/// <summary>
/// In-game clock. Starts from the real current time in Dhaka (GMT+6, no DST)
/// and then runs faster than real time. One day passes in 1440 / gameMinutesPerSecond real seconds.
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    [Tooltip("In-game minutes that pass per real second at speed x1. 24 = one real minute is one game day (60 s x 24 = 1440 game minutes).")]
    public float gameMinutesPerSecond = 24f;

    [Tooltip("Start from the real current Dhaka time. If off, starts on the date/time below.")]
    public bool startFromRealDhakaTime = true;

    public int startYear = 2026, startMonth = 1, startDay = 1;
    [Range(0, 23)] public int startHour = 6;

    public bool paused;

    [Tooltip("Selectable game speeds. 0 = paused.")]
    public float[] speedLevels = { 0f, 1f, 2f, 4f, 8f, 16f, 100f, 200f, 500f };

    [Tooltip("Time.timeScale never goes above this (physics and animation stay stable). " +
             "Game speed above it only advances the clock - use GameDeltaSeconds / GameTime for rockets and orbits.")]
    public float maxTimeScale = 16f;
    [Tooltip("Index into Speed Levels used at start (1 = x1).")]
    public int startSpeedIndex = 1;

    public int SpeedIndex { get; private set; }
    public float Speed => speedLevels[SpeedIndex];
    public event Action<float> SpeedChanged;

    public DateTime Now { get; private set; }

    /// <summary>Game seconds that passed during this frame (includes fast-forward). 0 while paused.</summary>
    public float GameDeltaSeconds { get; private set; }

    /// <summary>Total game seconds since the game started (includes fast-forward).</summary>
    public double GameTime { get; private set; }

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
        SetSpeedIndex(startSpeedIndex);
    }

    void OnDestroy()
    {
        if (Instance == this) { Time.timeScale = 1f; Instance = null; }
    }

    public void SetSpeedIndex(int index)
    {
        SpeedIndex = Mathf.Clamp(index, 0, speedLevels.Length - 1);
        Time.timeScale = Mathf.Min(speedLevels[SpeedIndex], maxTimeScale);
        SpeedChanged?.Invoke(Speed);
    }

    public void SpeedUp() => SetSpeedIndex(SpeedIndex + 1);
    public void SlowDown() => SetSpeedIndex(SpeedIndex - 1);

    void Update()
    {
        GameDeltaSeconds = 0f;
        if (paused || Speed <= 0f) return;
        var before = Now;
        double minutes = gameMinutesPerSecond * Speed * Time.unscaledDeltaTime;
        GameDeltaSeconds = (float)(minutes * 60.0);
        GameTime += minutes * 60.0;
        Now = Now.AddMinutes(minutes);
        if (Now.Minute != before.Minute || Now.Hour != before.Hour || Now.Date != before.Date)
            MinuteChanged?.Invoke(Now);
        if (Now.Date != before.Date)
            DayChanged?.Invoke(Now);
    }
}
