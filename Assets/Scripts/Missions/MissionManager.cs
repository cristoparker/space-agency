using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns mission state: available contracts, running missions and completed missions.
/// Other systems (rockets, orbits, UI) talk to this class only.
/// </summary>
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    public MissionDatabase database;

    readonly List<MissionDefinition> available = new List<MissionDefinition>();
    readonly List<MissionInstance> running = new List<MissionInstance>();
    readonly List<MissionInstance> completed = new List<MissionInstance>();

    public IReadOnlyList<MissionDefinition> Available => available;
    public IReadOnlyList<MissionInstance> Running => running;
    public IReadOnlyList<MissionInstance> Completed => completed;

    /// <summary>The mission shown in the top-bar progress bar.</summary>
    public MissionInstance Tracked { get; private set; }

    /// <summary>Raised whenever lists or the tracked mission change.</summary>
    public event Action Changed;
    public event Action<MissionInstance> MissionStarted;
    public event Action<MissionInstance> MissionCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (database != null)
            foreach (var m in database.missions)
                if (m != null && !available.Contains(m)) available.Add(m);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var clock = GameClock.Instance;
        for (int i = running.Count - 1; i >= 0; i--)
        {
            var m = running[i];
            if (m.Definition.progressMode == ProgressMode.TimeBased && clock != null)
            {
                double total = m.Definition.DurationDays * 86400.0;
                m.SetProgress(total > 0 ? (float)((clock.GameTime - m.StartGameTime) / total) : 1f);
            }
            if (m.Progress >= 1f) Complete(m);
        }
    }

    public MissionInstance StartMission(MissionDefinition definition)
    {
        if (definition == null || !available.Remove(definition)) return null;
        var clock = GameClock.Instance;
        var inst = new MissionInstance(definition, clock != null ? clock.GameTime : 0.0, clock != null ? clock.Now : DateTime.Now);
        running.Add(inst);
        if (Tracked == null) Tracked = inst;
        MissionStarted?.Invoke(inst);
        Changed?.Invoke();
        return inst;
    }

    /// <summary>Rejects a contract so it no longer appears in the list.</summary>
    public bool DiscardMission(MissionDefinition definition)
    {
        if (!available.Remove(definition)) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Gives up a running contract: it goes back to the available list and its progress is lost.</summary>
    public bool AbandonMission(MissionInstance mission)
    {
        if (mission == null || !running.Remove(mission)) return false;
        if (!available.Contains(mission.Definition)) available.Add(mission.Definition);
        if (Tracked == mission) Tracked = running.Count > 0 ? running[0] : null;
        Agency.AddReputation(-5);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Hook for other systems: set progress (0..1) of a running mission by id.</summary>
    public bool SetProgress(string missionId, float progress)
    {
        var m = running.Find(x => x.Definition.id == missionId);
        if (m == null) return false;
        m.SetProgress(progress);
        return true;
    }

    public void TrackMission(MissionInstance mission)
    {
        if (mission == null || !running.Contains(mission) || Tracked == mission) return;
        Tracked = mission;
        Changed?.Invoke();
    }

    void Complete(MissionInstance m)
    {
        m.SetProgress(1f);
        m.State = MissionState.Completed;
        running.Remove(m);
        completed.Add(m);
        if (Tracked == m) Tracked = running.Count > 0 ? running[0] : null;
        Agency.Earn((long)(m.Definition.budget * Agency.RewardFactor));
        Agency.AddReputation(25);
        MissionCompleted?.Invoke(m);
        Changed?.Invoke();
    }
}
