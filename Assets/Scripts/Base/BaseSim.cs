using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ProgramKind { Training, Testing }

/// <summary>A building the agency can own. Some exist from the start, the others must be constructed (money + time).</summary>
public class BuildingDef
{
    public string id, name, description;
    public float costM, days;
    public bool startBuilt;
}

/// <summary>A training or testing programme: costs money and game time, and needs a specific building.</summary>
public class ProgramDef
{
    public string id, name, description, effect, building, group;
    public ProgramKind kind;
    public float costM, days;
    public int level;                     // 1..n inside a group (Scientist I, II, III); 0 = one-off
}

/// <summary>The design data of the base: buildings and programmes. Change numbers here.</summary>
public static class BaseData
{
    public static readonly BuildingDef[] Buildings =
    {
        new BuildingDef { id = "LaunchPad", name = "Launch Pad", description = "Rockets stand here until launch day.", startBuilt = true },
        new BuildingDef { id = "MissionControl", name = "Mission Control", description = "Tracks every contract and flight.", startBuilt = true },
        new BuildingDef { id = "AdminOffice", name = "Admin Office", description = "Agency offices and the training academy.", startBuilt = true },
        new BuildingDef { id = "Hangar", name = "Hangar", description = "Rocket assembly hall and the pilot simulator.", costM = 150f, days = 60f },
        new BuildingDef { id = "FuelDepot", name = "Fuel Depot", description = "Fuel storage and the engine test stand.", costM = 90f, days = 45f },
        new BuildingDef { id = "RadarTower", name = "Radar Tower", description = "Tracking radar and the communications lab.", costM = 120f, days = 50f },
    };

    public static readonly ProgramDef[] Programs =
    {
        new ProgramDef { id = "sci1", group = "sci", level = 1, kind = ProgramKind.Training, name = "Scientist Training I", building = "AdminOffice", costM = 40f, days = 30f,
            description = "Teach your staff the basics of rocket science.", effect = "Tests finish 10% faster, +8% reputation gains" },
        new ProgramDef { id = "sci2", group = "sci", level = 2, kind = ProgramKind.Training, name = "Scientist Training II", building = "AdminOffice", costM = 80f, days = 60f,
            description = "Advanced propulsion and materials courses.", effect = "Tests finish 20% faster, +16% reputation gains" },
        new ProgramDef { id = "sci3", group = "sci", level = 3, kind = ProgramKind.Training, name = "Scientist Training III", building = "AdminOffice", costM = 160f, days = 90f,
            description = "A full research fellowship programme.", effect = "Tests finish 30% faster, +24% reputation gains" },
        new ProgramDef { id = "pilot1", group = "pilot", level = 1, kind = ProgramKind.Training, name = "Pilot Training I", building = "Hangar", costM = 60f, days = 45f,
            description = "Simulator hours for your first astronauts.", effect = "Qualifies a crew for capsule flights, +15 reputation" },
        new ProgramDef { id = "pilot2", group = "pilot", level = 2, kind = ProgramKind.Training, name = "Pilot Training II", building = "Hangar", costM = 120f, days = 75f,
            description = "Emergency drills and orbital rendezvous.", effect = "Safer crews (coming soon), +15 reputation" },
        new ProgramDef { id = "pilot3", group = "pilot", level = 3, kind = ProgramKind.Training, name = "Pilot Training III", building = "Hangar", costM = 240f, days = 120f,
            description = "Long-duration mission preparation.", effect = "Veteran crews (coming soon), +15 reputation" },
        new ProgramDef { id = "test_engine", kind = ProgramKind.Testing, name = "Engine Static-Fire Test", building = "FuelDepot", costM = 90f, days = 45f,
            description = "Burn every engine type on the test stand.", effect = "Rockets cost 4% less to build" },
        new ProgramDef { id = "test_struct", kind = ProgramKind.Testing, name = "Structural Load Test", building = "Hangar", costM = 70f, days = 35f,
            description = "Shake and stress the airframe.", effect = "Rockets cost 3% less to build" },
        new ProgramDef { id = "test_comms", kind = ProgramKind.Testing, name = "Comms Link Test", building = "RadarTower", costM = 60f, days = 30f,
            description = "Prove your antennas against the radar.", effect = "Contracts pay 10% more" },
    };

    public static BuildingDef Building(string id) => Buildings.FirstOrDefault(b => b.id == id);
    public static ProgramDef Program(string id) => Programs.FirstOrDefault(p => p.id == id);
    public static string BuildingName(string id) => Building(id)?.name ?? id;
}

[Serializable] public class BaseJob { public string id; public bool isBuild; public float remaining, total; }

[Serializable]
public class BaseSave
{
    public List<string> built = new List<string>(), done = new List<string>();
    public List<BaseJob> jobs = new List<BaseJob>();

    const string Key = "SpaceAgency.Base";
    static BaseSave cur;

    public static BaseSave Current
    {
        get
        {
            if (cur != null) return cur;
            try { cur = JsonUtility.FromJson<BaseSave>(PlayerPrefs.GetString(Key, "")); } catch { cur = null; }
            if (cur == null) cur = new BaseSave();
            if (cur.built.Count == 0 && cur.done.Count == 0 && cur.jobs.Count == 0)
                cur.built.AddRange(BaseData.Buildings.Where(b => b.startBuilt).Select(b => b.id));
            return cur;
        }
    }

    public static void Reload() => cur = null;
    public static void Reset() { PlayerPrefs.DeleteKey(Key); cur = null; }
    public static void Save() { if (cur != null) PlayerPrefs.SetString(Key, JsonUtility.ToJson(cur)); }

    public static bool Has(string programId) => Current.done.Contains(programId);
    public static int Level(string group) => BaseData.Programs.Count(p => p.group == group && Current.done.Contains(p.id));
}

/// <summary>
/// Runs construction and training / testing programmes against the game clock.
/// Everything costs money (Agency) and game time, and programmes need their building to be built and free.
/// </summary>
public class BaseSim : MonoBehaviour
{
    public static BaseSim Instance { get; private set; }
    public event Action Changed;
    float saveTimer;

    void Awake()
    {
        Instance = this;
        BaseSave.Reload();
    }

    void Start()
    {
        ApplyVisibility();
        Changed?.Invoke();
    }

    void OnDestroy()
    {
        BaseSave.Save();
        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit() => BaseSave.Save();

    BaseSave D => BaseSave.Current;

    // ---------------------------------------------------------------- queries
    public bool IsBuilt(string id) => D.built.Contains(id);
    public BaseJob Job(string id) => D.jobs.FirstOrDefault(j => j.id == id);
    public bool IsDone(string programId) => D.done.Contains(programId);
    public bool BuildingBusy(string buildingId) => D.jobs.Any(j => !j.isBuild && BaseData.Program(j.id)?.building == buildingId);
    public IEnumerable<BaseJob> RunningPrograms(string buildingId) => D.jobs.Where(j => !j.isBuild && BaseData.Program(j.id)?.building == buildingId);

    public static float Progress(BaseJob j) => j == null || j.total <= 0f ? 0f : 1f - j.remaining / j.total;
    public static int DaysLeft(BaseJob j) => j == null ? 0 : Mathf.CeilToInt(j.remaining / 86400f);

    /// <summary>Scientists finish testing programmes faster (10% per level, at most 50%).</summary>
    public static float TestTimeFactor => Mathf.Max(0.5f, 1f - 0.1f * BaseSave.Level("sci"));

    float DurationSeconds(ProgramDef p) => p.days * 86400f * (p.kind == ProgramKind.Testing ? TestTimeFactor : 1f);

    /// <summary>Why a programme cannot start right now (null = it can).</summary>
    public string WhyNot(ProgramDef p)
    {
        if (IsDone(p.id)) return "Completed";
        if (Job(p.id) != null) return "Running";
        if (p.level > 1)
        {
            var prev = BaseData.Programs.FirstOrDefault(x => x.group == p.group && x.level == p.level - 1);
            if (prev != null && !IsDone(prev.id)) return "Needs " + prev.name;
        }
        if (!IsBuilt(p.building)) return "Needs the " + BaseData.BuildingName(p.building);
        if (BuildingBusy(p.building)) return BaseData.BuildingName(p.building) + " is busy";
        if (!Agency.CanAfford(MoneyOf(p.costM))) return "Not enough money";
        return null;
    }

    static long MoneyOf(float costM) => (long)(costM * 1_000_000f);

    public string WhyNotBuild(BuildingDef b)
    {
        if (IsBuilt(b.id)) return "Built";
        if (Job(b.id) != null) return "Under construction";
        if (!Agency.CanAfford(MoneyOf(b.costM))) return "Not enough money";
        return null;
    }

    // ---------------------------------------------------------------- actions
    public bool StartProgram(ProgramDef p)
    {
        if (WhyNot(p) != null || !Agency.TrySpend(MoneyOf(p.costM))) return false;
        float secs = DurationSeconds(p);
        D.jobs.Add(new BaseJob { id = p.id, isBuild = false, remaining = secs, total = secs });
        Commit();
        Hud.Toast(p.name + " started (" + Mathf.CeilToInt(secs / 86400f) + " days)");
        return true;
    }

    public bool StartBuild(BuildingDef b)
    {
        if (WhyNotBuild(b) != null || !Agency.TrySpend(MoneyOf(b.costM))) return false;
        float secs = b.days * 86400f;
        D.jobs.Add(new BaseJob { id = b.id, isBuild = true, remaining = secs, total = secs });
        Commit();
        Hud.Toast("Construction of the " + b.name + " started (" + Mathf.CeilToInt(b.days) + " days)");
        return true;
    }

    void Commit()
    {
        BaseSave.Save();
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- time
    void Update()
    {
        var clock = GameClock.Instance;
        if (clock == null || D.jobs.Count == 0) return;
        float dt = clock.GameDeltaSeconds;
        if (dt <= 0f) return;

        bool finished = false;
        for (int i = D.jobs.Count - 1; i >= 0; i--)
        {
            var j = D.jobs[i];
            j.remaining -= dt;
            if (j.remaining > 0f) continue;
            D.jobs.RemoveAt(i);
            Complete(j);
            finished = true;
        }
        saveTimer += Time.unscaledDeltaTime;
        if (finished) Commit();
        else if (saveTimer > 10f) { saveTimer = 0f; BaseSave.Save(); }
    }

    void Complete(BaseJob j)
    {
        if (j.isBuild)
        {
            if (!D.built.Contains(j.id)) D.built.Add(j.id);
            ApplyVisibility();
            Hud.Toast("The " + BaseData.BuildingName(j.id) + " is finished");
            return;
        }
        var p = BaseData.Program(j.id);
        if (p == null) return;
        if (!D.done.Contains(p.id)) D.done.Add(p.id);
        Agency.AddReputation(p.kind == ProgramKind.Training ? 15 : 10);
        Hud.Toast(p.name + " complete: " + p.effect);
    }

    /// <summary>Buildings that are not built yet are not in the scene.</summary>
    public void ApplyVisibility()
    {
        var root = GameObject.Find("Base Buildings");
        if (root == null) return;
        foreach (Transform c in root.transform) c.gameObject.SetActive(IsBuilt(c.name));
    }
}
