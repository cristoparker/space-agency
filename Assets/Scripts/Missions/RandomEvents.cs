using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>One answer to an event. Money and days are paid when it is chosen; risk changes the chance that this mission's launch goes wrong.</summary>
public class EventChoice
{
    public string label;
    public string[] benefits = new string[0], costs = new string[0], risks = new string[0];
    public long cost, gain, extraBudget;       // money paid now / money received now / extra reward the client adds at the end
    public float days;                         // game days lost (positive) or won (negative) on this mission's deadline
    public float risk;                         // added to the chance of a launch failure (negative = safer)
    public string program;                     // client request: this training or test must be done before launch
    public int trust;                          // client trust: each point changes future rewards by 2 %
}

public class EventDef
{
    public string id, title, body;
    public bool client;
    public List<EventChoice> choices = new List<EventChoice>();
}

/// <summary>
/// Random events during a contract: about three mission events (a fault, a sponsor, a better part, advice, a setback) and exactly one
/// client event, spread over the contract's time. The player decides each one in a popup; every choice has a cost, a benefit and a risk.
/// A declined event never comes back. Accepting a client request adds an objective to the mission.
/// </summary>
public class RandomEvents : MonoBehaviour
{
    public static RandomEvents Instance { get; private set; }
    const string TrustKey = "SpaceAgency.ClientTrust";

    class State
    {
        public List<(float at, EventDef def)> queue = new List<(float, EventDef)>();
        public float launchRisk;
        public long extraBudget;
        public List<string> requested = new List<string>();
    }
    readonly Dictionary<string, State> states = new Dictionary<string, State>();
    float timer;

    public static void ResetStatics() { Instance = null; }
    public static int Trust { get => PlayerPrefs.GetInt(TrustKey, 0); set => PlayerPrefs.SetInt(TrustKey, Mathf.Clamp(value, -10, 10)); }
    /// <summary>Future rewards change by 2 % per point of client trust (between 0.85 and 1.1).</summary>
    public static float RewardFactor => Mathf.Clamp(1f + 0.02f * Trust, 0.85f, 1.1f);

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        var mm = MissionManager.Instance;
        if (mm == null) return;
        mm.MissionStarted += Schedule;
        foreach (var m in mm.Running) Schedule(m);
    }

    // ---- what the rest of the game asks
    public static float LaunchRisk(string missionId) => Instance != null && Instance.states.TryGetValue(missionId ?? "", out var s) ? s.launchRisk : 0f;
    public static long ExtraBudget(string missionId) => Instance != null && Instance.states.TryGetValue(missionId ?? "", out var s) ? s.extraBudget : 0L;
    public static IEnumerable<string> RequestedPrograms(string missionId) => Instance != null && Instance.states.TryGetValue(missionId ?? "", out var s) ? s.requested : Enumerable.Empty<string>();

    // ---- scheduling: three mission events and one client event at random points of the contract
    void Schedule(MissionInstance m)
    {
        var id = m.Definition.id;
        if (states.ContainsKey(id)) return;
        var st = new State();
        var pool = MissionEvents(m.Definition).OrderBy(_ => UnityEngine.Random.value).Take(3).ToList();
        var times = new List<float>();
        while (times.Count < 3)
        {
            float t = UnityEngine.Random.Range(0.12f, 0.85f);
            if (times.All(x => Mathf.Abs(x - t) > 0.12f)) times.Add(t);
        }
        times.Sort();
        for (int i = 0; i < 3; i++) st.queue.Add((times[i], pool[i]));
        st.queue.Add((UnityEngine.Random.Range(0.2f, 0.6f), ClientEvent(m.Definition)));
        st.queue = st.queue.OrderBy(q => q.at).ToList();
        states[id] = st;
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < 0.5f) return;
        timer = 0f;
        if (TeamSync.Mirror) return;                                      // in a shared game only the host rolls events; everyone sees them
        var mm = MissionManager.Instance;
        if (mm == null || mm.Running.Count == 0) return;
        if (ImpactCard.IsOpen || EventCard.IsOpen || LaunchSequence.Running || CinematicCamera.Active || LaunchResultUI.IsOpen) return;
        var m = mm.Running[0];
        if (!states.TryGetValue(m.Definition.id, out var st) || st.queue.Count == 0) return;
        if (m.Progress < st.queue[0].at) return;
        var def = st.queue[0].def; st.queue.RemoveAt(0);          // each event is shown once and never returns
        Show(m, st, def);
    }

    void Show(MissionInstance m, State st, EventDef def)
    {
        var rows = new List<EventCard.Row>();
        foreach (var c in def.choices)
        {
            var ch = c;
            bool afford = ch.cost <= 0 || Agency.CanAfford(ch.cost);
            rows.Add(new EventCard.Row { label = ch.label, benefits = ch.benefits, costs = ch.costs, risks = ch.risks, enabled = afford, note = afford ? null : "Not enough money", onPick = () => Apply(m, st, ch) });
        }
        string kicker = def.client ? "Call from the client" : "Mission event";
        if (TeamSync.HostShowEvent(m.Definition.id + ":" + def.id, kicker, def.title, def.body, def.client, rows)) return;   // shared game: the whole team sees it
        EventCard.Show(kicker, def.title, def.body, def.client, rows);
    }

    void Apply(MissionInstance m, State st, EventChoice c)
    {
        if (c.cost > 0 && !Agency.TrySpend(c.cost)) { Hud.Toast("Not enough money."); return; }
        if (c.gain > 0) Agency.Earn(c.gain);
        if (c.days != 0f) m.ShiftSeconds += c.days * 86400.0;
        st.launchRisk += c.risk;
        st.extraBudget += c.extraBudget;
        if (!string.IsNullOrEmpty(c.program) && !st.requested.Contains(c.program)) st.requested.Add(c.program);
        if (c.trust != 0) Trust += c.trust;
        MissionManager.Instance?.NotifyChanged();
    }

    // ---- event content
    static long M(float millions) => (long)(millions * 1_000_000f);
    static string Money(long v) => "$" + UIKit.Money(v);

    static List<EventDef> MissionEvents(MissionDefinition d)
    {
        float b = d.budget / 1e6f;                               // the contract's budget in $M: events scale with it
        long fix = M(Mathf.Max(4f, b * 0.03f)), sponsor = M(Mathf.Max(6f, b * 0.05f)), bracket = M(Mathf.Max(3f, b * 0.015f)), repair = M(Mathf.Max(5f, b * 0.025f)), patch = M(Mathf.Max(1.5f, b * 0.008f));
        var list = new List<EventDef>();

        var e = new EventDef { id = "fault", title = "A fault shows up in a test run", body = "Engineers found a small fault in the last test run. It may or may not matter on launch day." };
        e.choices.Add(new EventChoice { label = "Fix it properly", benefits = new[] { "No extra launch risk" }, costs = new[] { Money(fix), "3 days lost" }, risks = new string[0], cost = fix, days = 3f });
        e.choices.Add(new EventChoice { label = "Fly it as it is", benefits = new[] { "Saves the money and the time" }, costs = new string[0], risks = new[] { "+12% chance that the launch fails" }, risk = 0.12f });
        list.Add(e);

        e = new EventDef { id = "sponsor", title = "A sponsor makes an offer", body = "A company would like to put its name on this mission and will pay for it." };
        e.choices.Add(new EventChoice { label = "Accept the sponsor", benefits = new[] { "+" + Money(sponsor) + " now" }, costs = new[] { "2 days of paperwork" }, risks = new string[0], gain = sponsor, days = 2f });
        e.choices.Add(new EventChoice { label = "Decline", benefits = new[] { "Keeps the schedule" }, costs = new string[0], risks = new string[0] });
        list.Add(e);

        e = new EventDef { id = "bracket", title = "A lighter bracket", body = "Your engineers designed a lighter, stronger bracket. Fitting it costs a little." };
        e.choices.Add(new EventChoice { label = "Use it", benefits = new[] { "-6% chance that the launch fails" }, costs = new[] { Money(bracket) }, risks = new string[0], cost = bracket, risk = -0.06f });
        e.choices.Add(new EventChoice { label = "Not now", benefits = new[] { "Saves the money" }, costs = new string[0], risks = new string[0] });
        list.Add(e);

        e = new EventDef { id = "advice", title = "A veteran's advice", body = "A retired flight director suggests one more day of checks before launch." };
        e.choices.Add(new EventChoice { label = "Add a day of checks", benefits = new[] { "-4% chance that the launch fails" }, costs = new[] { "1 day lost" }, risks = new string[0], days = 1f, risk = -0.04f });
        e.choices.Add(new EventChoice { label = "Skip it", benefits = new[] { "Keeps the schedule" }, costs = new string[0], risks = new string[0] });
        list.Add(e);

        e = new EventDef { id = "pylon", title = "Storm damage on the pad", body = "A storm bent a pylon next to the launch pad. It can be repaired fully or patched quickly." };
        e.choices.Add(new EventChoice { label = "Repair it fully", benefits = new[] { "No extra launch risk" }, costs = new[] { Money(repair), "2 days lost" }, risks = new string[0], cost = repair, days = 2f });
        e.choices.Add(new EventChoice { label = "Patch it", benefits = new[] { "Cheap and quick" }, costs = new[] { Money(patch) }, risks = new[] { "+8% chance that the launch fails" }, cost = patch, risk = 0.08f });
        list.Add(e);
        return list;
    }

    static EventDef ClientEvent(MissionDefinition d)
    {
        var sim = BaseSim.Instance;
        string[] ids = { "sci1", "pilot1", "test_engine", "test_struct", "test_comms" };
        var open = ids.Where(i => sim == null || !sim.IsDone(i)).ToList();
        var pick = (open.Count > 0 ? open : ids.ToList())[UnityEngine.Random.Range(0, open.Count > 0 ? open.Count : ids.Length)];
        var pd = BaseData.Program(pick);
        long extra = M(Mathf.Max(8f, d.budget / 1e6f * 0.08f));
        var e = new EventDef { id = "client_" + pick, client = true, title = "The client wants a change", body = "The client asks you to complete \"" + (pd != null ? pd.name : pick) + "\" before the launch. They will add to the reward if you agree." };
        e.choices.Add(new EventChoice { label = "Accept the request", benefits = new[] { "+" + Money(extra) + " when the mission pays", "Client trust +1" }, costs = new[] { "Adds an objective to this mission" }, risks = new string[0], extraBudget = extra, program = pick, trust = 1 });
        e.choices.Add(new EventChoice { label = "Decline", benefits = new[] { "No extra work" }, costs = new[] { "No extra budget from the client" }, risks = new[] { "Client trust -1: future rewards pay a little less" }, trust = -1 });
        return e;
    }
}
