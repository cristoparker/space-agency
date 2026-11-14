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
    readonly List<MissionDefinition> pending = new List<MissionDefinition>();

    /// <summary>Contracts the player accepted while another one was running: they start one after another, in this order.</summary>
    public IReadOnlyList<MissionDefinition> Pending => pending;

    public IReadOnlyList<MissionDefinition> Available => available;
    public IReadOnlyList<MissionInstance> Running => running;
    public IReadOnlyList<MissionInstance> Completed => completed;

    /// <summary>The mission shown in the top-bar progress bar.</summary>
    public MissionInstance Tracked { get; private set; }

    /// <summary>Raised whenever lists or the tracked mission change.</summary>
    public event Action Changed;
    public event Action<MissionInstance> MissionStarted;
    public event Action<MissionInstance> MissionCompleted;
    /// <summary>A contract's work is over for any reason (launched, missed its deadline, completed or given up). The base resets its training and tests.</summary>
    public event Action<MissionInstance> MissionEnded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (database != null)
            foreach (var m in database.missions)
                if (m != null && !available.Contains(m)) available.Add(m);
        Changed += TeamSync.MarkMissionsDirty;           // a shared game's host sends the lists to the team when they change
    }

    // ---------------------------------------------------------------- shared game
    /// <summary>The contract lists as the host sends them to its team.</summary>
    public MissionsMsg Snapshot()
    {
        var m = new MissionsMsg
        {
            available = available.ConvertAll(d => d.id).ToArray(), pending = pending.ConvertAll(d => d.id).ToArray(),
            completed = completed.ConvertAll(x => x.Definition.id).ToArray(),
            running = new string[running.Count], runStart = new double[running.Count], runStartTicks = new long[running.Count],
            runShift = new double[running.Count], runProgress = new float[running.Count], tracked = Tracked != null ? Tracked.Definition.id : "",
        };
        for (int i = 0; i < running.Count; i++)
        {
            var r = running[i];
            m.running[i] = r.Definition.id; m.runStart[i] = r.StartGameTime; m.runStartTicks[i] = r.StartDate.Ticks;
            m.runShift[i] = r.ShiftSeconds; m.runProgress[i] = r.Progress;
        }
        return m;
    }

    /// <summary>A guest takes over the host's contract lists. Contracts this game does not know (global market) are left out.</summary>
    public void ApplyRemote(MissionsMsg m)
    {
        MissionDefinition Def(string id)
        {
            if (database != null) foreach (var d in database.missions) if (d != null && d.id == id) return d;
            foreach (var d in available) if (d.id == id) return d;
            foreach (var d in pending) if (d.id == id) return d;
            foreach (var r in running) if (r.Definition.id == id) return r.Definition;
            return null;
        }
        void Fill(List<MissionDefinition> list, string[] ids) { list.Clear(); if (ids != null) foreach (var id in ids) { var d = Def(id); if (d != null) list.Add(d); } }

        var oldRunning = new List<MissionInstance>(running);
        var newAvail = new List<MissionDefinition>(); Fill(newAvail, m.available);
        var newPend = new List<MissionDefinition>(); Fill(newPend, m.pending);
        var newRunning = new List<MissionInstance>();
        int n = m.running?.Length ?? 0;
        for (int i = 0; i < n; i++)
        {
            var d = Def(m.running[i]); if (d == null) continue;
            var inst = oldRunning.Find(x => x.Definition == d && Math.Abs(x.StartGameTime - m.runStart[i]) < 1.0)
                       ?? new MissionInstance(d, m.runStart[i], new DateTime(m.runStartTicks[i]));
            inst.ShiftSeconds = m.runShift[i];
            inst.SetProgress(m.runProgress[i]);
            newRunning.Add(inst);
        }
        var newDone = new List<MissionInstance>();
        if (m.completed != null) foreach (var id in m.completed) { var d = Def(id); if (d != null) newDone.Add(completed.Find(x => x.Definition == d) ?? new MissionInstance(d, 0, DateTime.MinValue) { State = MissionState.Completed }); }

        available.Clear(); available.AddRange(newAvail);
        pending.Clear(); pending.AddRange(newPend);
        running.Clear(); running.AddRange(newRunning);
        completed.Clear(); completed.AddRange(newDone);
        Tracked = running.Find(x => x.Definition.id == m.tracked) ?? (running.Count > 0 ? running[0] : null);
        foreach (var r in running) if (!oldRunning.Contains(r)) MissionStarted?.Invoke(r);
        Changed?.Invoke();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var clock = GameClock.Instance;
        bool mirror = TeamSync.Mirror;                       // a guest only shows the host's contracts: the host decides when they end
        for (int i = running.Count - 1; i >= 0; i--)
        {
            var m = running[i];
            if (m.Definition.progressMode == ProgressMode.TimeBased && clock != null)
            {
                double total = m.Definition.DurationDays * 86400.0;
                m.SetProgress(total > 0 ? (float)((clock.GameTime - m.StartGameTime + m.ShiftSeconds) / total) : 1f);
            }
            if (mirror) continue;
            if (m.Progress >= 1f)
            {
                if (m.Definition.progressMode == ProgressMode.TimeBased) Expire(m);     // the deadline passed before the rocket launched
                else Complete(m);
            }
        }
    }

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>The deadline ran out: the contract is lost.</summary>
    void Expire(MissionInstance m)
    {
        running.Remove(m);
        if (Tracked == m) Tracked = running.Count > 0 ? running[0] : null;
        Agency.AddReputation(-10);
        Hud.Toast("Contract failed: \"" + m.Definition.displayName + "\" missed its deadline (-10 reputation).");
        Changed?.Invoke();
        MissionEnded?.Invoke(m);
        PromoteNext();
    }

    /// <summary>The rocket for this contract was launched successfully: pays the reward and completes the contract.</summary>
    public bool CompleteByLaunch(string missionId)
    {
        var m = running.Find(x => x.Definition.id == missionId);
        if (m == null) return false;
        Complete(m);
        return true;
    }

    /// <summary>The rocket for this contract has been launched: the contract leaves the running list (its flight and reward are handled by FlightManager).</summary>
    public void DetachForFlight(string missionId)
    {
        var m = running.Find(x => x.Definition.id == missionId);
        if (m == null) return;
        running.Remove(m);
        if (Tracked == m) Tracked = running.Count > 0 ? running[0] : null;
        Changed?.Invoke();
        MissionEnded?.Invoke(m);
        PromoteNext();
    }

    /// <summary>Starts a contract that did not come from the local offers (a won global contract).</summary>
    public MissionInstance AcceptExternal(MissionDefinition definition)
    {
        if (definition == null) return null;
        if (TeamSync.Mirror) { Hud.Toast("In a shared agency only the host can take contracts from the global market."); return null; }
        if (!available.Contains(definition)) available.Add(definition);
        return StartMission(definition);
    }

    /// <summary>Only one contract runs at a time: a second accepted contract waits as pending until the first one is launched or ends.</summary>
    public MissionInstance StartMission(MissionDefinition definition)
    {
        if (definition == null) return null;
        if (TeamSync.Route(TeamRole.Finance, TeamAction.Accept, definition.id) != TeamSync.RouteResult.Local) return null;
        if (!available.Remove(definition)) return null;
        if (running.Count > 0) { if (!pending.Contains(definition)) pending.Add(definition); Changed?.Invoke(); return null; }
        return Begin(definition);
    }

    /// <summary>Takes a pending contract off the list and puts it back on offer.</summary>
    public bool RemovePending(MissionDefinition definition)
    {
        if (definition == null) return false;
        var route = TeamSync.Route(TeamRole.Finance, TeamAction.RemovePending, definition.id);
        if (route != TeamSync.RouteResult.Local) return route == TeamSync.RouteResult.Sent;
        if (!pending.Remove(definition)) return false;
        if (!available.Contains(definition)) available.Add(definition);
        Changed?.Invoke();
        return true;
    }

    void PromoteNext()
    {
        if (running.Count > 0 || pending.Count == 0) return;
        var next = pending[0]; pending.RemoveAt(0);
        Begin(next);
        Hud.Toast("Next contract started: " + next.displayName);
    }

    MissionInstance Begin(MissionDefinition definition)
    {
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
        if (definition == null) return false;
        var route = TeamSync.Route(TeamRole.Finance, TeamAction.Decline, definition.id);
        if (route != TeamSync.RouteResult.Local) return route == TeamSync.RouteResult.Sent;
        if (!available.Remove(definition)) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Gives up a running contract: it goes back to the available list and its progress is lost.</summary>
    public bool AbandonMission(MissionInstance mission)
    {
        if (mission == null) return false;
        var route = TeamSync.Route(TeamRole.Finance, TeamAction.Abandon, mission.Definition.id);
        if (route != TeamSync.RouteResult.Local) return route == TeamSync.RouteResult.Sent;
        if (!running.Remove(mission)) return false;
        if (!available.Contains(mission.Definition)) available.Add(mission.Definition);
        if (Tracked == mission) Tracked = running.Count > 0 ? running[0] : null;
        Agency.AddReputation(-5);
        Changed?.Invoke();
        MissionEnded?.Invoke(mission);
        PromoteNext();
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
        MissionEnded?.Invoke(m);
        PromoteNext();
    }
}
