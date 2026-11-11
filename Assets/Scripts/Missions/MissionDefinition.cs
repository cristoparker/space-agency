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

    /// <summary>The contract's length in days: always between one month and six months (never years).</summary>
    public double DurationDays
    {
        get
        {
            // the deadline follows the difficulty: easy contracts get a long time (4 to 6 months), average ones 2 to 3 months, hard ones only 1 to 1.5 months
            uint h = 2166136261u; foreach (char c in id ?? "") { h ^= c; h *= 16777619u; }
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;                          // mixed, so neighbouring ids get different deadlines
            float t = (h % 1000u) / 999f;
            switch (Difficulty)
            {
                case 0: return System.Math.Round(120.0 + 60.0 * t);
                case 1: return System.Math.Round(60.0 + 30.0 * t);
                default: return System.Math.Round(40.0 + 10.0 * t);
            }
        }
    }

    /// <summary>0 = easy, 1 = average, 2 = hard: from the target (Earth orbit, Moon, Mars), a crew, the number of special requirements and the mass.</summary>
    public int Difficulty
    {
        get
        {
            int s = 0; string t = (target ?? "").ToLowerInvariant();
            if (t.Contains("mars") || t.Contains("asteroid")) s += 3; else if (t.Contains("moon") || t.Contains("lunar")) s += 2;
            string pt = payload != null ? (payload.type ?? "").ToLowerInvariant() : "";
            if (pt.Contains("crew") || pt.Contains("human")) s += 1;
            if (specialRequirements != null && specialRequirements.Count >= 2) s += 1;
            if (massLimit >= 1500f) s += 1;
            return s <= 1 ? 0 : s <= 3 ? 1 : 2;
        }
    }

    public string DifficultyName => Difficulty == 0 ? "Easy" : Difficulty == 1 ? "Average" : "Hard";
}
