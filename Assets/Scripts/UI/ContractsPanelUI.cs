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

    int tab;                       // 0 = offers, 1 = active
    bool built;
    TMP_Text title;
    Segmented tabs;
    RectTransform list;
    MissionInstance pendingAbandon;

    public void Init()
    {
        var r = (RectTransform)transform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -32f); r.sizeDelta = new Vector2(W, H);
        UIKit.Glass(r);

        title = UIKit.TitleBar(r, "Contracts", UIKit.T.teal, W, () => gameObject.SetActive(false));
        tabs = new Segmented(r, 10, 28, W - 20, 15, new[] { "Offers", "Active" }, 0, i => { tab = i; Refresh(); });
        list = UIKit.ScrollList(r, "List", 6, 48, W - 12, H - 54, null, 5f);
        built = true;
    }

    void OnEnable()
    {
        if (!built) return;
        transform.SetAsLastSibling();
        if (MissionManager.Instance != null) MissionManager.Instance.Changed += Refresh;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed += Refresh;
        pendingAbandon = null;
        var mm = MissionManager.Instance;
        if (mm != null && mm.Available.Count == 0 && mm.Running.Count > 0) tab = 1;
        Refresh();
    }

    void OnDisable()
    {
        if (MissionManager.Instance != null) MissionManager.Instance.Changed -= Refresh;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed -= Refresh;
    }

    public void ShowOffers() { tab = 0; }
    public void ShowActive() { tab = 1; }

    void Update()
    {
        // keep the deadline pips live while the game clock runs
        if (built && tab == 1 && Time.frameCount % 20 == 0) RefreshPips();
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
        title.text = tab == 0 ? "Contracts" : "Contracted Mission";
        tabs.Select(tab);
        tabs.SetLabel(1, "Active" + (mm != null && mm.Running.Count > 0 ? "  " + mm.Running.Count : ""));
        UIKit.Clear(list);
        pips.Clear();
        if (mm == null) return;

        if (tab == 0)
        {
            foreach (var m in mm.Available) OfferCard(m, mm);
            if (mm.Available.Count == 0) Empty("No contracts on offer right now.\nCheck back later.");
        }
        else
        {
            foreach (var m in mm.Running) ActiveCard(m, mm);
            if (mm.Running.Count == 0) Empty("No contracted mission.\nAccept a contract from OFFERS.");
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
    void Field(Transform card, float x, float y, float w, string key, string val)
    {
        var r = UIKit.At(card, key, x, y, w, 11);
        UIKit.Tint(r, UIKit.T.chip);
        UIKit.TextAt(r, "k", 3, 0, w * 0.5f, 11, key, 5.8f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
        var v = UIKit.TextAt(r, "v", w * 0.42f, 0, w * 0.58f - 3, 11, val, 6.2f, TextAlignmentOptions.MidlineRight);
        v.overflowMode = TextOverflowModes.Ellipsis;
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
        UIKit.TextAt(card, "Agency", 7, 3, w - 14, 11, m.displayName, 7.5f, TextAlignmentOptions.MidlineLeft, true);
        // description box
        var box = UIKit.At(card, "Description", 5, 16, w * 0.44f, 52);
        UIKit.Tint(box, UIKit.T.well);
        var d = UIKit.Text(box, string.IsNullOrEmpty(m.objective) ? m.missionType : m.objective, 6f, TextAlignmentOptions.TopLeft);
        UIKit.Fill((RectTransform)d.transform, 3, 3, 3, 3);
        d.textWrappingMode = TextWrappingModes.Normal; d.overflowMode = TextOverflowModes.Ellipsis; d.lineSpacing = -8f;
        // data rows
        float fx = 5 + w * 0.44f + 3, fw = w - fx - 5;
        string payload = m.payload != null && !string.IsNullOrEmpty(m.payload.type) ? m.payload.type : "-";
        Field(card, fx, 16, fw, "Payload", payload);
        Field(card, fx, 28, fw, "Target", m.target ?? "-");
        Field(card, fx, 40, fw, "Mass", m.massLimit.ToString("N0", CultureInfo.InvariantCulture) + " kg");
        Field(card, fx, 52, fw, "Deadline", Months(m));
        // reward chip
        var rew = UIKit.At(card, "Reward", fx, 66, fw, 13);
        UIKit.Tint(rew, UIKit.Wash(UIKit.T.green, 0.6f));
        UIKit.Text(rew, "$" + UIKit.Money(m.budget) + " reward", 7f, TextAlignmentOptions.Center, true);
    }

    void OfferCard(MissionDefinition m, MissionManager mm)
    {
        float w = W - 16;
        var card = CardShell(100, new Color(1f, 1f, 1f, 0.10f));
        Details(card, m);
        UIKit.Btn(card, "Decline", 8, 82, w * 0.5f - 11, 14, UIKit.T.chip, () => mm.DiscardMission(m), 6.8f, true, new Color(1f, 0.45f, 0.42f, 1f));
        UIKit.Btn(card, "Accept", w * 0.5f + 3, 82, w * 0.5f - 11, 14, UIKit.T.green, () => { mm.StartMission(m); tab = 1; Refresh(); Hud.Toast("Contract accepted: " + m.displayName); }, 6.5f);
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
            UIKit.Btn(card, "Confirm abandon", 8, 107, w * 0.62f - 10, 14, UIKit.T.red, () => { pendingAbandon = null; mm.AbandonMission(inst); Hud.Toast("Contract abandoned (-5 reputation)."); }, 6f);
            UIKit.Btn(card, "Keep", w * 0.62f + 1, 107, w * 0.38f - 9, 14, UIKit.T.chip, () => { pendingAbandon = null; Refresh(); }, 6f);
        }
        else
        {
            UIKit.Btn(card, "Design rocket", 8, 107, w * 0.45f - 4, 14, UIKit.Wash(UIKit.T.orange), () => { mm.TrackMission(inst); Panels.Open("rocket"); }, 6.5f, true, UIKit.T.orange);
            UIKit.Btn(card, "Abandon", w * 0.45f + 6, 107, w * 0.55f - 14, 14, UIKit.T.chip, () => { pendingAbandon = inst; Refresh(); }, 6.5f, true, new Color(1f, 0.45f, 0.42f, 1f));
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
