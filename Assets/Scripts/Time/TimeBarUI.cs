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

    void Start()
    {
        if (calendarButton != null && calendarPanel != null)
            calendarButton.onClick.AddListener(() => calendarPanel.SetActive(!calendarPanel.activeSelf));
        Refresh();
    }

    void Update() => Refresh();

    void Refresh()
    {
        var clock = GameClock.Instance;
        if (clock == null) return;
        if (dayProgressSlider != null) dayProgressSlider.value = clock.DayProgress;
        if (timeText != null) timeText.text = clock.Now.ToString("hh:mm tt", CultureInfo.InvariantCulture);
        if (dateText != null) dateText.text = clock.Now.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture);
    }
}
