using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>One line of the mission diary: an icon name (Resources/UIIcons), the text and how it went (0 good, 1 so-so, 2 bad).</summary>
[Serializable] public class DiaryItem { public string section, icon, text; public int sev; }

/// <summary>Everything the player did for one mission, written down at launch. The "AI" learning outcome is built from it.</summary>
[Serializable]
public class LearningReport
{
    public string missionName, target, rocketName, launchDate, cause;
    public int result, stars, stages;
    public long reward;
    public float dvTotal, dvRequired, twr, stability, reliability;
    public int trainingDone, trainingTotal, testsDone, testsTotal;
    public float costM, budgetM;
    public bool riskyDecision, extraBudget;
    public List<DiaryItem> items = new List<DiaryItem>();
    public LaunchResult Result => (LaunchResult)result;
}

/// <summary>
/// Prototype of the "AI learning outcome": the diary of the last launched mission is saved here and shown in <see cref="LearningOutcomeUI"/>.
/// The "AI" is pretend: the analysis is built from the diary with fixed, real-world rocketry statements and no backend.
/// </summary>
public static class LearningLog
{
    public const string Key = "SpaceAgency.LastLearning";
    static LearningReport cached;
    static bool loaded;
    public static event Action Changed;

    public static void ResetStatics() { cached = null; loaded = false; }

    public static LearningReport Last
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                try { var r = JsonUtility.FromJson<LearningReport>(PlayerPrefs.GetString(Key, "")); if (r != null && r.items != null && r.items.Count > 0) cached = r; }
                catch { cached = null; }
            }
            return cached;
        }
    }

    public static void Clear() { cached = null; loaded = true; PlayerPrefs.DeleteKey(Key); Changed?.Invoke(); }

    static string IconFor(MissionObjective o)
    {
        switch (o.id)
        {
            case "sci1": case "sci2": return "icon_scientist";
            case "pilot1": return "icon_pilot";
            case "test_engine": return "icon_engine";
            case "test_comms": return "icon_comms";
            case "test_struct": return "icon_structure";
            case "design": return "icon_rocket";
            case "deploy": return "icon_flag";
            case "launch": return "icon_launch";
        }
        return o.kind == ObjectiveKind.Testing ? "icon_flask" : o.kind == ObjectiveKind.Training ? "icon_scientist" : "icon_check";
    }

    /// <summary>Called when the launch starts, before the contract leaves the running list: writes the diary of the whole mission.</summary>
    public static void Capture(Deployment dep, PartLibrary lib, FlightRecord rec)
    {
        if (dep == null || rec == null) return;
        var mm = MissionManager.Instance;
        var inst = mm != null && !string.IsNullOrEmpty(dep.missionId) ? mm.Running.FirstOrDefault(x => x.Definition.id == dep.missionId) : null;
        var def = inst != null ? inst.Definition : null;
        var r = new LearningReport
        {
            missionName = !string.IsNullOrEmpty(rec.missionName) ? rec.missionName : def != null ? def.displayName : "Free flight",
            target = def != null ? def.target : "",
            rocketName = string.IsNullOrEmpty(rec.rocketName) ? "Rocket" : rec.rocketName,
            result = rec.result, stars = rec.stars, reward = rec.reward, cause = rec.cause,
        };
        var clock = GameClock.Instance;
        DateTime now = clock != null ? clock.Now : DateTime.Now;
        r.launchDate = now.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        void Add(string section, string icon, string text, int sev = 0) => r.items.Add(new DiaryItem { section = section, icon = icon, text = text, sev = sev });

        // 1. preparation: the objective steps of the contract
        if (inst != null)
        {
            foreach (var o in MissionObjectives.For(inst))
            {
                if (o.kind == ObjectiveKind.Launch || o.kind == ObjectiveKind.Design || o.kind == ObjectiveKind.Deploy) continue;
                bool training = o.kind == ObjectiveKind.Training;
                if (training) { r.trainingTotal++; if (o.done) r.trainingDone++; } else { r.testsTotal++; if (o.done) r.testsDone++; }
                Add(training ? "Preparation" : "Testing", IconFor(o), o.done ? o.title + " - done" : "Skipped: " + o.title, o.done ? 0 : 1);
            }
        }

        // 2. design: what the player built
        if (lib != null && !string.IsNullOrEmpty(dep.design))
        {
            try
            {
                var design = RocketDesign.FromJson(dep.design);
                design.stack.RemoveAll(id => lib.Get(id) == null);
                var rep = RocketStats.Evaluate(design, lib, def);
                r.stages = Mathf.Max(1, rep.stages.Count);
                r.dvTotal = rep.dvTotal; r.dvRequired = rep.dvRequired; r.twr = rep.liftoffTwr;
                r.stability = rep.stability; r.reliability = rep.reliability; r.costM = rep.costM;
                Add("Design", "icon_rocket", r.rocketName + ": " + r.stages + (r.stages == 1 ? " stage, " : " stages, ") + rep.heightM.ToString("0") + " m tall, " + (rep.launchKg / 1000f).ToString("0.#") + " t at liftoff");
                if (rep.dvRequired > 0f)
                {
                    float ratio = rep.dvTotal / rep.dvRequired;
                    Add("Design", "icon_upgrade", "Delta-v " + (rep.dvTotal / 1000f).ToString("0.0") + " km/s, needed " + (rep.dvRequired / 1000f).ToString("0.0") + " km/s", ratio >= 1.02f ? 0 : ratio >= 0.9f ? 1 : 2);
                }
                else Add("Design", "icon_upgrade", "Delta-v budget " + (rep.dvTotal / 1000f).ToString("0.0") + " km/s");
                Add("Design", "icon_engine", "Liftoff thrust-to-weight " + rep.liftoffTwr.ToString("0.00"), rep.liftoffTwr >= 1.2f && rep.liftoffTwr <= 2.6f ? 0 : rep.liftoffTwr >= 1f ? 1 : 2);
                if (rep.stability > 0f) Add("Design", "icon_structure", "Stability " + Mathf.RoundToInt(rep.stability * (rep.stability <= 1f ? 100f : 1f)) + "%", rep.stability >= (rep.stability <= 1f ? 0.6f : 60f) ? 0 : 1);
                if (def != null)
                {
                    r.budgetM = def.budget / 1_000_000f;
                    float cr = r.budgetM > 0f ? r.costM / r.budgetM : 0f;
                    Add("Budget", "icon_money", "Rocket cost $" + r.costM.ToString("0.#") + " M of a $" + r.budgetM.ToString("0.#") + " M contract", cr <= 0.7f ? 0 : cr <= 1f ? 1 : 2);
                }
            }
            catch (Exception e) { Debug.LogWarning("LearningLog: could not read the design: " + e.Message); }
        }

        // 3. decisions in the contract's random events
        string mid = dep.missionId ?? "";
        float risk = RandomEvents.LaunchRisk(mid); long extra = RandomEvents.ExtraBudget(mid);
        if (risk > 0.001f) { r.riskyDecision = true; Add("Decisions", "icon_time", "Accepted a faster, riskier schedule (+" + Mathf.RoundToInt(risk * 100f) + "% launch risk)", 1); }
        if (extra > 0) { r.extraBudget = true; Add("Decisions", "icon_money", "Negotiated an extra budget of $" + UIKit.Money(extra), 0); }
        if (risk <= 0.001f && extra <= 0) Add("Decisions", "icon_check", "No risky shortcuts taken with the client", 0);

        // 4. launch
        if (!string.IsNullOrEmpty(dep.date)) Add("Launch", "icon_calendar", "Rocket deployed to the pad (launch date " + dep.date + ")");
        Add("Launch", "icon_launch", "Launched on " + r.launchDate);
        if (rec.lines != null)
            foreach (var l in rec.lines.Where(x => x.sev > 0).Take(3)) Add("Launch", "icon_flag", l.text, l.sev);
        string res = rec.Result == LaunchResult.Success ? "Success" : rec.Result == LaunchResult.Partial ? "Partial success" : "Failure";
        Add("Result", "icon_star", res + (rec.Result == LaunchResult.Failure ? "" : "  -  " + rec.stars + " of 3 stars") + (rec.reward > 0 ? "  -  reward $" + UIKit.Money(rec.reward) : ""), rec.Result == LaunchResult.Success ? 0 : rec.Result == LaunchResult.Partial ? 1 : 2);
        if (!string.IsNullOrEmpty(rec.cause)) Add("Result", "icon_flag", rec.cause, rec.Result == LaunchResult.Success ? 0 : 1);

        cached = r; loaded = true;
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(r));
        Changed?.Invoke();
    }
}
