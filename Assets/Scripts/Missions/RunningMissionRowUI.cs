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
    public Color normalColor = new Color(1f, 1f, 1f, 0.14f);
    public Color trackedColor = new Color(0.25f, 0.8f, 1f, 0.38f);

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
        var dep = DeploymentManager.Instance;
        string launch = dep != null && dep.IsMissionDeployed(Mission.Definition.id) && dep.LaunchDate.HasValue ? "  |  launch " + dep.LaunchDate.Value.ToString("dd MMM", System.Globalization.CultureInfo.InvariantCulture) : "";
        infoText.text = Mission.Definition.missionType + " - " + Mission.Definition.target + launch;
        if (background != null)
            background.color = MissionManager.Instance != null && MissionManager.Instance.Tracked == Mission ? trackedColor : normalColor;
    }

    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(Mission);
}
