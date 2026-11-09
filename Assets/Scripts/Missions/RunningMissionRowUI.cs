using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One running mission in the dashboard: name, type, progress bar and %.</summary>
public class RunningMissionRowUI : MonoBehaviour, IPointerClickHandler
{
    public Image background;
    public TMP_Text nameText;
    public TMP_Text infoText;
    public TMP_Text percentText;
    public Slider progressSlider;
    public Color normalColor = new Color(1f, 1f, 1f, 1f);
    public Color trackedColor = new Color32(0xFF, 0xF1, 0x76, 255);

    public MissionInstance Mission { get; private set; }
    Action<MissionInstance> onClick;

    public void Bind(MissionInstance mission, Action<MissionInstance> click)
    {
        Mission = mission;
        onClick = click;
        nameText.text = mission.Definition.displayName;
        infoText.text = mission.Definition.missionType + " - " + mission.Definition.target;
        Refresh();
    }

    void Update() => Refresh();

    void Refresh()
    {
        if (Mission == null) return;
        progressSlider.value = Mission.Progress;
        percentText.text = Mission.Percent + "%";
        if (background != null)
            background.color = MissionManager.Instance != null && MissionManager.Instance.Tracked == Mission ? trackedColor : normalColor;
    }

    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(Mission);
}
