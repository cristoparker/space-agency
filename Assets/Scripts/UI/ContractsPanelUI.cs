using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Contract window (reference "Contract" and "Active Contract" screens).
/// Offers tab: contract cards with Decline / Accept. Active tab: "Contracted Mission" cards with a deadline pip bar,
/// the launch date of the rocket on the pad, a shortcut to the Rocket Designer and Abandon Project.
/// </summary>
public class ContractsPanelUI : MonoBehaviour
{
    public const float W = 198f, H = 290f;

    const int TabOffers = 0, TabGlobal = 1, TabActive = 2;
    int tab;                       // 0 = local offers, 1 = global contract finder (bidding), 2 = active
    bool built;
    TMP_Text title;
    Segmented tabs;
    RectTransform list;
    MissionInstance pendingAbandon;

    public void Init()
    {
        var r = (RectTransform)transform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -36f); r.sizeDelta = new Vector2(W, H);
        UIKit.Panel(r);

        title = UIKit.TitleBar(r, "Contracts", UIKit.T.teal, W, () => gameObject.SetActive(false));
        tabs = new Segmented(r, 10, 28, W - 20, 15, new[] { "Offers", "Global", "Active" }, 0, i => { tab = i; Refresh(); });
        list = UIKit.ScrollList(r, "List", 6, 48, W - 12, H - 54, null, 5f);
        built = true;

        // the global contract finder talks to a service; this build uses the offline stand-in (no backend yet)
        if (MockGlobalContractService.Instance == null) new GameObject("Global Contract Service").AddComponent<MockGlobalContractService>();
        svc = MockGlobalContractService.Instance;
        svc.Closed += OnAuctionClosed;
    }

    IGlobalContractService svc;
    readonly System.Collections.Generic.Dictionary<string, long> draft = new System.Collections.Generic.Dictionary<string, long>();

    void OnDestroy() { if (svc != null) svc.Closed -= OnAuctionClosed; }

    void OnAuctionClosed(GlobalListing l, bool won)
    {
        if (l.myBid <= 0) return;                                   // the player never bid on it
        if (won)
        {
            var mm = MissionManager.Instance;
            if (mm != null) mm.AcceptExternal(MockGlobalContractService.ToMission(l));
            Hud.Toast("You won \"" + l.title + "\" at $" + UIKit.Money(l.myBid) + ". It is in your Active contracts.");
        }
        else Hud.Toast("Lost \"" + l.title + "\": awarded to " + l.leader + " for $" + UIKit.Money(l.lowestBid) + ".");
    }

    void OnEnable()
    {
        if (!built) return;
        transform.SetAsLastSibling();
        if (MissionManager.Instance != null) MissionManager.Instance.Changed += Request;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed += Request;
        if (svc != null) svc.Changed += OnGlobalChanged;
        pendingAbandon = null;
        var mm = MissionManager.Instance;
        if (mm != null && mm.Available.Count == 0 && mm.Running.Count > 0 && tab == TabOffers) tab = TabActive;
        Refresh();
    }

    void OnDisable()
    {
        if (MissionManager.Instance != null) MissionManager.Instance.Changed -= Request;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed -= Request;
        if (svc != null) svc.Changed -= OnGlobalChanged;
    }

    bool dirty;
    void Request() { dirty = true; }                                  // rebuilt in Update, never while the mouse button is held
    void OnGlobalChanged() { if (tab == TabGlobal) dirty = true; }

    public void ShowOffers() { tab = 0; }
    public void ShowActive() { tab = TabActive; }
    public void ShowGlobal() { tab = TabGlobal; }

    void Update()
    {
        if (dirty && !UIKit.PointerHeld) { dirty = false; Refresh(); }
        // keep the deadline pips live while the game clock runs
        if (built && tab == TabActive && Time.frameCount % 20 == 0) RefreshPips();
        if (built && tab == TabGlobal && Time.unscaledTime >= nextLive) { nextLive = Time.unscaledTime + 0.4f; RefreshLive(); }
    }

    static string Months(MissionDefinition m)
    {
        double d = m.DurationDays;
        if (d >= 720) return Mathf.RoundToInt((float)(d / 365.0)) + " years";
        if (d >= 60) return Mathf.RoundToInt((float)(d / 30.0)) + " months";
        return Mathf.RoundToInt((float)d) + " days";
    }

    void Refresh()
    {
        if (!built) return;
        var mm = MissionManager.Instance;
        title.text = tab == TabOffers ? "Contracts" : tab == TabGlobal ? "Global Contract Finder" : "Contracted Mission";
        tabs.Select(tab);
        tabs.SetLabel(1, "Global" + (svc != null && svc.Listings.Count > 0 ? "  " + svc.Listings.Count : ""));
        tabs.SetLabel(2, "Active" + (mm != null && mm.Running.Count > 0 ? "  " + mm.Running.Count : ""));
        UIKit.Clear(list);
        pips.Clear();
        live.Clear();
        if (tab == TabGlobal) { BuildGlobal(); return; }
        if (mm == null) return;

        if (tab == TabOffers)
        {
            foreach (var m in mm.Available) OfferCard(m, mm);
            if (mm.Available.Count == 0) Empty("No contracts on offer right now.\nCheck back later.");
        }
        else
        {
            foreach (var m in mm.Running) ActiveCard(m, mm);
            foreach (var pm in mm.Pending) PendingCard(pm, mm);
            if (mm.Running.Count == 0 && mm.Pending.Count == 0) Empty("No contracted mission.\nAccept a contract from OFFERS.");
        }
    }

    void Empty(string s)
    {
        var r = UIKit.At(list, "Empty", 0, 0, 100, 60);
        r.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
        var t = UIKit.Text(r, s, 7.5f, TextAlignmentOptions.Center, false, UIKit.T.dim);
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    // one grey data row: "PAYLOAD   ROVER"
    void Field(Transform card, float x, float y, float w, string key, string val, string icon = null)
    {
        var r = UIKit.At(card, key, x, y, w, 11);
        UIKit.Tint(r, UIKit.T.chip);
        if (icon != null) UIKit.IconAt(r, Resources.Load<Sprite>("UIIcons/" + icon), 3, 1.5f, 8, 8, UIKit.T.dim);
        else UIKit.TextAt(r, "k", 3, 0, w * 0.5f, 11, key, 5.8f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
        var v = UIKit.TextAt(r, "v", icon != null ? 13 : w * 0.42f, 0, icon != null ? w - 16 : w * 0.58f - 3, 11, val, 6.2f, TextAlignmentOptions.MidlineRight);
        v.overflowMode = TextOverflowModes.Ellipsis; v.raycastTarget = false;
        r.gameObject.AddComponent<TooltipTrigger>().text = key;
    }

    RectTransform CardShell(float h, Color tint)
    {
        var r = UIKit.At(list, "Card", 0, 0, W - 16, h);
        r.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        UIKit.Tint(r, tint);
        return r;
    }

    void Details(RectTransform card, MissionDefinition m)
    {
        float w = W - 16;
        UIKit.TextAt(card, "Agency", 7, 3, w - 66, 11, m.displayName, 7.5f, TextAlignmentOptions.MidlineLeft, true).overflowMode = TextOverflowModes.Ellipsis;
        // difficulty as a three-part bar: easy lights one part, average two, hard three
        var diff = UIKit.At(card, "Difficulty", w - 52, 5, 46, 7);
        Color dc = m.Difficulty == 0 ? UIKit.T.green : m.Difficulty == 1 ? new Color(1f, 0.72f, 0.3f, 1f) : UIKit.T.red;
        for (int i = 0; i < 3; i++)
        {
            var seg = UIKit.At(diff, "Seg" + i, i * 16f, 1.5f, 14, 4);
            UIKit.Tint(seg, i <= m.Difficulty ? dc : new Color(1f, 1f, 1f, 0.16f)).raycastTarget = false;
        }
        var hit = UIKit.At(diff, "Hit", 0, 0, 46, 7); UIKit.Tint(hit, Color.clear);
        hit.gameObject.AddComponent<TooltipTrigger>().text = "Difficulty: " + m.DifficultyName + "\n" + (m.Difficulty == 0 ? "A long deadline." : m.Difficulty == 1 ? "A fair deadline." : "A short deadline. Plan well.");
        // description box
        var box = UIKit.At(card, "Description", 5, 16, w * 0.44f, 52);
        UIKit.Tint(box, UIKit.T.well);
        var d = UIKit.Text(box, string.IsNullOrEmpty(m.objective) ? m.missionType : m.objective, 6f, TextAlignmentOptions.TopLeft);
        UIKit.Fill((RectTransform)d.transform, 3, 3, 3, 3);
        d.textWrappingMode = TextWrappingModes.Normal; d.overflowMode = TextOverflowModes.Ellipsis; d.lineSpacing = -8f;
        // data rows
        float fx = 5 + w * 0.44f + 3, fw = w - fx - 5;
        string payload = m.payload != null && !string.IsNullOrEmpty(m.payload.type) ? m.payload.type : "-";
        Field(card, fx, 16, fw, "Payload", payload, "stat_payload");
        Field(card, fx, 28, fw, "Target", m.target ?? "-", "stat_orbit");
        Field(card, fx, 40, fw, "Maximum payload mass", m.massLimit.ToString("N0", CultureInfo.InvariantCulture) + " kg", "stat_mass");
        Field(card, fx, 52, fw, "Deadline", Months(m), "icon_time");
        // reward chip
        var rew = UIKit.At(card, "Reward", fx, 66, fw, 13);
        UIKit.Tint(rew, UIKit.T.well);
        UIKit.IconAt(rew, Resources.Load<Sprite>("UIIcons/icon_money"), 5, 2.5f, 8, 8, UIKit.SoftInk(UIKit.T.green));
        UIKit.Text(rew, "$" + UIKit.Money(m.budget) + " reward", 7f, TextAlignmentOptions.Center, true, UIKit.SoftInk(UIKit.T.green));
    }

    void PendingCard(MissionDefinition m, MissionManager mm)
    {
        float w = W - 16;
        var card = CardShell(100, new Color(1f, 0.72f, 0.3f, 0.10f));
        Details(card, m);
        UIKit.TextAt(card, "Pending", 7, 3, 60, 11, "", 6f, TextAlignmentOptions.MidlineLeft, true).raycastTarget = false;
        var tag = UIKit.At(card, "Tag", w - 62, 3, 56, 11); UIKit.Tint(tag, UIKit.Wash(new Color(1f, 0.72f, 0.3f, 1f), 0.8f));
        UIKit.Text(tag, "PENDING", 5.8f, TextAlignmentOptions.Center, true, new Color(0.2f, 0.1f, 0f, 1f));
        UIKit.SoftBtn(card, "Remove from pending", 8, 82, w - 16, 14, UIKit.T.red, () => { mm.RemovePending(m); Refresh(); }, 6.5f);
    }

    void OfferCard(MissionDefinition m, MissionManager mm)
    {
        float w = W - 16;
        var card = CardShell(100, new Color(1f, 1f, 1f, 0.10f));
        Details(card, m);
        UIKit.SoftBtn(card, "Decline", 8, 82, w * 0.5f - 11, 14, UIKit.T.red, () => mm.DiscardMission(m), 6.8f);
        bool busy = mm.Running.Count > 0;
        UIKit.SoftBtn(card, busy ? "Add as pending" : "Accept", w * 0.5f + 3, 82, w * 0.5f - 11, 14, busy ? UIKit.Amber : UIKit.T.green, () => { mm.StartMission(m); tab = TabActive; Refresh(); Hud.Toast(busy ? "Pending: " + m.displayName + " starts when the current contract is launched." : "Contract accepted: " + m.displayName); }, 6.5f);
    }

    // =============================================================== global contract finder (bidding front end)
    sealed class LiveCard { public GlobalListing l; public TMP_Text time, lowest, status, bids, bid; }
    readonly System.Collections.Generic.List<LiveCard> live = new System.Collections.Generic.List<LiveCard>();
    float nextLive;
    static readonly Color Orange = new Color(1f, 0.62f, 0.25f, 1f);

    static string Clock(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }

    static long Step(GlobalListing l) => System.Math.Max(100_000L, (long)(l.ceiling * 0.01 / 100_000) * 100_000L);

    long Draft(GlobalListing l)
    {
        if (!draft.TryGetValue(l.id, out var v))
            v = l.leader == null ? (long)(l.ceiling * 0.95) : l.lowestBid - Step(l);
        return System.Math.Min(l.ceiling, System.Math.Max(l.floor, v));
    }

    void BuildGlobal()
    {
        if (svc == null) return;
        float w = W - 16;

        // header: what this is, and the (pretend) server connection
        var head = CardShell(40, new Color(1f, 1f, 1f, 0.10f));
        UIKit.TextAt(head, "T", 7, 3, w - 14, 11, "Bid for contracts worldwide", 7.5f, TextAlignmentOptions.MidlineLeft, true);
        var dot = UIKit.Tint(UIKit.At(head, "Dot", 8, 19.5f, 4, 4), svc.Connected ? UIKit.T.green : UIKit.T.red);
        live.Add(null);                                                                 // keep indices simple: the header status is rebuilt with the list
        UIKit.TextAt(head, "Status", 15, 16, w - 20, 11, svc.Connected ? "Connected  ·  " + svc.OnlineAgencies.ToString("N0", CultureInfo.InvariantCulture) + " agencies online" : "Offline", 6.2f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
        var rule = UIKit.TextAt(head, "Rule", 7, 27, w - 14, 11, "Lowest bid wins. You are paid your bid.", 6f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
        live.Clear();
        head.gameObject.AddComponent<TooltipTrigger>().text = "Global contracts\nPosted by other agencies on the shared server.\nPlace a lower bid than the current best to take the lead.\nWhen the timer ends, the lowest bidder wins the contract.";

        foreach (var l in svc.Listings) GlobalCard(l, w);
        if (svc.Listings.Count == 0) Empty("No open auctions right now.\nNew contracts are posted all the time.");
    }

    void GlobalCard(GlobalListing l, float w)
    {
        var card = CardShell(110, new Color(1f, 1f, 1f, 0.10f));
        var lc = new LiveCard { l = l };

        // poster
        var cc = UIKit.At(card, "Country", 5, 3.5f, 17, 10);
        UIKit.Tint(cc, UIKit.Wash(UIKit.T.teal, 0.5f));
        UIKit.Text(cc, l.country, 5.8f, TextAlignmentOptions.Center, true);
        UIKit.TextAt(card, "Poster", 25, 3, w - 70, 11, l.poster, 6.8f, TextAlignmentOptions.MidlineLeft, true);
        lc.time = UIKit.TextAt(card, "Time", w - 46, 3, 42, 11, Clock(l.SecondsLeft), 7f, TextAlignmentOptions.MidlineRight, true);

        // mission
        var tt = UIKit.TextAt(card, "Title", 5, 15, w - 10, 11, l.title, 7.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.teal);
        tt.overflowMode = TextOverflowModes.Ellipsis;
        var box = UIKit.At(card, "Description", 5, 28, w * 0.44f, 30);
        UIKit.Tint(box, UIKit.T.well);
        var d = UIKit.Text(box, l.objective, 5.6f, TextAlignmentOptions.TopLeft);
        UIKit.Fill((RectTransform)d.transform, 3, 2, 3, 2);
        d.textWrappingMode = TextWrappingModes.Normal; d.overflowMode = TextOverflowModes.Ellipsis; d.lineSpacing = -8f;
        float fx = 5 + w * 0.44f + 3, fw = w - fx - 5;
        Field(card, fx, 28, fw, "Payload", l.payloadQuantity > 1 ? l.payloadType + " x" + l.payloadQuantity : l.payloadType);
        Field(card, fx, 40, fw, "Target", l.target);
        Field(card, fx, 52, fw, "Deadline", l.deadlineDays >= 60 ? Mathf.RoundToInt(l.deadlineDays / 30f) + " months" : l.deadlineDays + " days");

        // auction state
        float half = (w - 14) / 2f;
        Field(card, 5, 65, half, "Ceiling", "$" + UIKit.Money(l.ceiling));
        var lowRow = UIKit.At(card, "Lowest", 9 + half, 65, half, 11);
        UIKit.Tint(lowRow, UIKit.Wash(UIKit.T.green, 0.35f));
        UIKit.TextAt(lowRow, "k", 3, 0, half * 0.45f, 11, "Best bid", 5.8f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
        lc.lowest = UIKit.TextAt(lowRow, "v", half * 0.4f, 0, half * 0.6f - 3, 11, "", 6.2f, TextAlignmentOptions.MidlineRight, true);
        lc.status = UIKit.TextAt(card, "Status", 6, 78, w * 0.6f, 10, "", 6f, TextAlignmentOptions.MidlineLeft, true);
        lc.bids = UIKit.TextAt(card, "Bids", w * 0.55f, 78, w * 0.45f - 6, 10, "", 5.8f, TextAlignmentOptions.MidlineRight, false, UIKit.T.dim);

        // bid controls: [-]  $amount  [+]  [Place bid]
        string id = l.id;
        var minus = UIKit.Btn(card, "-", 5, 91, 14, 14, UIKit.T.chip, () => { draft[id] = Draft(l) - Step(l); lc.bid.text = "$" + UIKit.Money(Draft(l)); }, 8f);
        var amt = UIKit.At(card, "Amount", 21, 91, 52, 14);
        UIKit.Tint(amt, UIKit.T.well);
        lc.bid = UIKit.Text(amt, "$" + UIKit.Money(Draft(l)), 6.8f, TextAlignmentOptions.Center, true);
        UIKit.Btn(card, "+", 75, 91, 14, 14, UIKit.T.chip, () => { draft[id] = Draft(l) + Step(l); lc.bid.text = "$" + UIKit.Money(Draft(l)); }, 8f);
        UIKit.SoftBtn(card, "Place bid", 93, 91, w - 98, 14, UIKit.T.green, () =>
        {
            string err = svc.PlaceBid(id, Draft(l));
            if (err != null) Hud.Toast(err);
            else { draft.Remove(id); Hud.Toast("Bid placed on \"" + l.title + "\"."); }
        }, 6.5f);
        live.Add(lc);
        RefreshLive();
    }

    void RefreshLive()
    {
        foreach (var c in live)
        {
            if (c == null || c.l == null) continue;
            var l = c.l;
            if (c.time != null) { c.time.text = Clock(l.SecondsLeft); c.time.color = l.SecondsLeft < 30f ? new Color(1f, 0.45f, 0.42f, 1f) : UIKit.T.ink; }
            if (c.lowest != null) c.lowest.text = l.leader == null ? "none yet" : "$" + UIKit.Money(l.lowestBid);
            if (c.bids != null) c.bids.text = l.bidCount + (l.bidCount == 1 ? " bid" : " bids");
            if (c.status != null)
            {
                if (l.myBid <= 0) { c.status.text = l.leader == null ? "Be the first to bid" : "Leading: " + l.leader; c.status.color = UIKit.T.dim; }
                else if (l.IAmLeading) { c.status.text = "You lead at $" + UIKit.Money(l.myBid); c.status.color = UIKit.T.green; }
                else { c.status.text = "Outbid by " + l.leader; c.status.color = Orange; }
            }
        }
    }

    // deadline pips (purple = elapsed), refreshed live
    readonly System.Collections.Generic.List<(MissionInstance m, Image[] pips)> pips = new System.Collections.Generic.List<(MissionInstance, Image[])>();

    void ActiveCard(MissionInstance inst, MissionManager mm)
    {
        var m = inst.Definition;
        float w = W - 16;
        bool tracked = mm.Tracked == inst;
        var card = CardShell(126, tracked ? new Color(0.16f, 0.75f, 0.42f, 0.30f) : new Color(1f, 1f, 1f, 0.10f));
        Details(card, m);

        UIKit.TextAt(card, "DeadlineLabel", 7, 82, 40, 12, "Deadline", 7f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.dim);
        int n = Mathf.Clamp(Mathf.CeilToInt((float)(m.DurationDays / 30.0)), 4, 12);
        var imgs = new Image[n];
        float px = 46, pw = (w - px - 8) / n;
        for (int i = 0; i < n; i++)
        {
            var pr = UIKit.At(card, "Pip", px + i * pw, 85, pw - 2, 6);
            imgs[i] = UIKit.Tint(pr, UIKit.T.chip);
        }
        pips.Add((inst, imgs));
        RefreshPips();

        var dep = DeploymentManager.Instance;
        bool deployed = dep != null && dep.IsMissionDeployed(m.id) && dep.LaunchDate.HasValue;
        UIKit.TextAt(card, "Launch", 6, 94, w - 12, 10, deployed ? "Rocket on the pad. Launch " + dep.LaunchDate.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "No rocket deployed for this contract yet.",
            6f, TextAlignmentOptions.MidlineLeft, false, deployed ? UIKit.T.green : UIKit.T.dim);

        if (pendingAbandon == inst)
        {
            UIKit.SolidBtn(card, "Confirm abandon", 8, 107, w * 0.62f - 10, 14, UIKit.T.red, () => { pendingAbandon = null; mm.AbandonMission(inst); Hud.Toast("Contract abandoned (-5 reputation)."); }, 6f);
            UIKit.PlainBtn(card, "Keep", w * 0.62f + 1, 107, w * 0.38f - 9, 14, () => { pendingAbandon = null; Refresh(); }, 6f);
        }
        else
        {
            UIKit.SoftBtn(card, "Design rocket", 8, 107, w * 0.45f - 4, 14, UIKit.T.orange, () => { mm.TrackMission(inst); Panels.Open("rocket"); }, 6.5f);
            UIKit.SoftBtn(card, "Abandon", w * 0.45f + 6, 107, w * 0.55f - 14, 14, UIKit.T.red, () => { pendingAbandon = inst; Refresh(); }, 6.5f);
        }
    }

    void RefreshPips()
    {
        foreach (var (m, imgs) in pips)
        {
            if (m == null || imgs == null) continue;
            int lit = Mathf.FloorToInt(m.Progress * imgs.Length);
            for (int i = 0; i < imgs.Length; i++)
                if (imgs[i] != null) imgs[i].color = i < lit ? UIKit.T.teal : UIKit.T.chip;
        }
    }
}
