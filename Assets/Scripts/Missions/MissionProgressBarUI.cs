using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Top-bar progress of the tracked mission (set in the dashboard by clicking a running mission).</summary>
public class MissionProgressBarUI : MonoBehaviour
{
    public Slider slider;
    public TMP_Text percentText;
    [Tooltip("Optional: text shown next to the bar with the mission name.")]
    public TMP_Text nameText;

    void Update()
    {
        var m = MissionManager.Instance != null ? MissionManager.Instance.Tracked : null;
        if (m == null)
        {
            slider.value = 0f;
            if (percentText != null) percentText.text = "--";
            if (nameText != null) nameText.text = string.Empty;
            return;
        }
        slider.value = m.Progress;
        if (percentText != null) percentText.text = m.Percent + "%";
        if (nameText != null) nameText.text = m.Definition.displayName;
    }
}
