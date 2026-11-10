using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public enum ObjectiveKind { Training, Design, Testing, Deploy, Launch }
public enum ObjectiveState { Done, Running, Available, Locked }

/// <summary>
/// One step of a contract. Steps are not a queue: every step whose prerequisites are done can be worked on at the same time
/// (train scientists while a pilot course runs and the rocket is being designed). Only real dependencies lock a step.
/// </summary>
public class MissionObjective
{
    public string id, title, detail;
    public ObjectiveKind kind;
    public string programId;
    public List<string> requires = new List<string>();
    public ObjectiveState state;
    public float progress;            // 0..1 while it runs
    public string status;             // short text: "3 days left", "Needs: Design a rocket", "Ready to start"...
    public string actionLabel;
    public Action action;
    public bool done => state == ObjectiveState.Done;
}

/// <summary>
/// Builds the objective graph of a running contract from the game state (nothing is stored, so it is always up to date):
///
///   Train scientists ──► Advanced scientist training (far missions)
///   Train pilots (crewed missions)
///   Design a rocket ──► Test the engines / comms / structure   (a test needs a rocket to test)
///                  └──► Deploy the rocket to the pad
///   Launch  needs: Deploy + every other step done (and the launch day)
/// </summary>
public static class MissionObjectives
{
    static bool Crewed(MissionDefinition d) => d.payload != null && !string.IsNullOrEmpty(d.payload.type) &&
        (d.payload.type.IndexOf("crew", StringComparison.OrdinalIgnoreCase) >= 0 || d.payload.type.IndexOf("human", StringComparison.OrdinalIgnoreCase) >= 0);

    static bool Far(MissionDefinition d)
    {
        string t = (d.target ?? "").ToLowerInvariant();
        return t.Contains("moon") || t.Contains("mars") || t.Contains("lunar") || t.Contains("asteroid");
    }

    static bool Needs(MissionDefinition d, string type) => d.specialRequirements != null &&
        d.specialRequirements.Any(r => string.Equals(r.type, type, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the player has designed a rocket that passes the checks (a saved design marked valid, or one on the pad).</summary>
    public static bool HasRocketDesign()
    {
        var dep = DeploymentManager.Instance;
        if (dep != null && dep.Current != null) return true;
        foreach (var r in RocketSaves.All()) if (r.valid) return true;
        return false;
    }

    public static List<MissionObjective> For(MissionInstance m)
    {
        var d = m.Definition;
        var list = new List<MissionObjective>();

        list.Add(Program("sci1", "Train your scientists", "sci1"));
        if (Far(d)) list.Add(Program("sci2", "Advanced scientist training", "sci2", "sci1"));
        if (Crewed(d)) list.Add(Program("pilot1", "Train your pilots", "pilot1"));

        list.Add(Design());
        list.Add(Program("test_engine", "Test the engines", "test_engine", "design"));
        if (Needs(d, "communication")) list.Add(Program("test_comms", "Test the comms link", "test_comms", "design"));
        if (Far(d) || Needs(d, "landing")) list.Add(Program("test_struct", "Test the structure", "test_struct", "design"));
        list.Add(Deploy(d));

        var launch = Launch(d);
        foreach (var o in list) launch.requires.Add(o.id);                      // the launch needs everything else
        list.Add(launch);

        // a step is locked while something it requires is not done
        var byId = list.ToDictionary(o => o.id);
        foreach (var o in list)
        {
            if (o.state == ObjectiveState.Done) continue;
            var missing = o.requires.Where(r => byId.TryGetValue(r, out var q) && !q.done).Select(r => byId[r].title).ToList();
            if (missing.Count > 0)
            {
                o.state = ObjectiveState.Locked; o.action = null; o.actionLabel = null; o.progress = 0f;
                o.status = missing.Count == 1 ? "Needs: " + missing[0] : "Needs: " + missing[0] + " + " + (missing.Count - 1) + " more";
            }
        }
        return list;
    }

    /// <summary>True when everything except the launch itself is finished: the launch button may appear (on and after the launch day).</summary>
    public static bool ReadyToLaunch(string missionId)
    {
        if (string.IsNullOrEmpty(missionId)) return true;                       // a rocket that was not built for a contract
        var mm = MissionManager.Instance;
        var inst = mm != null ? mm.Running.FirstOrDefault(x => x.Definition.id == missionId) : null;
        if (inst == null) return true;
        foreach (var o in For(inst)) if (o.kind != ObjectiveKind.Launch && !o.done) return false;
        return true;
    }

    /// <summary>The launch button is shown: launch day reached, rocket on the pad and every other objective done.</summary>
    public static bool LaunchAllowed(DeploymentManager dep) => dep != null && dep.LaunchReady && ReadyToLaunch(dep.Current.missionId);

    // ---------------------------------------------------------------- steps
    static MissionObjective Program(string id, string title, string programId, params string[] requires)
    {
        var p = BaseData.Program(programId);
        var sim = BaseSim.Instance;
        var o = new MissionObjective { id = id, title = title, kind = p != null && p.kind == ProgramKind.Training ? ObjectiveKind.Training : ObjectiveKind.Testing, programId = programId, state = ObjectiveState.Available };
        o.requires.AddRange(requires);
        if (p == null || sim == null) { o.status = "-"; return o; }
        o.detail = p.name + " at the " + BaseData.BuildingName(p.building);
        if (sim.IsDone(programId)) { o.state = ObjectiveState.Done; o.progress = 1f; o.status = "Done"; return o; }
        var job = sim.Job(programId);
        if (job != null) { o.state = ObjectiveState.Running; o.progress = BaseSim.Progress(job); o.status = BaseSim.DaysLeft(job) + " days left"; return o; }
        o.status = sim.WhyNot(p) ?? "Ready to start";
        o.actionLabel = p.kind == ProgramKind.Training ? "Training" : "Testing";
        o.action = () => Panels.Open(p.kind == ProgramKind.Training ? "programs:0" : "programs:1");
        return o;
    }

    static MissionObjective Design()
    {
        var o = new MissionObjective { id = "design", title = "Design a rocket", kind = ObjectiveKind.Design, detail = "Build one that passes the contract checks and save it" };
        bool has = HasRocketDesign();
        o.state = has ? ObjectiveState.Done : ObjectiveState.Available;
        o.progress = has ? 1f : 0f;
        o.status = has ? "Rocket ready" : "No valid rocket yet";
        if (!has) { o.actionLabel = "Designer"; o.action = () => Panels.Open("rocket"); }
        return o;
    }

    static MissionObjective Deploy(MissionDefinition d)
    {
        var dep = DeploymentManager.Instance;
        var o = new MissionObjective { id = "deploy", title = "Deploy the rocket to the pad", kind = ObjectiveKind.Deploy, detail = "Pick a launch date in the Rocket Designer" };
        o.requires.Add("design");
        bool deployed = dep != null && dep.IsMissionDeployed(d.id) && dep.LaunchDate.HasValue;
        o.state = deployed ? ObjectiveState.Done : ObjectiveState.Available;
        o.progress = deployed ? 1f : 0f;
        o.status = deployed ? "On the pad" : "Not on the pad yet";
        if (!deployed) { o.actionLabel = "Designer"; o.action = () => Panels.Open("rocket"); }
        return o;
    }

    static MissionObjective Launch(MissionDefinition d)
    {
        var dep = DeploymentManager.Instance;
        var o = new MissionObjective { id = "launch", title = "Launch", kind = ObjectiveKind.Launch, detail = "Press the glowing Launch button on launch day", state = ObjectiveState.Available };
        if (dep != null && dep.IsMissionDeployed(d.id) && dep.LaunchDate.HasValue)
        {
            var date = dep.LaunchDate.Value;
            int days = dep.DaysToLaunch;
            o.status = date.ToString("dd MMM", CultureInfo.InvariantCulture) + (days > 1 ? "  ·  in " + days + " days" : days == 1 ? "  ·  tomorrow" : "  ·  today");
        }
        else o.status = "Waiting for the rocket";
        return o;
    }
}
