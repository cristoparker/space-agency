using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ProgramKind { Training, Testing }

/// <summary>
/// A facility the agency can own. Level 1 is the small starter version, levels 2 and 3 are bigger and more scientific.
/// Each level costs money and game time: costM[0] / days[0] build level 1, costM[1] / days[1] upgrade to level 2, and so on.
/// </summary>
public class BuildingDef
{
    public string id, name, description;
    public float[] costM = new float[0], days = new float[0];
    public int startLevel;                          // 0 = not built yet
    public bool needsPlot;                          // built by the player on one of the empty plots (P1..P5)
    public bool listed = true;                      // shown in the Facility window
    public float[] powerMW = new float[0];          // power the building draws at each level (index 0 = level 1)
    public string role;                             // one line: what the building does for the agency
    public int MaxLevel => Mathf.Max(startLevel, costM.Length);
    public static readonly string[] LevelNames = { "Empty plot", "Starter", "Expanded", "Complete" };
}

/// <summary>A training or testing programme: costs money and game time, and needs a specific building.</summary>
public class ProgramDef
{
    public string id, name, description, effect, building, group;
    public int minLevel = 1;               // the building must be at least this level
    public ProgramKind kind;
    public float costM, days;
    public int level;                     // 1..n inside a group (Scientist I, II, III); 0 = one-off
}

/// <summary>The design data of the base: buildings and programmes. Change numbers here.</summary>
public static class BaseData
{
    /// <summary>Plots on the map. Index 0 = P1 ... 4 = P5 (the order of the site sketch).</summary>
    public const int PlotCount = 5;

    public static readonly BuildingDef[] Buildings =
    {
        // pre-built, fixed place
        new BuildingDef { id = "LaunchPad", name = "Launch Complex", description = "Pad, flame trench and a 100 m service tower. Rockets stand here until launch day.", startLevel = 1, listed = false, costM = new float[] { 0f } , powerMW = new[] { 8f }, role = "Launches your rockets." },
        new BuildingDef { id = "AdminOffice", name = "Office", description = "Agency headquarters and the training academy.", startLevel = 1,
            costM = new[] { 0f, 120f, 400f }, days = new[] { 0f, 6f, 8f } , powerMW = new[] { 4f, 8f, 14f }, role = "Trains your staff and runs the agency." },
        new BuildingDef { id = "PowerGrid", name = "Power Grid", description = "Substation, solar and batteries. No facility can be upgraded past the grid's level.", startLevel = 1,
            costM = new[] { 0f, 200f, 520f }, days = new[] { 0f, 7f, 9f } , powerMW = new[] { 0f, 0f, 0f }, role = "Makes the power every building needs." },
        new BuildingDef { id = "CarParkA", name = "Car Park A", description = "Visitor and contractor parking.", startLevel = 1, costM = new float[] { 0f }, listed = false , powerMW = new[] { 1f }, role = "Parking." },
        new BuildingDef { id = "CarParkB", name = "Car Park B", description = "Staff and delivery parking.", startLevel = 1, costM = new float[] { 0f }, listed = false , powerMW = new[] { 1f }, role = "Parking." },
        new BuildingDef { id = "StaffHousing", name = "Staff Housing", description = "Dormitories and a community hall for your people.", startLevel = 1, costM = new float[] { 0f }, listed = false , powerMW = new[] { 3f }, role = "Homes for your staff." },
        // built by the player on any empty plot
        new BuildingDef { id = "MissionControl", name = "Mission Control", description = "Tracks every contract and flight. Bigger rooms, more dishes.", needsPlot = true,
            costM = new[] { 180f, 300f, 700f }, days = new[] { 5f, 6f, 8f } , powerMW = new[] { 8f, 16f, 30f }, role = "Follows every contract and flight." },
        new BuildingDef { id = "Hangar", name = "Assembly Hangar", description = "Rocket assembly hall and the pilot simulator.", needsPlot = true,
            costM = new[] { 150f, 350f, 900f }, days = new[] { 7f, 8f, 9f } , powerMW = new[] { 9f, 18f, 34f }, role = "Builds rockets and trains pilots." },
        new BuildingDef { id = "FuelDepot", name = "Fuel Depot", description = "Propellant storage: tanks, spheres and pipe racks.", needsPlot = true,
            costM = new[] { 90f, 220f, 600f }, days = new[] { 5f, 6f, 7f } , powerMW = new[] { 5f, 10f, 20f }, role = "Stores propellant for bigger rockets." },
        new BuildingDef { id = "RadarTower", name = "Tracking Station", description = "Dishes and radar that follow your rockets and satellites.", needsPlot = true,
            costM = new[] { 120f, 260f, 700f }, days = new[] { 6f, 7f, 8f } , powerMW = new[] { 7f, 14f, 26f }, role = "Tracks your rockets and satellites." },
        new BuildingDef { id = "ResearchLab", name = "Research Lab", description = "Scientists, observatory dome and a wind tunnel.", needsPlot = true,
            costM = new[] { 140f, 320f, 800f }, days = new[] { 7f, 8f, 9f } , powerMW = new[] { 9f, 18f, 32f }, role = "Research and structural tests." },
        new BuildingDef { id = "TestStand", name = "Engine Test Stand", description = "Static-fire stand with a flame deflector and a blast wall.", needsPlot = true,
            costM = new[] { 110f, 260f, 650f }, days = new[] { 6f, 7f, 8f } , powerMW = new[] { 8f, 16f, 30f }, role = "Fires engines before launch day." },
    };

    public static readonly ProgramDef[] Programs =
    {
        new ProgramDef { id = "sci1", group = "sci", level = 1, kind = ProgramKind.Training, name = "Scientist Training I", building = "AdminOffice", costM = 40f, days = 6f,
            description = "Teach your staff the basics of rocket science.", effect = "Tests finish 10% faster, +8% reputation gains" },
        new ProgramDef { id = "sci2", group = "sci", level = 2, kind = ProgramKind.Training, name = "Scientist Training II", building = "AdminOffice", minLevel = 2, costM = 80f, days = 8f,
            description = "Advanced propulsion and materials courses.", effect = "Tests finish 20% faster, +16% reputation gains" },
        new ProgramDef { id = "sci3", group = "sci", level = 3, kind = ProgramKind.Training, name = "Scientist Training III", building = "AdminOffice", minLevel = 3, costM = 160f, days = 10f,
            description = "A full research fellowship programme.", effect = "Tests finish 30% faster, +24% reputation gains" },
        new ProgramDef { id = "pilot1", group = "pilot", level = 1, kind = ProgramKind.Training, name = "Pilot Training I", building = "Hangar", costM = 60f, days = 7f,
            description = "Simulator hours for your first astronauts.", effect = "Qualifies a crew for capsule flights, +15 reputation" },
        new ProgramDef { id = "pilot2", group = "pilot", level = 2, kind = ProgramKind.Training, name = "Pilot Training II", building = "Hangar", minLevel = 2, costM = 120f, days = 9f,
            description = "Emergency drills and orbital rendezvous.", effect = "Safer crews (coming soon), +15 reputation" },
        new ProgramDef { id = "pilot3", group = "pilot", level = 3, kind = ProgramKind.Training, name = "Pilot Training III", building = "Hangar", minLevel = 3, costM = 240f, days = 12f,
            description = "Long-duration mission preparation.", effect = "Veteran crews (coming soon), +15 reputation" },
        new ProgramDef { id = "test_engine", kind = ProgramKind.Testing, name = "Engine Static-Fire Test", building = "TestStand", costM = 90f, days = 6f,
            description = "Burn every engine type on the test stand.", effect = "Rockets cost 4% less to build" },
        new ProgramDef { id = "test_struct", kind = ProgramKind.Testing, name = "Structural Load Test", building = "ResearchLab", costM = 70f, days = 5f,
            description = "Shake and stress the airframe.", effect = "Rockets cost 3% less to build" },
        new ProgramDef { id = "test_comms", kind = ProgramKind.Testing, name = "Comms Link Test", building = "RadarTower", costM = 60f, days = 5f,
            description = "Prove your antennas against the radar.", effect = "Contracts pay 10% more" },
    };

    /// <summary>Power the grid makes at each grid level (MW).</summary>
    public static readonly float[] GridSupplyMW = { 40f, 110f, 260f };

    /// <summary>Rocket parts that need a building level. Parts that are not listed here are free from the start.</summary>
    public class PartUnlock { public string building; public int level; public string label; public string[] prefixes; }
    public static readonly PartUnlock[] PartUnlocks =
    {
        new PartUnlock { building = "TestStand", level = 1, label = "Medium engines", prefixes = new[] { "Eng_M_Lifter", "Eng_M_Cluster3" } },
        new PartUnlock { building = "TestStand", level = 2, label = "Large engines", prefixes = new[] { "Eng_L_Cluster4" } },
        new PartUnlock { building = "FuelDepot", level = 1, label = "Vacuum engines", prefixes = new[] { "Eng_S_Vac", "Eng_M_Vac" } },
        new PartUnlock { building = "FuelDepot", level = 2, label = "Large vacuum engine, RCS", prefixes = new[] { "Eng_L_Vac", "Thr_RCS_Block" } },
        new PartUnlock { building = "FuelDepot", level = 3, label = "Ion thruster", prefixes = new[] { "Thr_Ion" } },
        new PartUnlock { building = "ResearchLab", level = 1, label = "Medium tanks and boosters", prefixes = new[] { "Tank_M_", "Booster_S", "Booster_M" } },
        new PartUnlock { building = "ResearchLab", level = 2, label = "Large tanks and boosters", prefixes = new[] { "Tank_L_", "Booster_L" } },
        new PartUnlock { building = "ResearchLab", level = 3, label = "RTG power", prefixes = new[] { "Pow_RTG" } },
        new PartUnlock { building = "Hangar", level = 1, label = "Medium fairing and capsule", prefixes = new[] { "Head_Fairing_M", "Head_Capsule_M" } },
        new PartUnlock { building = "Hangar", level = 2, label = "Large fairing and capsule", prefixes = new[] { "Head_Fairing_L", "Head_Capsule_L" } },
        new PartUnlock { building = "Hangar", level = 3, label = "Rover and lander payloads", prefixes = new[] { "Pay_Rover", "Pay_Lander" } },
        new PartUnlock { building = "RadarTower", level = 1, label = "Dish antennas", prefixes = new[] { "Com_Dish_S", "Com_Dish_L" } },
        new PartUnlock { building = "RadarTower", level = 2, label = "Phased array antenna", prefixes = new[] { "Com_PhasedArray" } },
        new PartUnlock { building = "MissionControl", level = 1, label = "Landing legs and parachutes", prefixes = new[] { "Rec_LandingLeg", "Rec_Parachute_M" } },
        new PartUnlock { building = "MissionControl", level = 2, label = "Large solar panels", prefixes = new[] { "Pow_Solar_L" } },
    };

    /// <summary>The building level a rocket part needs, or null when the part is free.</summary>
    public static PartUnlock UnlockFor(string partId)
    {
        foreach (var u in PartUnlocks) foreach (var pre in u.prefixes) if (partId == pre || (pre.EndsWith("_") && partId.StartsWith(pre))) return u;
        return null;
    }

    /// <summary>Everything a building level unlocks: programmes (training / testing) and rocket part groups.</summary>
    public static System.Collections.Generic.List<string> UnlocksAt(string buildingId, int level)
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var p in Programs) if (p.building == buildingId && p.minLevel == level) list.Add(p.name);
        foreach (var u in PartUnlocks) if (u.building == buildingId && u.level == level) list.Add("Parts: " + u.label);
        if (buildingId == "PowerGrid" && level >= 1 && level <= GridSupplyMW.Length) list.Add(GridSupplyMW[level - 1].ToString("0") + " MW of power");
        return list;
    }

    public static float PowerOf(BuildingDef b, int level) => b.powerMW == null || b.powerMW.Length == 0 || level < 1 ? 0f : b.powerMW[Mathf.Clamp(level - 1, 0, b.powerMW.Length - 1)];

    public static BuildingDef Building(string id) => Buildings.FirstOrDefault(b => b.id == id);
    public static ProgramDef Program(string id) => Programs.FirstOrDefault(p => p.id == id);
    public static string BuildingName(string id) => Building(id)?.name ?? id;
}

[Serializable] public class BaseJob { public string id; public bool isBuild; public int level, plot = -1; public float remaining, total; }

[Serializable]
public class BaseSave
{
    [Serializable] public class LevelEntry { public string id; public int level; }
    public List<LevelEntry> levels = new List<LevelEntry>();
    public List<string> done = new List<string>();
    public List<BaseJob> jobs = new List<BaseJob>();
    [Serializable] public class PlotEntry { public int plot; public string id; }
    public List<PlotEntry> plots = new List<PlotEntry>();            // which facility stands (or is being built) on which plot

    public string FacilityAt(int plot) { var e = plots.FirstOrDefault(x => x.plot == plot); return e == null ? null : e.id; }
    public int PlotOf(string id) { var e = plots.FirstOrDefault(x => x.id == id); return e == null ? -1 : e.plot; }

    public int LevelOf(string id) { var e = levels.FirstOrDefault(x => x.id == id); return e == null ? 0 : e.level; }
    public void SetLevel(string id, int level)
    {
        var e = levels.FirstOrDefault(x => x.id == id);
        if (e == null) levels.Add(new LevelEntry { id = id, level = level }); else e.level = level;
    }

    const string Key = "SpaceAgency.Base";
    static BaseSave cur;

    public static BaseSave Current
    {
        get
        {
            if (cur != null) return cur;
            try { cur = JsonUtility.FromJson<BaseSave>(PlayerPrefs.GetString(Key, "")); } catch { cur = null; }
            if (cur == null) cur = new BaseSave();
            if (cur.levels.Count == 0 && cur.done.Count == 0 && cur.jobs.Count == 0)
                foreach (var b in BaseData.Buildings) if (b.startLevel > 0) cur.SetLevel(b.id, b.startLevel);
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
    /// <summary>A construction (level 1) or an upgrade finished: (building id, new level).</summary>
    public event Action<string, int> BuildFinished;
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
    public int LevelOf(string id) => D.LevelOf(id);
    public string FacilityAt(int plot) => D.FacilityAt(plot);
    public int PlotOf(string id) => D.PlotOf(id);
    public int FreePlot() { for (int i = 0; i < BaseData.PlotCount; i++) if (D.FacilityAt(i) == null) return i; return -1; }
    public bool PlotFree(int i) => D.FacilityAt(i) == null;
    public int PowerLevel => D.LevelOf("PowerGrid");
    public bool IsBuilt(string id) => D.LevelOf(id) > 0;
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
        // a test needs a rocket to test: the Rocket Designer must have produced a valid rocket first (training has no such need)
        if (p.kind == ProgramKind.Testing && !MissionObjectives.HasRocketDesign()) return "Build a rocket first";
        if (LevelOf(p.building) < p.minLevel)
            return p.minLevel <= 1 ? "Needs the " + BaseData.BuildingName(p.building) : "Needs " + BaseData.BuildingName(p.building) + " level " + p.minLevel;
        if (BuildingBusy(p.building)) return BaseData.BuildingName(p.building) + " is busy";
        if (!Agency.CanAfford(MoneyOf(p.costM))) return "Not enough money";
        return null;
    }

    static long MoneyOf(float costM) => (long)(costM * 1_000_000f);

    /// <summary>Why the next level of a building cannot be started (null = it can). For a new facility pass the plot it would go on (-1 = any free one).</summary>
    public string WhyNotBuild(BuildingDef b, int plot = -1)
    {
        int next = LevelOf(b.id) + 1;
        if (next > b.MaxLevel) return "Fully upgraded";
        if (Job(b.id) != null) return "Under construction";
        if (b.needsPlot && next == 1)
        {
            if (plot >= 0 ? !PlotFree(plot) : FreePlot() < 0) return "No free plot";
        }
        if (b.id != "PowerGrid" && next > PowerLevel) return "Needs Power Grid level " + next;
        if (b.id != "PowerGrid" && PowerDemand - BaseData.PowerOf(b, next - 1) + BaseData.PowerOf(b, next) > PowerSupply + 0.01f) return "Not enough power";
        if (!Agency.CanAfford(MoneyOf(CostOf(b, next)))) return "Not enough money";
        return null;
    }

    /// <summary>Power the grid makes (MW) at its current level.</summary>
    public float PowerSupply => BaseData.GridSupplyMW[Mathf.Clamp(PowerLevel, 1, BaseData.GridSupplyMW.Length) - 1];

    /// <summary>Power all finished buildings draw (MW).</summary>
    public float PowerDemand
    {
        get
        {
            float sum = 0f;
            foreach (var b in BaseData.Buildings)
            {
                float p = BaseData.PowerOf(b, LevelOf(b.id));
                var job = Job(b.id);
                if (job != null && job.isBuild) p = Mathf.Max(p, BaseData.PowerOf(b, job.level));        // power is reserved from the moment construction starts
                sum += p;
            }
            return sum;
        }
    }

    /// <summary>True when the building level a rocket part needs is built (parts not tied to a building are always free).</summary>
    public bool IsPartUnlocked(string partId)
    {
        var u = BaseData.UnlockFor(partId);
        return u == null || LevelOf(u.building) >= u.level;
    }

    public static float CostOf(BuildingDef b, int level) => b.costM == null || b.costM.Length == 0 ? 0f : b.costM[Mathf.Clamp(level - 1, 0, b.costM.Length - 1)];
    public static float DaysOf(BuildingDef b, int level) => b.days == null || b.days.Length == 0 ? 0f : b.days[Mathf.Clamp(level - 1, 0, b.days.Length - 1)];

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

    public bool StartBuild(BuildingDef b, int plot = -1)
    {
        if (WhyNotBuild(b, plot) != null) return false;
        int next = LevelOf(b.id) + 1;
        if (b.needsPlot && next == 1)
        {
            if (plot < 0) plot = FreePlot();
            D.plots.Add(new BaseSave.PlotEntry { plot = plot, id = b.id });         // the plot is reserved while it is being built
        }
        else plot = D.PlotOf(b.id);
        if (!Agency.TrySpend(MoneyOf(CostOf(b, next)))) return false;
        float days = DaysOf(b, next), secs = days * 86400f;
        D.jobs.Add(new BaseJob { id = b.id, isBuild = true, level = next, plot = plot, remaining = secs, total = secs });
        Commit();
        Hud.Toast((next == 1 ? "Construction of the " : "Upgrade of the ") + b.name + (plot >= 0 ? " on P" + (plot + 1) : "") + " started (" + Mathf.CeilToInt(days) + " days)");
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
            D.SetLevel(j.id, Mathf.Max(j.level, D.LevelOf(j.id)));
            ApplyVisibility();
            Hud.Toast("The " + BaseData.BuildingName(j.id) + " is now level " + D.LevelOf(j.id));
            BuildFinished?.Invoke(j.id, D.LevelOf(j.id));
            return;
        }
        var p = BaseData.Program(j.id);
        if (p == null) return;
        if (!D.done.Contains(p.id)) D.done.Add(p.id);
        Agency.AddReputation(p.kind == ProgramKind.Training ? 15 : 10);
        Hud.Toast(p.name + " complete: " + p.effect);
    }

    FacilityCatalog catalog;
    readonly Dictionary<int, string> shown = new Dictionary<int, string>();

    /// <summary>
    /// Fixed buildings ("Base Buildings/*") have children "L1", "L2", "L3": only the one matching the level is shown.
    /// Plots ("Base Plots/Plot1..5") show their empty "Marker" until a facility is built there; then the facility model of the
    /// right level is created from the <see cref="FacilityCatalog"/>.
    /// </summary>
    public void ApplyVisibility()
    {
        var root = GameObject.Find("Base Buildings");
        if (root != null)
            foreach (Transform c in root.transform)
            {
                int level = LevelOf(c.name);
                foreach (Transform child in c)
                {
                    bool on = child.name == "L" + level;
                    if (child.gameObject.activeSelf != on) child.gameObject.SetActive(on);
                }
            }

        var plots = GameObject.Find("Base Plots");
        if (plots == null) return;
        if (catalog == null) catalog = Resources.Load<FacilityCatalog>("FacilityCatalog");
        for (int i = 0; i < plots.transform.childCount; i++)
        {
            var plot = plots.transform.GetChild(i);
            string id = D.FacilityAt(i);
            int level = id != null ? LevelOf(id) : 0;
            string key = level > 0 ? id + ":" + level : "";
            shown.TryGetValue(i, out var cur);
            if (cur == key && (key == "" || plot.Find("Facility") != null)) { SetMarker(plot, key == ""); continue; }
            var old = plot.Find("Facility");
            if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            if (key != "" && catalog != null)
            {
                var go = catalog.Spawn(id, level, plot);
                if (go != null) go.name = "Facility";
            }
            shown[i] = key;
            SetMarker(plot, key == "");
        }
    }

    static void SetMarker(Transform plot, bool on)
    {
        var m = plot.Find("Marker");                              // the paved plot stays under the finished facility
        if (m != null && !m.gameObject.activeSelf) m.gameObject.SetActive(true);
    }
}
