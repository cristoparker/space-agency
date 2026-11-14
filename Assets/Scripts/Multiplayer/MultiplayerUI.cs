using System;
using System.Collections.Generic;
using PurrNet.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Team Play window, the same in the main menu and in the game: a Friends tab (Steam friends with avatars, host, invite, join),
/// an Agency tab (the lobby: who is in, their roles, ready, start) and a Chat tab. Built in code like the rest of the UI.
/// </summary>
public class MultiplayerUI : MonoBehaviour
{
    public const float W = 430f, H = 280f;
    enum Tab { Friends, Agency, Chat }

    RectTransform card, body, friendsList, rosterList, chatList, rolesRow, actionsRow, friendsActions;
    GameObject friendsPage, agencyPage, chatPage;
    readonly Dictionary<Tab, Image> tabFills = new Dictionary<Tab, Image>();
    TMP_Text status, agencyTitle, roleDuty, friendsEmpty;
    TMP_InputField chatInput;
    Tab tab = Tab.Friends;
    float friendsTimer;
    bool dirty = true, chatDirty = true;
    Action onClose;

    public static Color RoleColor(TeamRole r)
    {
        var T = UIKit.T;
        switch (r)
        {
            case TeamRole.Engineer: return T.orange;
            case TeamRole.Scientist: return T.purple;
            case TeamRole.Finance: return T.green;
            case TeamRole.Safety: return UIKit.Amber;
            default: return T.teal;
        }
    }

    /// <summary>Builds the window centred on <paramref name="parent"/>. It starts hidden; call <see cref="Show"/>.</summary>
    public static MultiplayerUI Create(Transform parent, Action onClose)
    {
        var go = new GameObject("Team Play", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(W, H); rt.anchoredPosition = Vector2.zero;
        var ui = go.AddComponent<MultiplayerUI>();
        ui.onClose = onClose;
        ui.Build(rt);
        go.SetActive(false);
        return ui;
    }

    public void Show(int tabIndex = -1)
    {
        NetSession.Ensure();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (tabIndex >= 0) SetTab((Tab)tabIndex);
        else SetTab(NetSession.Instance != null && NetSession.Instance.InSession ? Tab.Agency : Tab.Friends);
        friendsTimer = 0f; dirty = true; chatDirty = true;
    }

    public void Hide() { gameObject.SetActive(false); onClose?.Invoke(); }

    void OnEnable()
    {
        NetSession.Changed += MarkDirty;
        NetSession.ChatAdded += OnChat;
        SteamService.FriendsChanged += MarkFriends;
    }

    void OnDisable()
    {
        NetSession.Changed -= MarkDirty;
        NetSession.ChatAdded -= OnChat;
        SteamService.FriendsChanged -= MarkFriends;
    }

    void MarkDirty() => dirty = true;
    void MarkFriends() => friendsTimer = 0f;
    void OnChat(ChatLine _) => chatDirty = true;

    // ================================================================ build
    void Build(RectTransform root)
    {
        var T = UIKit.T;
        card = UIKit.Card(root, "Card", 0, 0, W, H);
        UIKit.TitleBar(card, "Team Play", T.teal, W, Hide);

        string[] names = { "Friends", "Agency", "Chat" };
        for (int i = 0; i < names.Length; i++)
        {
            var t = (Tab)i;
            var b = UIKit.Btn(card, names[i], 12 + i * 74, 30, 70, 16, T.chip, () => SetTab(t), 7.5f);
            tabFills[t] = (Image)b.targetGraphic;
        }
        status = UIKit.TextAt(card, "Status", 236, 30, W - 248, 16, "", 6.5f, TextAlignmentOptions.MidlineRight, false, T.dim);

        body = UIKit.At(card, "Body", 12, 52, W - 24, H - 62);
        BuildFriends(); BuildAgency(); BuildChat();
    }

    void BuildFriends()
    {
        var T = UIKit.T;
        friendsPage = UIKit.At(body, "Friends", 0, 0, W - 24, H - 62).gameObject;
        friendsActions = UIKit.At(friendsPage.transform, "Actions", 0, 0, W - 24, 18);
        friendsList = UIKit.ScrollList(friendsPage.transform, "Friend List", 0, 24, W - 24, H - 62 - 24, T.well, 2f);
        friendsEmpty = UIKit.TextAt(friendsPage.transform, "Empty", 10, 60, W - 44, 60, "", 7.5f, TextAlignmentOptions.Center, false, T.dim);
        friendsEmpty.textWrappingMode = TextWrappingModes.Normal;
    }

    void BuildAgency()
    {
        var T = UIKit.T;
        agencyPage = UIKit.At(body, "Agency", 0, 0, W - 24, H - 62).gameObject;
        var p = agencyPage.transform;
        agencyTitle = UIKit.TextAt(p, "Title", 0, 0, W - 24, 14, "", 8.5f, TextAlignmentOptions.MidlineLeft, true);
        rosterList = UIKit.ScrollList(p, "Roster", 0, 18, W - 24, 104, T.well, 2f);
        UIKit.TextAt(p, "Role Label", 0, 128, 120, 12, "Your role", 7.5f, TextAlignmentOptions.MidlineLeft, true);
        roleDuty = UIKit.TextAt(p, "Duty", 120, 128, W - 144, 12, "", 6.5f, TextAlignmentOptions.MidlineRight, false, T.dim);
        rolesRow = UIKit.At(p, "Roles", 0, 142, W - 24, 34);
        actionsRow = UIKit.At(p, "Actions", 0, H - 62 - 20, W - 24, 18);
    }

    void BuildChat()
    {
        var T = UIKit.T;
        chatPage = UIKit.At(body, "Chat", 0, 0, W - 24, H - 62).gameObject;
        var p = chatPage.transform;
        chatList = UIKit.ScrollList(p, "Messages", 0, 0, W - 24, H - 62 - 26, T.well, 1f);
        chatInput = UIKit.Input(p, 0, H - 62 - 20, W - 24 - 60, 18, "Say something to your team...", 200, 7.5f);
        chatInput.onSubmit.AddListener(_ => SendChat());
        UIKit.SolidBtn(p, "Send", W - 24 - 54, H - 62 - 20, 54, 18, T.teal, SendChat, 7.5f);
    }

    void SetTab(Tab t)
    {
        tab = t;
        friendsPage.SetActive(t == Tab.Friends);
        agencyPage.SetActive(t == Tab.Agency);
        chatPage.SetActive(t == Tab.Chat);
        foreach (var kv in tabFills) kv.Value.color = kv.Key == t ? UIKit.Solid(UIKit.T.teal) : UIKit.T.chip;
        dirty = true; chatDirty = true; friendsTimer = 0f;
        if (t == Tab.Chat) chatInput.ActivateInputField();
    }

    // ================================================================ refresh
    void Update()
    {
        if (UIKit.PointerHeld) return;                 // never rebuild under a pressed button
        var s = NetSession.Instance;
        if (s != null) status.text = s.Status;

        if (tab == Tab.Friends)
        {
            friendsTimer -= Time.unscaledDeltaTime;
            if (friendsTimer <= 0f || dirty) { friendsTimer = 4f; RefreshFriends(); }
        }
        if (dirty && tab == Tab.Agency) RefreshAgency();
        if (chatDirty && tab == Tab.Chat) RefreshChat();
        dirty = false;
    }

    static bool CanLocalTest => Application.isEditor;

    void RefreshFriends()
    {
        var T = UIKit.T;
        var s = NetSession.Instance;
        UIKit.Clear(friendsActions);
        float x = 0;
        Button Act(string label, Color c, UnityEngine.Events.UnityAction a, bool solid = false, float w = 92)
        {
            var b = solid ? UIKit.SolidBtn(friendsActions, label, x, 0, w, 18, c, a, 7f) : UIKit.SoftBtn(friendsActions, label, x, 0, w, 18, c, a, 7f);
            x += w + 6; return b;
        }
        if (s == null || !s.InSession)
        {
            Act("Host with Steam", T.teal, () => s?.Host(false), true, 96).interactable = SteamService.Ready && Agency.Registered;
            if (CanLocalTest)
            {
                if (!ApplicationContext.isClone) Act("Host local test", T.purple, () => s?.Host(true));
                Act("Join local test", T.purple, () => s?.JoinLocal());
            }
        }
        else
        {
            if (s.IsHost && !s.LocalTest) Act("Invite with Steam", T.teal, SteamService.OpenInviteOverlay, true, 100);
            Act("Open agency", T.chip, () => SetTab(Tab.Agency));
            Act("Leave", T.red, () => s.Leave(), false, 60);
        }

        UIKit.Clear(friendsList);
        if (!SteamService.Ready)
        {
            friendsEmpty.text = SteamService.Problem + (CanLocalTest ? "\n\nIn the editor you can still test with ParrelSync: host a local test here, then press Join local test in the clone." : "");
            return;
        }
        var friends = SteamService.Friends();
        friendsEmpty.text = friends.Count == 0 ? "No Steam friends yet. Add friends in Steam, and they will show up here." : "";
        foreach (var f in friends) FriendRow(f, s);
    }

    void FriendRow(SteamFriendInfo f, NetSession s)
    {
        var T = UIKit.T;
        var row = UIKit.At(friendsList, "Friend " + f.name, 0, 0, W - 28, 26);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
        UIKit.Tint(row, f.inThisGame ? UIKit.Wash(T.teal, 0.14f) : new Color(1f, 1f, 1f, 0.03f));

        var av = UIKit.At(row, "Avatar", 4, 3, 20, 20);
        var tex = SteamService.Avatar(f.id);
        if (tex != null) { var ri = av.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; }
        else UIKit.Tint(av, T.chip);
        var dot = UIKit.At(row, "Presence", 20, 19, 5, 5);
        UIKit.Tint(dot, f.inThisGame ? T.teal : f.online ? T.green : new Color(0.5f, 0.5f, 0.55f, 1f));

        UIKit.TextAt(row, "Name", 30, 2, 220, 12, f.name, 7.5f, TextAlignmentOptions.MidlineLeft, true, f.online ? T.ink : T.dim);
        UIKit.TextAt(row, "State", 30, 13, 220, 11, f.status, 6.3f, TextAlignmentOptions.MidlineLeft, false, f.inThisGame ? UIKit.SoftInk(T.teal) : T.dim);

        float bx = W - 28 - 64;
        if (s != null && s.IsHost && !s.LocalTest && f.online)
        {
            ulong id = f.id; string name = f.name;
            UIKit.SoftBtn(row, "Invite", bx, 5, 58, 16, T.teal, () => { SteamService.Invite(id); Hud.Toast("Invite sent to " + name + "."); s.SendChat("(invited " + name + ")"); }, 7f);
        }
        else if ((s == null || !s.InSession) && f.lobby != 0)
        {
            var friend = f;
            UIKit.SolidBtn(row, "Join", bx, 5, 58, 16, T.green, () => { NetSession.Ensure()?.JoinFriend(friend); SetTab(Tab.Agency); }, 7f);
        }
    }

    void RefreshAgency()
    {
        var T = UIKit.T;
        var s = NetSession.Instance;
        UIKit.Clear(rosterList); UIKit.Clear(rolesRow); UIKit.Clear(actionsRow);
        if (s == null || !s.InSession)
        {
            agencyTitle.text = "Not in a shared agency";
            roleDuty.text = "";
            var empty = UIKit.At(rosterList, "Empty", 0, 0, W - 28, 60);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            var t = UIKit.Text(empty, Agency.Registered
                ? "Host your agency from the Friends tab and invite up to three friends. Each player takes a role: Engineer, Scientist, Finance or Safety."
                : "Register an agency with New Game first. Then you can host it, or join a friend's agency from the Friends tab.", 7f, TextAlignmentOptions.Center, false, T.dim);
            t.textWrappingMode = TextWrappingModes.Normal;
            UIKit.SoftBtn(actionsRow, "Friends", 0, 0, 80, 18, T.teal, () => SetTab(Tab.Friends), 7f);
            return;
        }

        agencyTitle.text = (string.IsNullOrEmpty(s.AgencyName) ? "Shared agency" : s.AgencyName) + "   " +
                           "<size=6.5><color=#9AA4B8>" + s.Members.Count + " / " + SteamService.MaxPlayers + " players" + (s.LocalTest ? ", local test" : "") + (s.InGame ? ", in game" : ", in the lobby") + "</color></size>";

        var me = s.Me;
        foreach (var m in s.Members)
        {
            var row = UIKit.At(rosterList, "Member " + m.name, 0, 0, W - 28, 22);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            UIKit.Tint(row, m == me ? UIKit.Wash(T.teal, 0.12f) : new Color(1f, 1f, 1f, 0.03f));
            var av = UIKit.At(row, "Avatar", 4, 3, 16, 16);
            var tex = m.steamId != 0 ? SteamService.Avatar(m.steamId) : null;
            if (tex != null) { var ri = av.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; }
            else UIKit.Tint(av, T.chip);
            UIKit.TextAt(row, "Name", 26, 0, 150, 22, m.name + (m == me ? "  <color=#9AA4B8>(you)</color>" : ""), 7.5f, TextAlignmentOptions.MidlineLeft, true);
            if (m.isHost)
            {
                var hb = UIKit.At(row, "Host", 180, 5, 34, 12); UIKit.Pill(hb, UIKit.Soft(UIKit.Amber));
                UIKit.Text(hb, "HOST", 5.5f, TextAlignmentOptions.Center, true, UIKit.SoftInk(UIKit.Amber));
            }
            var rc = RoleColor(m.role);
            var rb = UIKit.At(row, "Role", 220, 4, 74, 14); UIKit.Pill(rb, m.role == TeamRole.None ? T.chip : UIKit.Soft(rc));
            UIKit.Text(rb, TeamRoles.Name(m.role), 6.5f, TextAlignmentOptions.Center, true, m.role == TeamRole.None ? T.dim : UIKit.SoftInk(rc));
            UIKit.TextAt(row, "Ready", 300, 0, 60, 22, s.InGame ? "playing" : m.ready ? "ready" : "not ready", 6.5f, TextAlignmentOptions.MidlineLeft, false, m.ready || s.InGame ? T.green : T.dim);
            if (s.IsHost && !m.isHost) { var target = m; UIKit.SoftBtn(row, "Kick", W - 28 - 40, 4, 36, 14, T.red, () => s.Kick(target), 6.5f); }
        }

        // ---- role picker
        float rw = (W - 24 - 4 * 4) / 5f;
        var roles = new List<TeamRole>(TeamRoles.All) { TeamRole.None };
        for (int i = 0; i < roles.Count; i++)
        {
            var r = roles[i];
            var holder = r == TeamRole.None ? null : s.Holder(r);
            bool mine = me != null && me.role == r;
            var c = RoleColor(r);
            var b = UIKit.Btn(rolesRow, "Role " + r, i * (rw + 4), 0, rw, 32, mine ? UIKit.Solid(c) : holder != null ? new Color(1f, 1f, 1f, 0.04f) : UIKit.Soft(c), () => s.RequestRole(r), 7f, true, mine ? UIKit.OnSolid(c) : UIKit.SoftInk(c));
            var label = b.GetComponentInChildren<TMP_Text>();
            label.text = TeamRoles.Name(r) + "\n<size=5.5>" + (r == TeamRole.None ? "spectate and chat" : holder == null ? "free" : holder.name) + "</size>";
            label.textWrappingMode = TextWrappingModes.Normal;
            b.interactable = holder == null || mine;
            b.gameObject.AddComponent<TooltipTrigger>().text = TeamRoles.Name(r) + "\n" + TeamRoles.Duty(r);
        }
        roleDuty.text = me != null ? TeamRoles.Duty(me.role) + (me.role == TeamRole.None ? "" : ". Free roles can be done by anyone.") : "";

        // ---- actions
        float x = 0;
        if (s.IsHost && !s.InGame)
        {
            bool allReady = true; foreach (var m in s.Members) allReady &= m.ready;
            var sb = UIKit.SolidBtn(actionsRow, "Start game", x, 0, 96, 18, T.green, s.StartGame, 7.5f); x += 102;
            sb.interactable = allReady;
            if (!s.LocalTest) { UIKit.SoftBtn(actionsRow, "Invite with Steam", x, 0, 100, 18, T.teal, SteamService.OpenInviteOverlay, 7f); x += 106; }
        }
        else if (!s.IsHost && !s.InGame && me != null)
        {
            bool ready = me.ready;
            UIKit.SolidBtn(actionsRow, ready ? "Not ready" : "Ready", x, 0, 80, 18, ready ? T.orange : T.green, () => s.SetReady(!ready), 7.5f); x += 86;
            UIKit.TextAt(actionsRow, "Wait", x, 0, 160, 18, "The host starts the game when everyone is ready.", 6.3f, TextAlignmentOptions.MidlineLeft, false, T.dim);
        }
        UIKit.SoftBtn(actionsRow, s.IsHost ? "Close agency" : "Leave", W - 24 - 84, 0, 84, 18, T.red, () => s.Leave(), 7f);
    }

    void RefreshChat()
    {
        chatDirty = false;
        var T = UIKit.T;
        var s = NetSession.Instance;
        UIKit.Clear(chatList);
        if (s == null || s.Chat.Count == 0)
        {
            var e = UIKit.At(chatList, "Empty", 0, 0, W - 28, 30);
            e.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            UIKit.Text(e, s != null && s.InSession ? "No messages yet. Say hello!" : "Chat works once you host or join a shared agency.", 7f, TextAlignmentOptions.Center, false, T.dim);
            return;
        }
        foreach (var line in s.Chat) ChatRow(chatList, line, W - 28);
        Canvas.ForceUpdateCanvases();
        var sr = chatList.GetComponentInParent<ScrollRect>();
        if (sr != null) sr.verticalNormalizedPosition = 0f;
    }

    /// <summary>One chat line: "[role] name: text", or a grey system note. Used by the in-game feed too.</summary>
    public static void ChatRow(Transform list, ChatLine line, float width)
    {
        var T = UIKit.T;
        var row = UIKit.At(list, "Line", 0, 0, width, 12);
        var le = row.gameObject.AddComponent<LayoutElement>();
        string text;
        if (line.system) text = "<color=#9AA4B8><i>" + Escape(line.text) + "</i></color>";
        else
        {
            var c = ColorUtility.ToHtmlStringRGB(UIKit.SoftInk(RoleColor(line.role)));
            text = "<color=#" + c + "><b>" + Escape(line.from) + "</b></color>" + (line.role != TeamRole.None ? " <size=5.5><color=#9AA4B8>" + line.role + "</color></size>" : "") + "  " + Escape(line.text);
        }
        var t = UIKit.Text(row, text, 7f, TextAlignmentOptions.TopLeft);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.offsetMin = new Vector2(4, 0); t.rectTransform.offsetMax = new Vector2(-4, 0);
        le.preferredHeight = Mathf.Max(12f, t.GetPreferredValues(text, width - 8, 0).y + 2f);
    }

    static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "<​");

    void SendChat()
    {
        var text = chatInput.text;
        if (string.IsNullOrWhiteSpace(text)) return;
        NetSession.Ensure()?.SendChat(text);
        chatInput.text = "";
        chatInput.ActivateInputField();
    }
}
