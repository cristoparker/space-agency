using System;
using System.Collections.Generic;
using System.Globalization;
using PurrNet;
using UnityEngine;

/// <summary>
/// The shared agency while the team is in the Game scene. The host's game is the only real one:
/// <list type="bullet">
/// <item>Guests do not simulate (no construction ticks, no contract deadlines, no random events). Every second the host sends the
/// save keys that changed and the contract lists, and the guests' game shows them.</item>
/// <item>Every action a player takes goes through <see cref="Route"/>. It checks the player's role, and on a guest it becomes a
/// request that the host carries out (checking the role again).</item>
/// <item>Random events appear for everyone; the Safety officer chooses. A launch plays for everyone with the host's result.</item>
/// </list>
/// Outside a shared game every call here does nothing and the game runs exactly as in single player.
/// </summary>
public static class TeamSync
{
    public enum RouteResult { Local, Sent, Denied }

    static readonly string[] StreamStr =
    {
        "SpaceAgency.Money", "SpaceAgency.Owned", "SpaceAgency.Accepted", "SpaceAgency.Base", "SpaceAgency.Deployment", "SpaceAgency.Flight",
        "SpaceAgency.RocketBuild", "SpaceAgency.SavedRockets", "SpaceAgency.CustomPayloads", "SpaceAgency.AgencyName", "SpaceAgency.Logo",
    };
    static readonly string[] StreamInt = { "SpaceAgency.Rep", "SpaceAgency.ClientTrust" };

    /// <summary>True while the host runs a guest's request: the request was already checked, so it must not be routed again.</summary>
    public static bool Executing { get; private set; }

    static readonly Dictionary<string, string> sentStr = new Dictionary<string, string>();
    static readonly Dictionary<string, int> sentInt = new Dictionary<string, int>();
    static float streamTimer, missionTimer;
    static bool missionsDirty;

    // a random event waiting for the Safety officer (host side)
    static string pendingKey;
    static List<Action> pendingPicks;
    static List<string> pendingLabels;
    static EventShowMsg pendingMsg;

    public static void ResetStatics()
    {
        Executing = false; sentStr.Clear(); sentInt.Clear(); streamTimer = missionTimer = 0f; missionsDirty = false;
        pendingKey = null; pendingPicks = null; pendingLabels = null;
    }

    static NetSession S => NetSession.Instance;

    /// <summary>This player is a guest in a running shared game: their game mirrors the host's and simulates nothing itself.</summary>
    public static bool Mirror => S != null && S.IsGuest && S.InGame;

    /// <summary>This player hosts a running shared game.</summary>
    public static bool HostInGame => S != null && S.IsHost && S.InGame;

    // ================================================================ routing player actions
    /// <summary>
    /// Call at the start of every player action. Local = go on and do it here. Sent = a guest's request went to the host (stop here,
    /// report success to the caller). Denied = another player holds the role (stop here, the player was told why).
    /// </summary>
    public static RouteResult Route(TeamRole role, TeamAction kind, string a = null, string b = null, string c = null, int i = 0, float f = 0f, long n = 0)
    {
        if (Executing || S == null || !S.InSession || !S.InGame) return RouteResult.Local;
        if (!NetSession.MayAct(role)) return RouteResult.Denied;
        if (!S.IsGuest) return RouteResult.Local;
        S.network.SendToServer(new ActionMsg { kind = (int)kind, a = a, b = b, c = c, i = i, f = f, n = n });
        Hud.Toast("Sent to the host.");
        return RouteResult.Sent;
    }

    internal static void OnAction(PlayerID player, ActionMsg msg, bool asServer)
    {
        var s = S;
        if (s == null || !s.IsHost || !s.InGame) return;
        var who = s.MemberOf(player);
        if (who == null) return;
        var kind = (TeamAction)msg.kind;
        var role = RoleFor(kind);
        var holder = s.Holder(role);
        if (holder != null && holder != who)
        {
            s.SystemTo(player, "Only the " + role + " (" + holder.name + ") can do that.");
            return;
        }
        Executing = true;
        try { string done = Execute(kind, msg); if (!string.IsNullOrEmpty(done)) s.SystemChat(who.name + " " + done); }
        catch (Exception e) { Debug.LogException(e); }
        finally { Executing = false; }
        streamTimer = 0f; missionsDirty = true;          // show the result to everyone right away
    }

    static TeamRole RoleFor(TeamAction k)
    {
        switch (k)
        {
            case TeamAction.Program: return TeamRole.Scientist;
            case TeamAction.Build: case TeamAction.Accept: case TeamAction.Decline: case TeamAction.RemovePending: case TeamAction.Abandon: return TeamRole.Finance;
            case TeamAction.BuildRocket: case TeamAction.DeployRocket: case TeamAction.FinishRocket: case TeamAction.CancelDeploy: return TeamRole.Engineer;
            default: return TeamRole.Safety;
        }
    }

    /// <summary>Carries out a guest's request on the host. Returns what happened, for the team chat (null = nothing happened).</summary>
    static string Execute(TeamAction kind, ActionMsg m)
    {
        var mm = MissionManager.Instance;
        var sim = BaseSim.Instance;
        switch (kind)
        {
            case TeamAction.Program:
            {
                var p = BaseData.Program(m.a);
                return p != null && sim != null && sim.StartProgram(p) ? "started " + p.name + "." : null;
            }
            case TeamAction.Build:
            {
                var b = BaseData.Building(m.a);
                return b != null && sim != null && sim.StartBuild(b, m.i) ? "started building the " + b.name + "." : null;
            }
            case TeamAction.Accept:
            {
                var d = FindDefinition(m.a);
                if (d == null || mm == null) return null;
                mm.StartMission(d);
                return "accepted the contract \"" + d.displayName + "\".";
            }
            case TeamAction.Decline:
            {
                var d = FindDefinition(m.a);
                return d != null && mm != null && mm.DiscardMission(d) ? "declined \"" + d.displayName + "\"." : null;
            }
            case TeamAction.RemovePending:
            {
                var d = FindDefinition(m.a);
                return d != null && mm != null && mm.RemovePending(d) ? "took \"" + d.displayName + "\" off the pending list." : null;
            }
            case TeamAction.Abandon:
            {
                if (mm == null) return null;
                foreach (var r in mm.Running)
                    if (r.Definition.id == m.a) { mm.AbandonMission(r); return "gave up the contract \"" + r.Definition.displayName + "\"."; }
                return null;
            }
            case TeamAction.BuildRocket:
            {
                var rc = RocketConstruction.Instance;
                if (rc == null || !Agency.TrySpend(m.n)) return null;
                if (!rc.Begin(m.a, RocketDesign.FromJson(m.b), FindDefinition(m.c), m.f)) { Agency.Earn(m.n); return null; }
                return "started building the rocket \"" + m.a + "\".";
            }
            case TeamAction.DeployRocket:
            {
                // c = "yyyy-MM-dd|missionId"
                var dm = DeploymentManager.Instance;
                var parts = (m.c ?? "").Split('|');
                if (dm == null || !DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || !Agency.TrySpend(m.n)) return null;
                dm.Schedule(m.a, RocketDesign.FromJson(m.b), FindDefinition(parts.Length > 1 ? parts[1] : null), date);
                return "put \"" + m.a + "\" on the launch pad.";
            }
            case TeamAction.FinishRocket:
            {
                var rc = RocketConstruction.Instance;
                if (rc == null || !DateTime.TryParseExact(m.a, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return null;
                rc.Finish(date);
                return "set the launch date to " + date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + ".";
            }
            case TeamAction.CancelDeploy:
                DeploymentManager.Instance?.Cancel();
                return "took the rocket off the pad.";
            case TeamAction.Launch:
                return LaunchSequence.Begin() ? "gave the GO for launch!" : null;
            case TeamAction.EventPick:
                return PickEvent(m.a, m.i);
        }
        return null;
    }

    static MissionDefinition FindDefinition(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var mm = MissionManager.Instance;
        if (mm == null) return null;
        foreach (var d in mm.Available) if (d != null && d.id == id) return d;
        foreach (var d in mm.Pending) if (d != null && d.id == id) return d;
        foreach (var r in mm.Running) if (r.Definition.id == id) return r.Definition;
        if (mm.database != null) foreach (var d in mm.database.missions) if (d != null && d.id == id) return d;
        return null;
    }

    // ================================================================ host: the stream
    public static void MarkMissionsDirty() { if (HostInGame) missionsDirty = true; }

    /// <summary>Host, every frame: sends what changed (save keys once a second, contract lists when they change or every 3 s for the progress).</summary>
    public static void HostTick()
    {
        if (!HostInGame) return;
        streamTimer -= Time.unscaledDeltaTime;
        if (streamTimer <= 0f)
        {
            streamTimer = 1f;
            BaseSave.Save();
            RocketConstruction.Instance?.SaveNow();
            var sk = new List<string>(); var sv = new List<string>(); var ik = new List<string>(); var iv = new List<int>();
            foreach (var k in StreamStr)
            {
                var v = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null;
                if (sentStr.TryGetValue(k, out var old) && old == v) continue;
                sentStr[k] = v; sk.Add(k); sv.Add(v ?? "\u0000");          // "\0" = the key was deleted
            }
            foreach (var k in StreamInt)
            {
                int v = PlayerPrefs.GetInt(k, 0);
                if (sentInt.TryGetValue(k, out var old) && old == v) continue;
                sentInt[k] = v; ik.Add(k); iv.Add(v);
            }
            if (sk.Count + ik.Count > 0)
                S.network.SendToAll(new StateMsg { strKeys = sk.ToArray(), strValues = sv.ToArray(), intKeys = ik.ToArray(), intValues = iv.ToArray() });
        }
        missionTimer -= Time.unscaledDeltaTime;
        if (missionsDirty || missionTimer <= 0f)
        {
            missionsDirty = false; missionTimer = 3f;
            var mm = MissionManager.Instance;
            if (mm != null) S.network.SendToAll(mm.Snapshot());
        }
    }

    /// <summary>A new player came in: send them everything again, not only what changed.</summary>
    public static void ForgetSent() { sentStr.Clear(); sentInt.Clear(); streamTimer = 0f; missionsDirty = true; }

    // ================================================================ guest: applying the stream
    internal static void OnState(PlayerID _, StateMsg m, bool asServer)
    {
        if (!Mirror) return;
        bool deploy = false, build = false, flight = false;
        if (m.strKeys != null)
            for (int i = 0; i < m.strKeys.Length && i < m.strValues.Length; i++)
            {
                var k = m.strKeys[i]; var v = m.strValues[i];
                if (v == "\u0000") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetString(k, v ?? "");
                deploy |= k == "SpaceAgency.Deployment"; build |= k == "SpaceAgency.RocketBuild"; flight |= k == "SpaceAgency.Flight";
            }
        if (m.intKeys != null)
            for (int i = 0; i < m.intKeys.Length && i < m.intValues.Length; i++) PlayerPrefs.SetInt(m.intKeys[i], m.intValues[i]);

        BaseSave.Reload();
        BaseSim.Instance?.RefreshFromSave();
        if (deploy) DeploymentManager.Instance?.ReloadFromSave();
        if (build) RocketConstruction.Instance?.ReloadFromSave();
        if (flight) FlightManager.Instance?.ReloadFromSave();
        Agency.NotifyChanged();
    }

    internal static void OnMissions(PlayerID _, MissionsMsg m, bool asServer)
    {
        if (!Mirror) return;
        MissionManager.Instance?.ApplyRemote(m);
    }

    // ================================================================ random events
    /// <summary>
    /// Host: a random event comes up. Everyone sees the card; the Safety officer (or anyone, while nobody is Safety) chooses.
    /// Returns false outside a shared game (then the caller shows the card itself, as in single player).
    /// </summary>
    public static bool HostShowEvent(string key, string kicker, string title, string body, bool client, List<EventCard.Row> rows)
    {
        if (!HostInGame) return false;
        pendingKey = key;
        pendingPicks = new List<Action>(); pendingLabels = new List<string>();
        int n = rows.Count;
        pendingMsg = new EventShowMsg { key = key, kicker = kicker, title = title, body = body, client = client,
            labels = new string[n], benefits = new string[n], costs = new string[n], risks = new string[n], enabled = new bool[n] };
        for (int i = 0; i < n; i++)
        {
            var r = rows[i];
            pendingPicks.Add(r.onPick); pendingLabels.Add(r.label);
            pendingMsg.labels[i] = r.label; pendingMsg.enabled[i] = r.enabled;
            pendingMsg.benefits[i] = Join(r.benefits); pendingMsg.costs[i] = Join(r.costs); pendingMsg.risks[i] = Join(r.risks);
        }
        S.network.SendToAll(pendingMsg);          // the host's own client shows it too (through OnEventShow)
        S.SystemChat("Event: " + title + ". The Safety officer decides.");
        return true;
    }

    static string Join(string[] a) => a == null ? "" : string.Join("\n", a);
    static string[] Split(string s) => string.IsNullOrEmpty(s) ? new string[0] : s.Split('\n');

    internal static void OnEventShow(PlayerID _, EventShowMsg m, bool asServer) => ShowEventCard(m);

    static void ShowEventCard(EventShowMsg m)
    {
        bool mine = NetSession.MayAct(TeamRole.Safety, false);
        var holder = S != null ? S.Holder(TeamRole.Safety) : null;
        var rows = new List<EventCard.Row>();
        int n = m.labels?.Length ?? 0;
        for (int i = 0; i < n; i++)
        {
            int idx = i; string key = m.key;
            rows.Add(new EventCard.Row
            {
                label = m.labels[i], benefits = Split(m.benefits[i]), costs = Split(m.costs[i]), risks = Split(m.risks[i]),
                enabled = m.enabled[i] && mine,
                note = !m.enabled[i] ? "Not enough money" : mine ? null : "The Safety officer (" + (holder != null ? holder.name : "?") + ") decides",
                onPick = () => RequestPick(key, idx),
            });
        }
        EventCard.Close();
        EventCard.Show(m.kicker, m.title, m.body, m.client, rows);
    }

    static void RequestPick(string key, int idx)
    {
        if (S == null) return;
        if (S.IsHost) { Executing = true; try { var r = PickEvent(key, idx); if (r != null) S.SystemChat(S.MyName + " " + r); } finally { Executing = false; } }
        else S.network.SendToServer(new ActionMsg { kind = (int)TeamAction.EventPick, a = key, i = idx });
    }

    /// <summary>Host: applies a choice of the waiting event and closes the card for everyone.</summary>
    static string PickEvent(string key, int idx)
    {
        if (key != pendingKey || pendingPicks == null || idx < 0 || idx >= pendingPicks.Count) return null;
        var pick = pendingPicks[idx]; var label = pendingLabels[idx];
        pendingKey = null; pendingPicks = null; pendingLabels = null;
        EventCard.Close();
        pick?.Invoke();
        S.network.SendToAll(new EventCloseMsg { key = key, choice = label });
        missionsDirty = true; streamTimer = 0f;
        return "chose \"" + label + "\".";
    }

    internal static void OnEventClose(PlayerID _, EventCloseMsg m, bool asServer)
    {
        if (S != null && S.IsHost) return;
        if (EventCard.IsOpen) EventCard.Close();
    }

    /// <summary>The roster changed (someone took or left the Safety role): show the waiting event again so the right person can choose.</summary>
    public static void RosterChanged()
    {
        if (HostInGame && pendingKey != null) S.network.SendToAll(pendingMsg);
    }

    // ================================================================ launch
    /// <summary>Host: the launch began with this result. Guests play the same flight.</summary>
    public static void HostLaunched(FlightRecord rec)
    {
        if (!HostInGame || rec == null) return;
        S.network.SendToAll(new LaunchMsg { record = JsonUtility.ToJson(rec) });
    }

    internal static void OnLaunch(PlayerID _, LaunchMsg m, bool asServer)
    {
        if (!Mirror) return;
        FlightRecord rec = null;
        try { rec = JsonUtility.FromJson<FlightRecord>(m.record); } catch { /* ignore */ }
        if (rec != null) LaunchSequence.Spectate(rec);
    }
}
