using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows day progress (slider), 12-hour time and the date. Clicking the date toggles the calendar.</summary>
public class TimeBarUI : MonoBehaviour
{
    public Slider dayProgressSlider;
    public TMP_Text timeText;
    public TMP_Text dateText;
    public Button calendarButton;
    public GameObject calendarPanel;
    public Button slowDownButton;
    public Button speedUpButton;
    public TMP_Text speedText;

    void Start()
    {
        if (calendarButton != null && calendarPanel != null)
            calendarButton.onClick.AddListener(() => calendarPanel.SetActive(!calendarPanel.activeSelf));
        if (slowDownButton != null) slowDownButton.onClick.AddListener(() => GameClock.Instance?.SlowDown());
        if (speedUpButton != null) speedUpButton.onClick.AddListener(() => GameClock.Instance?.SpeedUp());
        Refresh();
    }

    void Update() => Refresh();

    void Refresh()
    {
        var clock = GameClock.Instance;
        if (clock == null) return;
        if (dayProgressSlider != null) dayProgressSlider.value = clock.DayProgress;
        if (timeText != null) timeText.text = clock.Now.ToString("hh:mm tt", CultureInfo.InvariantCulture);
        if (speedText != null) speedText.text = clock.Speed <= 0f ? "Pause" : "x" + clock.Speed.ToString("0.##", CultureInfo.InvariantCulture);
        if (slowDownButton != null) slowDownButton.interactable = clock.SpeedIndex > 0;
        if (speedUpButton != null) speedUpButton.interactable = clock.SpeedIndex < clock.speedLevels.Length - 1;
        if (dateText != null) dateText.text = clock.Now.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture);
    }
}
