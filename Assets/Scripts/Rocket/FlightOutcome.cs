using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public enum LaunchResult { Success, Partial, Failure }

[Serializable] public class FlightLine { public string text; public int sev; }     // sev: 0 ok, 1 close, 2 failed

/// <summary>How a launch went and what comes after it: the result, the star rating, the reward and the flight that follows.</summary>
[Serializable]
public class FlightRecord
{
    public string missionId, missionName, rocketName, startDate, cause;
    public int result, stars;
    public long reward;
    public float durationDays;
    public List<FlightLine> lines = new List<FlightLine>();
    public LaunchResult Result => (LaunchResult)result;
}

/// <summary>Works out whether a launch succeeds, partly succeeds or fails, from the rocket design and the contract. Nothing blocks a launch: weak rockets just fail more often.</summary>
public static class FlightOutcome
{
    public static FlightRecord Evaluate(Deployment dep, PartLibrary lib, MissionDefinition mission)
    {
        var rec = new FlightRecord { missionId = dep.missionId, missionName = dep.missionName, rocketName = dep.rocketName };
        var design = RocketDesign.FromJson(dep.design);
        design.stack.RemoveAll(id => lib.Get(id) == null);
        var rep = RocketStats.Evaluate(design, lib, mission);
        var core = RocketRules.MissingCore(design, lib);

        int errs = rep.checks.Count(c => c.sev == Sev.Error), warns = rep.checks.Count(c => c.sev == Sev.Warn);
        int contractErrs = rep.checks.Count(c => c.contract && c.sev == Sev.Error);
        float rel = rep.reliability > 0f ? rep.reliability : 80f;
        float pf = 0.02f + (100f - rel) / 100f * 0.5f + 0.09f * errs + 0.03f * warns + (rep.stability < 40f ? 0.12f : 0f);
        pf *= 1f - 0.06f * (Agency.Stat(3) - 1);                              // the Safety stat: fewer failures
        pf = Mathf.Clamp(pf, 0.02f, 0.92f);

        string cause = null;
        if (core.Count > 0) cause = "The rocket was missing " + string.Join(", ", core) + " and never left the pad.";
        else if (rep.liftoffTwr < 1f) cause = "The engines were too weak to lift the rocket off the pad.";
        else if (UnityEngine.Random.value < pf)
        {
            float roll = UnityEngine.Random.value;
            cause = roll < 0.4f && rel < 92f ? "Engine failure during ascent."
                  : roll < 0.7f && rep.stability < 70f ? "The rocket lost control and tumbled."
                  : "Structural failure at maximum dynamic pressure.";
        }
        bool anomaly = cause == null && UnityEngine.Random.value < pf * 0.8f;

        LaunchResult res;
        if (cause != null) res = LaunchResult.Failure;
        else if (contractErrs > 0 || anomaly) res = LaunchResult.Partial;
        else res = LaunchResult.Success;
        rec.result = (int)res; rec.cause = cause;

        // ---- what the player sees: the flight in steps, then the contract's requirements
        var L = rec.lines;
        void Line(string t, int sev) => L.Add(new FlightLine { text = t, sev = sev });
        if (res == LaunchResult.Failure && core.Count > 0) Line("Liftoff", 2);
        else
        {
            Line("Liftoff", res == LaunchResult.Failure && rep.liftoffTwr < 1f ? 2 : 0);
            if (rep.liftoffTwr >= 1f)
            {
                Line("Ascent", res == LaunchResult.Failure ? 2 : anomaly ? 1 : 0);
                if (res != LaunchResult.Failure) Line("Orbit", rep.dvTotal >= rep.dvRequired || mission == null ? 0 : 1);
            }
        }
        foreach (var c in rep.checks.Where(c => c.contract)) Line(c.text, c.sev == Sev.Ok ? 0 : c.sev == Sev.Warn ? 1 : 2);
        if (anomaly && cause == null && contractErrs == 0) rec.cause = "A minor anomaly cost some of the mission's value.";
        else if (res == LaunchResult.Partial && contractErrs > 0) rec.cause = contractErrs + " contract requirement" + (contractErrs == 1 ? " was" : "s were") + " not met.";

        // ---- stars and reward
        int stars;
        if (res == LaunchResult.Failure) stars = 0;
        else if (res == LaunchResult.Success) stars = errs == 0 && warns == 0 && rel >= 90f ? 3 : 2;
        else stars = contractErrs <= 1 && !anomaly ? 2 : 1;
        rec.stars = stars;
        float mult = res == LaunchResult.Failure ? 0f
            : res == LaunchResult.Success ? (stars == 3 ? 1.3f : 1.0f)
            : (stars == 2 ? 0.6f : 0.4f);
        rec.reward = mission != null ? (long)(mission.budget * Agency.RewardFactor * mult) : 0L;

        // ---- how long the flight takes (game days)
        string target = mission != null ? (mission.target ?? "").ToLowerInvariant() : "";
        rec.durationDays = res == LaunchResult.Failure ? 0f : target.Contains("mars") ? 20f : target.Contains("moon") || target.Contains("lunar") ? 8f : target.Contains("asteroid") ? 14f : mission == null ? 2f : 4f;
        return rec;
    }

    public static string StarWord(int stars) => stars >= 3 ? "Outstanding" : stars == 2 ? "Good" : stars == 1 ? "Fair" : "Failed";
}

/// <summary>Runs the mission that follows a launch: after a successful or partly successful launch the rocket flies for some days
/// (shown as a bar under the Objectives button), then the reward is paid and the result window opens.</summary>
public class FlightManager : MonoBehaviour
{
    public static FlightManager Instance { get; private set; }
    const string Key = "SpaceAgency.Flight";

    public FlightRecord Current { get; private set; }
    public event Action Changed;
    public static void ResetStatics() { Instance = null; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        try { var r = JsonUtility.FromJson<FlightRecord>(PlayerPrefs.GetString(Key, "")); if (r != null && !string.IsNullOrEmpty(r.startDate) && r.durationDays > 0f) Current = r; }
        catch { /* nothing saved */ }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public float Progress
    {
        get
        {
            var clock = GameClock.Instance;
            if (Current == null || clock == null || !DateTime.TryParse(Current.startDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start)) return 0f;
            return Mathf.Clamp01((float)((clock.Now - start).TotalDays / Mathf.Max(0.1f, Current.durationDays)));
        }
    }

    public int DaysLeft => Current == null ? 0 : Mathf.CeilToInt(Current.durationDays * (1f - Progress));

    /// <summary>The rocket has been launched (called by the launch cutscene when it ends).</summary>
    public void Launched(FlightRecord rec)
    {
        var mm = MissionManager.Instance;
        if (mm != null && !string.IsNullOrEmpty(rec.missionId)) mm.DetachForFlight(rec.missionId);
        if (rec.Result == LaunchResult.Failure)
        {
            Agency.AddReputation(-8);
            LaunchResultUI.Show(rec, false);
            return;
        }
        var clock = GameClock.Instance;
        rec.startDate = (clock != null ? clock.Now : DateTime.Now).ToString("o", CultureInfo.InvariantCulture);
        Current = rec;
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(rec));
        Changed?.Invoke();
        LaunchResultUI.Show(rec, false);
    }

    void Update()
    {
        if (Current == null || Progress < 1f) return;
        var rec = Current;
        Current = null;
        PlayerPrefs.DeleteKey(Key);
        Agency.Earn(rec.reward);
        Agency.AddReputation(rec.Result == LaunchResult.Success ? 10 + rec.stars * 5 : 5);
        Changed?.Invoke();
        LaunchResultUI.Show(rec, true);
    }
}
