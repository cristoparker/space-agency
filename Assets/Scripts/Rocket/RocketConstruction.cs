using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public class ConstructionJob
{
    public string rocketName, design, missionId, missionName;
    public float remaining, total;          // game seconds
    public bool ready;                      // built: waiting for the player to choose the launch date
}

/// <summary>
/// Building a rocket takes time, like a building: after the design is set, the rocket is under construction (a progress bar on the Launch Complex and in the
/// objectives). How long it takes is worked out in the background from the rocket's size and kind. When it is finished the player is asked for the launch
/// date, and only then does the rocket stand on the pad. Construction stops while the power is short.
/// </summary>
public class RocketConstruction : MonoBehaviour
{
    public static RocketConstruction Instance { get; private set; }
    const string Key = "SpaceAgency.RocketBuild";
    public static void ResetStatics() { Instance = null; }

    public ConstructionJob Current { get; private set; }
    public event Action Changed;
    float saveTimer;

    void Awake()
    {
        Instance = this;
        try { var j = JsonUtility.FromJson<ConstructionJob>(PlayerPrefs.GetString(Key, "")); if (j != null && !string.IsNullOrEmpty(j.design)) Current = j; } catch { /* nothing saved */ }
    }

    void OnDestroy() { Save(); if (Instance == this) Instance = null; }
    void OnApplicationQuit() => Save();

    void Save() { if (Current != null) PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current)); else PlayerPrefs.DeleteKey(Key); }

    public bool Building => Current != null && !Current.ready;
    public bool WaitingForDate => Current != null && Current.ready;
    public float Progress => Current == null ? 0f : Current.ready ? 1f : Current.total <= 0f ? 1f : Mathf.Clamp01(1f - Current.remaining / Current.total);
    public int DaysLeft => Current == null || Current.ready ? 0 : Mathf.CeilToInt(Current.remaining / 86400f);
    public bool IsFor(string missionId) => Current != null && !string.IsNullOrEmpty(missionId) && Current.missionId == missionId;

    /// <summary>
    /// Days of construction: a base of a few days, more for a heavier rocket (square root of its mass), for every stage and booster, and extra for a crew capsule
    /// or a heavy payload. A small satellite launcher takes about two weeks; a big multi-stage rocket more than a month.
    /// </summary>
    public static float DaysFor(RocketReport rep, RocketDesign d, PartLibrary lib)
    {
        float tons = Mathf.Max(0.5f, rep.launchKg / 1000f);
        int stages = 0; foreach (var s in rep.stages) if (s.parts.Count > 0) stages++;
        int boosters = !string.IsNullOrEmpty(d.boosterId) ? Mathf.Max(0, d.boosterCount) : 0;
        bool crew = false; foreach (var id in d.stack) if (id.StartsWith("Head_Capsule")) crew = true;
        float days = 4f + 1.6f * Mathf.Sqrt(tons) + 2f * Mathf.Max(1, stages) + 0.8f * boosters + (crew ? 5f : 0f) + (rep.payloadKg > 1000f ? 2f : 0f);
        return Mathf.Max(5f, Mathf.Ceil(days));
    }

    /// <summary>Starts building the rocket. Returns false when another one is already being built.</summary>
    public bool Begin(string rocketName, RocketDesign design, MissionDefinition mission, float days)
    {
        if (Current != null) return false;
        float secs = days * 86400f;
        Current = new ConstructionJob { rocketName = rocketName, design = design.ToJson(), missionId = mission != null ? mission.id : "", missionName = mission != null ? mission.displayName : "", remaining = secs, total = secs };
        Save(); Changed?.Invoke();
        return true;
    }

    void Update()
    {
        if (Current == null || Current.ready) return;
        var clock = GameClock.Instance; if (clock == null) return;
        float dt = clock.GameDeltaSeconds;
        if (dt > 0f && !ResourceFlow.PowerShort) Current.remaining -= dt;
        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer > 10f) { saveTimer = 0f; Save(); }
        if (Current.remaining <= 0f)
        {
            Current.remaining = 0f; Current.ready = true; Save(); Changed?.Invoke();
            Hud.Toast("\"" + Current.rocketName + "\" is built. Choose its launch date.");
            LaunchDateUI.Show();
        }
    }

    /// <summary>The player picked the launch date: the finished rocket goes to the pad.</summary>
    public void Finish(DateTime date)
    {
        if (Current == null || !Current.ready || DeploymentManager.Instance == null) return;
        var design = RocketDesign.FromJson(Current.design);
        MissionDefinition mission = null;
        var mm = MissionManager.Instance;
        if (mm != null && !string.IsNullOrEmpty(Current.missionId)) foreach (var m in mm.Running) if (m.Definition.id == Current.missionId) mission = m.Definition;
        string nm = Current.rocketName;
        DeploymentManager.Instance.Schedule(nm, design, mission, date);
        Current = null; Save(); Changed?.Invoke();
        Hud.Toast("\"" + nm + "\" is on the launch pad. Launch " + date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
    }
}
