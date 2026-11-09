using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static description of a mission/contract. One asset per mission, edited in the Inspector.
/// Create new ones with Assets > Create > Space Agency > Mission, or import JSON via Tools > Missions.
/// </summary>
[CreateAssetMenu(menuName = "Space Agency/Mission", fileName = "MISSION-000")]
public class MissionDefinition : ScriptableObject
{
    public string id = "MISSION-000";
    public string displayName;
    public string missionType;
    public string target;
    public long budget;
    [Tooltip("Maximum launch mass in kg.")]
    public float massLimit;
    public MissionPayload payload = new MissionPayload();
    public MissionDuration duration = new MissionDuration { value = 30, unit = DurationUnit.Days };
    [TextArea(2, 6)] public string objective;
    public List<MissionRequirement> specialRequirements = new List<MissionRequirement>();

    [Tooltip("TimeBased = follows the game clock. Manual = other systems set progress (e.g. rocket flight).")]
    public ProgressMode progressMode = ProgressMode.TimeBased;

    public double DurationDays => duration.ToDays();
}
