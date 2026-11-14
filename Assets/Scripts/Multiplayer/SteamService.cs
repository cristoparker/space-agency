#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>One Steam friend as the Friends tab shows it.</summary>
public class SteamFriendInfo
{
    public ulong id;
    public string name;
    public bool online;
    public bool inThisGame;      // running Space Mission Tycoon right now
    public ulong lobby;           // the lobby they are in (0 = none we can see)
    public string status;
}

/// <summary>
/// Everything the game needs from Steam: start-up, the friends list with avatars, and the friends-only lobby that carries
/// invites and joins. The network game itself runs over PurrNet's Steam transport, addressed by the host's Steam ID,
/// which this class keeps as lobby data. Without a running Steam client every call is a harmless no-op and <see cref="Ready"/> is false.
/// </summary>
public static class SteamService
{
    public const int MaxPlayers = 4;
    const string HostKey = "host", NameKey = "name", GameKey = "game";
    const string GameTag = "space-mission-tycoon";

    public static bool Ready { get; private set; }
    public static string Problem { get; private set; } = "Steam has not started yet.";
    public static ulong Lobby { get; private set; }
    public static bool OwnsLobby { get; private set; }

    /// <summary>A lobby was entered as a guest: connect to this host Steam ID.</summary>
    public static event Action<ulong> JoinedLobbyAsGuest;
    /// <summary>Our own lobby is ready, so invites can go out.</summary>
    public static event Action<ulong> LobbyCreated;
    /// <summary>Something failed (lobby full, gone, Steam error): a sentence for the player.</summary>
    public static event Action<string> Failed;
    /// <summary>Friends changed (came online, started the game, changed name...).</summary>
    public static event Action FriendsChanged;

    static readonly Dictionary<ulong, Texture2D> avatars = new Dictionary<ulong, Texture2D>();
    static bool inited;

    public static void ResetStatics()
    {
        Lobby = 0; OwnsLobby = false;
        JoinedLobbyAsGuest = null; LobbyCreated = null; Failed = null; FriendsChanged = null;
    }

#if DISABLESTEAMWORKS
    public static void Init() { Ready = false; Problem = "Steam is not available on this platform."; }
    public static void Tick() { }
    public static void Shutdown() { }
    public static ulong MyId => 0;
    public static string MyName => Environment.UserName;
    public static List<SteamFriendInfo> Friends() => new List<SteamFriendInfo>();
    public static Texture2D Avatar(ulong id) => null;
    public static void CreateLobby() => Failed?.Invoke(Problem);
    public static void JoinLobby(ulong lobby) => Failed?.Invoke(Problem);
    public static void Invite(ulong friend) { }
    public static void OpenInviteOverlay() { }
    public static void LeaveLobby() { }
    public static void SetStatus(string s) { }
    public static string FriendName(ulong id) => "Player";
#else
    static Callback<LobbyEnter_t> cbEnter;
    static Callback<GameLobbyJoinRequested_t> cbJoinRequest;
    static Callback<GameRichPresenceJoinRequested_t> cbRichJoin;
    static Callback<PersonaStateChange_t> cbPersona;
    static Callback<AvatarImageLoaded_t> cbAvatar;
    static CallResult<LobbyCreated_t> crCreated;
    static float friendsDirtyTimer;

    public static void Init()
    {
        if (inited) return;
        inited = true;
        try
        {
            if (!Packsize.Test()) { Problem = "Steamworks.NET was built for the wrong platform."; return; }
            if (!DllCheck.Test()) { Problem = "The Steam API library is missing or out of date."; return; }
            if (!SteamAPI.Init())
            {
                Problem = "Steam is not running. Start Steam and sign in, then restart the game.";
                return;
            }
        }
        catch (DllNotFoundException e)
        {
            Problem = "The Steam API library could not be loaded: " + e.Message;
            return;
        }

        Ready = true; Problem = null;
        cbEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        cbJoinRequest = Callback<GameLobbyJoinRequested_t>.Create(r => JoinLobby(r.m_steamIDLobby.m_SteamID));
        cbRichJoin = Callback<GameRichPresenceJoinRequested_t>.Create(r => TryJoinFromConnectString(r.m_rgchConnect));
        cbPersona = Callback<PersonaStateChange_t>.Create(_ => friendsDirtyTimer = 0.4f);
        cbAvatar = Callback<AvatarImageLoaded_t>.Create(r => { avatars.Remove(r.m_steamID.m_SteamID); friendsDirtyTimer = 0.2f; });
        crCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);

        // launched by "Join game" from a friend's Steam profile: Steam passes +connect_lobby <id> on the command line
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var lobbyId)) { JoinLobby(lobbyId); break; }
    }

    public static void Tick()
    {
        if (!Ready) return;
        SteamAPI.RunCallbacks();
        if (friendsDirtyTimer > 0f)
        {
            friendsDirtyTimer -= Time.unscaledDeltaTime;
            if (friendsDirtyTimer <= 0f) FriendsChanged?.Invoke();
        }
    }

    public static void Shutdown()
    {
        if (!Ready) return;
        LeaveLobby();
        SteamFriends.ClearRichPresence();
        SteamAPI.Shutdown();
        Ready = false; inited = false; Problem = "Steam was shut down.";
        foreach (var t in avatars.Values) if (t != null) UnityEngine.Object.Destroy(t);
        avatars.Clear();
    }

    public static ulong MyId => Ready ? SteamUser.GetSteamID().m_SteamID : 0;
    public static string MyName => Ready ? SteamFriends.GetPersonaName() : Environment.UserName;
    public static string FriendName(ulong id) => Ready ? SteamFriends.GetFriendPersonaName(new CSteamID(id)) : "Player";

    // ---------------------------------------------------------------- friends
    public static List<SteamFriendInfo> Friends()
    {
        var list = new List<SteamFriendInfo>();
        if (!Ready) return list;
        var appId = SteamUtils.GetAppID();
        int n = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        for (int i = 0; i < n; i++)
        {
            var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            var state = SteamFriends.GetFriendPersonaState(id);
            var f = new SteamFriendInfo { id = id.m_SteamID, name = SteamFriends.GetFriendPersonaName(id), online = state != EPersonaState.k_EPersonaStateOffline };
            if (SteamFriends.GetFriendGamePlayed(id, out var game) && game.m_gameID.AppID() == appId)
            {
                f.inThisGame = true;
                if (game.m_steamIDLobby.IsValid()) f.lobby = game.m_steamIDLobby.m_SteamID;
            }
            var rich = SteamFriends.GetFriendRichPresence(id, "status");
            f.status = f.inThisGame ? (string.IsNullOrEmpty(rich) ? "Playing Space Mission Tycoon" : rich)
                     : !f.online ? "Offline"
                     : state == EPersonaState.k_EPersonaStateAway || state == EPersonaState.k_EPersonaStateSnooze ? "Away"
                     : "Online";
            list.Add(f);
        }
        // in this game first, then online, then by name
        list.Sort((a, b) =>
        {
            int c = b.inThisGame.CompareTo(a.inThisGame); if (c != 0) return c;
            c = b.online.CompareTo(a.online); if (c != 0) return c;
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    /// <summary>The friend's small Steam avatar, or null while Steam is still downloading it.</summary>
    public static Texture2D Avatar(ulong id)
    {
        if (!Ready) return null;
        if (avatars.TryGetValue(id, out var cached)) return cached;
        int handle = SteamFriends.GetMediumFriendAvatar(new CSteamID(id));
        if (handle <= 0 || !SteamUtils.GetImageSize(handle, out uint w, out uint h) || w == 0 || h == 0) return null;
        var raw = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(handle, raw, raw.Length)) return null;
        // Steam gives rows top to bottom; textures want them bottom to top
        var flipped = new byte[raw.Length];
        int row = (int)w * 4;
        for (int y = 0; y < h; y++) Buffer.BlockCopy(raw, y * row, flipped, ((int)h - 1 - y) * row, row);
        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false) { name = "Avatar " + id, wrapMode = TextureWrapMode.Clamp };
        tex.LoadRawTextureData(flipped);
        tex.Apply();
        avatars[id] = tex;
        return tex;
    }

    // ---------------------------------------------------------------- lobby
    public static void CreateLobby()
    {
        if (!Ready) { Failed?.Invoke(Problem); return; }
        LeaveLobby();
        crCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers));
    }

    static void OnLobbyCreated(LobbyCreated_t r, bool ioFailure)
    {
        if (ioFailure || r.m_eResult != EResult.k_EResultOK) { Failed?.Invoke("Steam could not create a lobby (" + r.m_eResult + ")."); return; }
        var lobby = new CSteamID(r.m_ulSteamIDLobby);
        Lobby = lobby.m_SteamID; OwnsLobby = true;
        SteamMatchmaking.SetLobbyData(lobby, HostKey, MyId.ToString());
        SteamMatchmaking.SetLobbyData(lobby, NameKey, Agency.Registered ? Agency.Name : MyName + "'s agency");
        SteamMatchmaking.SetLobbyData(lobby, GameKey, GameTag);
        SteamFriends.SetRichPresence("connect", "+connect_lobby " + Lobby);
        SteamFriends.SetRichPresence("status", "Hosting a shared agency");
        LobbyCreated?.Invoke(Lobby);
    }

    public static void JoinLobby(ulong lobby)
    {
        if (!Ready) { Failed?.Invoke(Problem); return; }
        if (lobby == 0) { Failed?.Invoke("That friend is not in a lobby."); return; }
        if (lobby == Lobby) return;
        LeaveLobby();
        SteamMatchmaking.JoinLobby(new CSteamID(lobby));
    }

    static void TryJoinFromConnectString(string connect)
    {
        if (string.IsNullOrEmpty(connect)) return;
        var parts = connect.Split(' ');
        for (int i = 0; i < parts.Length - 1; i++)
            if (parts[i] == "+connect_lobby" && ulong.TryParse(parts[i + 1], out var id)) { JoinLobby(id); return; }
    }

    static void OnLobbyEnter(LobbyEnter_t r)
    {
        var lobby = new CSteamID(r.m_ulSteamIDLobby);
        if ((EChatRoomEnterResponse)r.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Failed?.Invoke("Could not join the lobby (" + (EChatRoomEnterResponse)r.m_EChatRoomEnterResponse + ").");
            return;
        }
        if (SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID == MyId) return;   // our own lobby: handled by OnLobbyCreated

        Lobby = lobby.m_SteamID; OwnsLobby = false;
        if (!ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, HostKey), out var host) || host == 0)
        {
            Failed?.Invoke("That lobby has no host. It may have closed.");
            LeaveLobby();
            return;
        }
        SteamFriends.SetRichPresence("connect", "+connect_lobby " + Lobby);
        SteamFriends.SetRichPresence("status", "In a shared agency");
        JoinedLobbyAsGuest?.Invoke(host);
    }

    public static void Invite(ulong friend)
    {
        if (!Ready || Lobby == 0) return;
        SteamMatchmaking.InviteUserToLobby(new CSteamID(Lobby), new CSteamID(friend));
    }

    public static void OpenInviteOverlay()
    {
        if (Ready && Lobby != 0) SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(Lobby));
    }

    public static void LeaveLobby()
    {
        if (Ready && Lobby != 0) SteamMatchmaking.LeaveLobby(new CSteamID(Lobby));
        if (Ready) { SteamFriends.SetRichPresence("connect", null); SteamFriends.SetRichPresence("status", "In the menus"); }
        Lobby = 0; OwnsLobby = false;
    }

    public static void SetStatus(string s) { if (Ready) SteamFriends.SetRichPresence("status", s); }
#endif
}
