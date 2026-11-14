using System;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Steam;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One person in the shared agency, as the host keeps them.</summary>
public class TeamMember
{
    public ulong playerId;
    public string name;
    public ulong steamId;
    public TeamRole role;
    public bool ready;
    public bool isHost;
}

public class ChatLine
{
    public string from, text;
    public TeamRole role;
    public bool system;
    public DateTime at;
}

/// <summary>
/// The multiplayer session: up to four players run one agency together. The host's game is the real one; guests load the host's
/// agency, follow its clock and send their requests to it. Runs on PurrNet with plain broadcast messages (no networked prefabs),
/// over Steam (friends-only lobby, P2P by Steam ID) or, for testing on one machine with ParrelSync, over UDP on localhost.
/// Lives in Resources/Multiplayer/NetSession (made by Tools / Space Agency / Setup Multiplayer) and survives scene loads.
/// </summary>
public class NetSession : MonoBehaviour
{
    public const string PrefabPath = "Multiplayer/NetSession";
    public const string Version = "1";
    public const string MenuScene = "1. Main Menu", GameScene = "2. Game";
    public const int MaxChat = 120;

    public enum Mode { Offline, Starting, Hosting, Joining, Guest }

    public static NetSession Instance { get; private set; }
    public static event Action Changed;
    public static event Action<ChatLine> ChatAdded;

    public NetworkManager network;
    public SteamTransport steam;
    public UDPTransport udp;

    public Mode State { get; private set; } = Mode.Offline;
    public bool IsHost => State == Mode.Hosting;
    public bool IsGuest => State == Mode.Guest || State == Mode.Joining;
    public bool InSession => State != Mode.Offline;
    public bool LocalTest { get; private set; }
    public string Status { get; private set; } = "";
    public bool InGame { get; private set; }
    public string AgencyName { get; private set; } = "";

    public readonly List<TeamMember> Members = new List<TeamMember>();
    public readonly List<ChatLine> Chat = new List<ChatLine>();
    readonly Dictionary<PlayerID, TeamMember> byPlayer = new Dictionary<PlayerID, TeamMember>();

    float clockTimer;
    bool leaving;

    public static void ResetStatics() { Instance = null; Changed = null; ChatAdded = null; }

    /// <summary>The session object, created on first use.</summary>
    public static NetSession Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("NetSession: Resources/" + PrefabPath + " is missing. Run Tools / Space Agency / Setup Multiplayer."); return null; }
        var go = Instantiate(prefab);
        go.name = "Net Session";
        return go.GetComponent<NetSession>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        TeamWorld.RestoreBackup();                         // a shared game that was closed in the middle: give the player their own save back
        Ensure();
    }

    // ================================================================ lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SteamService.Init();
        SteamService.JoinedLobbyAsGuest += OnSteamLobbyJoined;
        SteamService.LobbyCreated += OnSteamLobbyCreated;
        SteamService.Failed += OnSteamFailed;

        network.onServerConnectionState += OnServerState;
        network.onClientConnectionState += OnClientState;
        network.onPlayerLeft += OnPlayerLeft;
        network.onLocalPlayerReceivedID += OnLocalPlayerId;

        network.Subscribe<HelloMsg>(OnHello, true);
        network.Subscribe<RoleRequestMsg>(OnRoleRequest, true);
        network.Subscribe<ReadyMsg>(OnReady, true);
        network.Subscribe<ChatMsg>(OnChatToServer, true);
        network.Subscribe<SpeedRequestMsg>(OnSpeedRequest, true);

        network.Subscribe<RosterMsg>(OnRoster, false);
        network.Subscribe<ChatMsg>(OnChatToClient, false);
        network.Subscribe<WorldMsg>(OnWorld, false);
        network.Subscribe<ClockMsg>(OnClock, false);
        network.Subscribe<KickMsg>(OnKick, false);

        // the shared agency in game (see TeamSync)
        network.Subscribe<ActionMsg>(TeamSync.OnAction, true);
        network.Subscribe<StateMsg>(TeamSync.OnState, false);
        network.Subscribe<MissionsMsg>(TeamSync.OnMissions, false);
        network.Subscribe<EventShowMsg>(TeamSync.OnEventShow, false);
        network.Subscribe<EventCloseMsg>(TeamSync.OnEventClose, false);
        network.Subscribe<LaunchMsg>(TeamSync.OnLaunch, false);

        SceneManager.sceneLoaded += OnSceneLoaded;
        Status = SteamService.Ready ? "Signed in to Steam as " + SteamService.MyName : SteamService.Problem;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameClock.SpeedRequest = null; GameClock.FollowingHost = false;
        Instance = null;
    }

    void OnApplicationQuit()
    {
        Leave(false);
        SteamService.Shutdown();
    }

    void Update()
    {
        SteamService.Tick();
        TeamSync.HostTick();
        if (IsHost && InGame && GameClock.Instance != null)
        {
            clockTimer -= Time.unscaledDeltaTime;
            if (clockTimer <= 0f)
            {
                clockTimer = 0.25f;
                var c = GameClock.Instance;
                network.SendToAll(new ClockMsg { nowTicks = c.Now.Ticks, gameTime = c.GameTime, speedIndex = c.SpeedIndex, paused = c.paused }, Channel.UnreliableSequenced);
            }
        }
    }

    // ================================================================ start and stop
    /// <summary>Opens a shared agency. Over Steam this makes a friends-only lobby first; in local test it listens on localhost.</summary>
    public void Host(bool localTest)
    {
        if (InSession) return;
        if (!Agency.Registered) { Fail("Register an agency first (New Game), then host it."); return; }
        LocalTest = localTest;
        AgencyName = Agency.Name;
        if (localTest) { StartHostOn(udp); return; }
        if (!SteamService.Ready) { Fail(SteamService.Problem); return; }
        SetState(Mode.Starting, "Creating a Steam lobby...");
        SteamService.CreateLobby();
    }

    void OnSteamLobbyCreated(ulong lobby)
    {
        if (State != Mode.Starting) return;
        StartHostOn(steam);
    }

    void StartHostOn(GenericTransport t)
    {
        try
        {
            network.transport = t;
            SetState(Mode.Starting, "Starting the server...");
            network.StartHost();
        }
        catch (Exception e) { Fail("Could not start hosting: " + e.Message); }
    }

    /// <summary>Joins the editor that hosts a local test on this machine (ParrelSync clones).</summary>
    public void JoinLocal()
    {
        if (InSession) return;
        LocalTest = true;
        udp.address = "127.0.0.1";
        StartClientOn(udp, "Connecting to the local host...");
    }

    public void JoinFriend(SteamFriendInfo f)
    {
        if (InSession) { Fail("Leave the current session first."); return; }
        if (f == null || f.lobby == 0) { Fail("That friend is not hosting a lobby you can see."); return; }
        SetState(Mode.Joining, "Joining " + f.name + "'s lobby...");
        SteamService.JoinLobby(f.lobby);
    }

    void OnSteamLobbyJoined(ulong hostSteamId)
    {
        if (IsHost) return;
        LocalTest = false;
        steam.address = hostSteamId.ToString();
        StartClientOn(steam, "Connecting to " + SteamService.FriendName(hostSteamId) + "...");
    }

    void StartClientOn(GenericTransport t, string msg)
    {
        try
        {
            network.transport = t;
            SetState(Mode.Joining, msg);
            network.StartClient();
        }
        catch (Exception e) { Fail("Could not connect: " + e.Message); }
    }

    /// <summary>Leaves the session. A guest gets their own save back and returns to the main menu.</summary>
    public void Leave(bool toMenu = true)
    {
        if (leaving) return;
        leaving = true;
        bool wasGuest = IsGuest, wasInGame = InGame;
        try
        {
            if (network.isServer) network.StopServer();
            if (network.isClient) network.StopClient();
        }
        catch (Exception e) { Debug.LogWarning("NetSession: stop failed: " + e.Message); }
        SteamService.LeaveLobby();
        Members.Clear(); byPlayer.Clear();
        InGame = false;
        TeamSync.ResetStatics();
        GameClock.SpeedRequest = null; GameClock.FollowingHost = false;
        SetState(Mode.Offline, SteamService.Ready ? "Signed in to Steam as " + SteamService.MyName : SteamService.Problem);
        if (wasGuest)
        {
            TeamWorld.RestoreBackup();
            if (toMenu && wasInGame && SceneManager.GetActiveScene().name != MenuScene) SceneManager.LoadScene(MenuScene);
        }
        leaving = false;
    }

    void OnSteamFailed(string why) { if (State == Mode.Starting || State == Mode.Joining) Leave(false); Fail(why); }

    void Fail(string why)
    {
        Status = why;
        AddChat(new ChatLine { from = "", text = why, system = true, at = DateTime.Now });
        Changed?.Invoke();
    }

    void SetState(Mode m, string status) { State = m; Status = status; Changed?.Invoke(); }

    void OnServerState(ConnectionState s)
    {
        if (s == ConnectionState.Connected && State == Mode.Starting)
        {
            SetState(Mode.Hosting, LocalTest ? "Hosting a local test on this machine." : "Hosting. Invite friends from the Friends tab.");
            SteamService.SetStatus("Hosting a shared agency");
        }
        else if (s == ConnectionState.Disconnected && IsHost && !leaving) Leave(false);
    }

    void OnClientState(ConnectionState s)
    {
        if (s == ConnectionState.Connected)
        {
            if (State == Mode.Joining) SetState(Mode.Guest, "Connected. Saying hello...");
            if (IsGuest)
            {
                GameClock.FollowingHost = true;
                GameClock.SpeedRequest = i => { network.SendToServer(new SpeedRequestMsg { speedIndex = i }); return true; };
            }
        }
        else if (s == ConnectionState.Disconnected && IsGuest && !leaving)
        {
            Leave(true);
            Fail("Disconnected from the host.");
        }
    }

    /// <summary>The host knows this player now (it has an ID), so the hello will be heard.</summary>
    void OnLocalPlayerId(PlayerID _)
    {
        network.SendToServer(new HelloMsg { name = MyName, steamId = SteamService.MyId, version = Version });
        if (IsGuest) SetState(Mode.Guest, "Connected.");
    }

    public string MyName
    {
        get
        {
            var n = SteamService.Ready ? SteamService.MyName : Environment.UserName;
            if (ApplicationContext.isClone) n += " (clone)";
            return n;
        }
    }

    // ================================================================ host side
    void OnHello(PlayerID player, HelloMsg msg, bool asServer)
    {
        if (msg.version != Version) { network.Send(player, new KickMsg { reason = "Your game version does not match the host's." }); return; }
        if (!byPlayer.ContainsKey(player) && byPlayer.Count >= SteamService.MaxPlayers) { network.Send(player, new KickMsg { reason = "The agency is full (4 players)." }); return; }
        if (!byPlayer.TryGetValue(player, out var m))
        {
            m = new TeamMember { playerId = player.id, isHost = byPlayer.Count == 0 };
            byPlayer[player] = m; Members.Add(m);
        }
        m.name = string.IsNullOrWhiteSpace(msg.name) ? "Player " + Members.Count : msg.name.Trim();
        m.steamId = msg.steamId;
        if (m.isHost) m.ready = true;
        ServerSystemChat(m.name + (m.isHost ? " opened the agency." : " joined."));
        BroadcastRoster();
        if (InGame && !m.isHost)
        {
            PlayerPrefs.Save();
            network.Send(player, TeamWorld.Capture());                             // late joiner: straight into the running game
            TeamSync.ForgetSent();
        }
    }

    void OnPlayerLeft(PlayerID player, bool asServer)
    {
        if (!asServer || !byPlayer.TryGetValue(player, out var m)) return;
        byPlayer.Remove(player); Members.Remove(m);
        ServerSystemChat(m.name + " left.");
        BroadcastRoster();
    }

    void OnRoleRequest(PlayerID player, RoleRequestMsg msg, bool asServer)
    {
        if (!byPlayer.TryGetValue(player, out var m)) return;
        var role = (TeamRole)Mathf.Clamp(msg.role, 0, 4);
        if (role != TeamRole.None)
            foreach (var other in Members)
                if (other != m && other.role == role) { network.Send(player, new ChatMsg { text = other.name + " already has the " + role + " role.", system = true }); return; }
        if (m.role == role) return;
        m.role = role;
        ServerSystemChat(m.name + (role == TeamRole.None ? " gave up their role." : " is now the " + role + "."));
        BroadcastRoster();
    }

    void OnReady(PlayerID player, ReadyMsg msg, bool asServer)
    {
        if (!byPlayer.TryGetValue(player, out var m) || m.isHost) return;
        m.ready = msg.ready;
        BroadcastRoster();
    }

    void OnChatToServer(PlayerID player, ChatMsg msg, bool asServer)
    {
        if (!byPlayer.TryGetValue(player, out var m) || string.IsNullOrWhiteSpace(msg.text)) return;
        var text = msg.text.Trim();
        if (text.Length > 200) text = text.Substring(0, 200);
        network.SendToAll(new ChatMsg { from = m.name, text = text, role = (int)m.role });
    }

    void OnSpeedRequest(PlayerID player, SpeedRequestMsg msg, bool asServer)
    {
        var c = GameClock.Instance;
        if (c == null || !byPlayer.TryGetValue(player, out var m)) return;
        c.SetSpeedIndex(msg.speedIndex);
        if (msg.speedIndex == 0) ServerSystemChat(m.name + " paused the game.");
    }

    void ServerSystemChat(string text) => network.SendToAll(new ChatMsg { text = text, system = true });

    /// <summary>Host only: a grey line in everyone's chat.</summary>
    public void SystemChat(string text) { if (IsHost) ServerSystemChat(text); }

    /// <summary>Host only: a grey line for one player.</summary>
    public void SystemTo(PlayerID player, string text) { if (IsHost) network.Send(player, new ChatMsg { text = text, system = true }); }

    public TeamMember MemberOf(PlayerID player) => byPlayer.TryGetValue(player, out var m) ? m : null;

    void BroadcastRoster()
    {
        TeamSync.RosterChanged();
        int n = Members.Count;
        var r = new RosterMsg
        {
            playerIds = new ulong[n], names = new string[n], steamIds = new ulong[n], roles = new int[n], ready = new bool[n],
            agencyName = AgencyName, inGame = InGame,
        };
        for (int i = 0; i < n; i++)
        {
            var m = Members[i];
            r.playerIds[i] = m.playerId; r.names[i] = m.name; r.steamIds[i] = m.steamId; r.roles[i] = (int)m.role; r.ready[i] = m.ready;
        }
        network.SendToAll(r);
    }

    /// <summary>Host only: sends the agency to everyone and opens the Game scene for all.</summary>
    public void StartGame()
    {
        if (!IsHost) return;
        if (InGame) return;
        foreach (var m in Members) if (!m.ready) { Fail(m.name + " is not ready yet."); return; }
        PlayerPrefs.Save();
        InGame = true;
        TeamSync.ForgetSent();
        var world = TeamWorld.Capture();
        foreach (var kv in byPlayer) if (!kv.Value.isHost) network.Send(kv.Key, world);
        ServerSystemChat("The agency opens. Good luck!");
        BroadcastRoster();
        SceneManager.LoadScene(GameScene);
    }

    public void Kick(TeamMember m)
    {
        if (!IsHost || m == null || m.isHost) return;
        foreach (var kv in byPlayer)
            if (kv.Value == m) { network.Send(kv.Key, new KickMsg { reason = "The host removed you from the agency." }); break; }
    }

    // ================================================================ client side (the host's own client too)
    void OnRoster(PlayerID _, RosterMsg r, bool asServer)
    {
        if (!IsHost)
        {
            Members.Clear();
            int n = r.names?.Length ?? 0;
            for (int i = 0; i < n; i++)
                Members.Add(new TeamMember { playerId = r.playerIds[i], name = r.names[i], steamId = r.steamIds[i], role = (TeamRole)r.roles[i], ready = r.ready[i], isHost = i == 0 });
            AgencyName = r.agencyName;
            InGame = r.inGame;
        }
        Changed?.Invoke();
    }

    void OnChatToClient(PlayerID _, ChatMsg c, bool asServer)
    {
        AddChat(new ChatLine { from = c.from, text = c.text, role = (TeamRole)c.role, system = c.system, at = DateTime.Now });
    }

    void AddChat(ChatLine line)
    {
        Chat.Add(line);
        while (Chat.Count > MaxChat) Chat.RemoveAt(0);
        ChatAdded?.Invoke(line);
    }

    void OnWorld(PlayerID _, WorldMsg w, bool asServer)
    {
        if (IsHost) return;
        TeamWorld.Apply(w);
        InGame = true;
        SceneManager.LoadScene(GameScene);
    }

    void OnClock(PlayerID _, ClockMsg c, bool asServer)
    {
        if (IsHost || GameClock.Instance == null) return;
        GameClock.Instance.SyncTo(new DateTime(c.nowTicks), c.gameTime, c.speedIndex, c.paused);
    }

    void OnKick(PlayerID _, KickMsg k, bool asServer)
    {
        Leave(true);
        Fail(k.reason);
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // the host walked back to the main menu: the shared game is over for everyone
        if (IsHost && InGame && s.name == MenuScene)
        {
            InGame = false;
            ServerSystemChat("The host returned to the menu.");
            BroadcastRoster();
        }
    }

    // ================================================================ requests from this player
    public void SendChat(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!InSession || !network.isClient) { AddChat(new ChatLine { from = MyName, text = text.Trim(), at = DateTime.Now }); return; }
        network.SendToServer(new ChatMsg { text = text });
    }

    public void RequestRole(TeamRole r) { if (network.isClient) network.SendToServer(new RoleRequestMsg { role = (int)r }); }

    public void SetReady(bool ready) { if (network.isClient) network.SendToServer(new ReadyMsg { ready = ready }); }

    public TeamMember Me
    {
        get
        {
            if (!InSession || !network.isClient) return null;
            ulong id = network.localPlayer.id;
            foreach (var m in Members) if (m.playerId == id) return m;
            return null;
        }
    }

    /// <summary>Who holds a role (null = nobody, so anyone may act for it).</summary>
    public TeamMember Holder(TeamRole r)
    {
        foreach (var m in Members) if (m.role == r) return m;
        return null;
    }

    /// <summary>
    /// May this player act for a role right now? Always yes outside a shared game, and yes when nobody holds the role.
    /// Shows a short message when the answer is no.
    /// </summary>
    public static bool MayAct(TeamRole r, bool explain = true)
    {
        var s = Instance;
        if (s == null || !s.InSession || !s.InGame) return true;
        var holder = s.Holder(r);
        var me = s.Me;
        if (holder == null || holder == me) return true;
        if (explain) Hud.Toast("Only the " + r + " (" + holder.name + ") can do that.");
        return false;
    }
}
