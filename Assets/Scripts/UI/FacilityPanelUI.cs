using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Facility menu: a grid of buildings, each with a picture of the building, what it unlocks (stars), what it does,
/// the power it needs, its price and how many days it takes. Build -> pick a plot on the map -> confirm. Upgrades are confirmed too.
/// Opened from the bottom dock (Building > Facility) or by clicking an empty plot (then that plot is already chosen).
/// </summary>
public class FacilityPanelUI : MonoBehaviour
{
    public const float W = 310f, H = 300f;
    /// <summary>The empty plot the menu was opened from (-1 = none: the player is asked to pick one after choosing a building).</summary>
    public static int PlotContext = -1;

    public static void ResetStatics() { PlotContext = -1; }

    bool built;
    RectTransform list;
    TMP_Text powerText, contextText;
    RectTransform powerFill;
    Sprite starIcon, powerIcon, moneyIcon, timeIcon;
    float refreshTimer;
    readonly List<(BaseJob job, Image fill, TMP_Text label)> live = new List<(BaseJob, Image, TMP_Text)>();

    public static void OpenForPlot(int plot)
    {
        PlotContext = plot;
        Panels.Open("facility");
    }

    public void Init()
    {
        var r = (RectTransform)transform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -32f); r.sizeDelta = new Vector2(W, H);
        UIKit.Panel(r);
        UIKit.TitleBar(r, "Facility", UIKit.T.teal, W, () => gameObject.SetActive(false));
        starIcon = Resources.Load<Sprite>("UIIcons/icon_star"); powerIcon = Resources.Load<Sprite>("UIIcons/icon_power");
        moneyIcon = Resources.Load<Sprite>("UIIcons/icon_money"); timeIcon = Resources.Load<Sprite>("UIIcons/icon_time");

        // power strip: how much of the grid's power is used, and which plot (if any) is chosen
        var strip = UIKit.At(r, "Power", 6, 27, W - 12, 20);
        UIKit.Tint(strip, new Color(1f, 1f, 1f, 0.10f));
        UIKit.IconAt(strip, powerIcon, 5, 4.5f, 11, 11, new Color(1f, 0.85f, 0.35f, 1f));
        powerText = UIKit.TextAt(strip, "Label", 19, 1, 130, 11, "", 6.6f, TextAlignmentOptions.MidlineLeft, true);
        var track = UIKit.At(strip, "Track", 19, 13, 150, 4);
        UIKit.Tint(track, UIKit.T.track);
        powerFill = UIKit.At(track, "Fill", 0, 0, 10, 4);
        powerFill.anchorMin = Vector2.zero; powerFill.anchorMax = new Vector2(0.5f, 1f); powerFill.offsetMin = powerFill.offsetMax = Vector2.zero;
        UIKit.Tint(powerFill, UIKit.T.teal);
        contextText = UIKit.TextAt(strip, "Context", 172, 0, W - 12 - 178, 20, "", 6.3f, TextAlignmentOptions.MidlineRight, true);
        strip.gameObject.AddComponent<TooltipTrigger>().text = "Power\nEvery building draws power from the Power Grid.\nUpgrade the grid to build more.";

        list = UIKit.ScrollList(r, "Grid", 6, 51, W - 12, H - 57, null, 4f, true, new Vector2((W - 12 - 4f - 4f) / 2f, 172f), 2);
        ((GridLayoutGroup)list.GetComponent<GridLayoutGroup>()).padding = new RectOffset(0, 0, 0, 0);
        built = true;
    }

    void OnEnable()
    {
        if (!built) return;
        transform.SetAsLastSibling();
        if (BaseSim.Instance != null) BaseSim.Instance.Changed += Refresh;
        Agency.Changed += Refresh;
        var sim = BaseSim.Instance;
        if (PlotContext >= 0 && sim != null && !sim.PlotFree(PlotContext)) PlotContext = -1;
        Refresh();
    }

    void OnDisable()
    {
        if (BaseSim.Instance != null) BaseSim.Instance.Changed -= Refresh;
        Agency.Changed -= Refresh;
    }

    void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < 0.25f) return;
        refreshTimer = 0f;
        foreach (var (job, fill, label) in live)
        {
            if (fill == null) continue;
            ((RectTransform)fill.transform).anchorMax = new Vector2(Mathf.Clamp01(BaseSim.Progress(job)), 1f);
            label.text = (job.level <= 1 ? "Building" : "Upgrading") + "  ·  " + BaseSim.DaysLeft(job) + " days left";
        }
    }

    // ---------------------------------------------------------------- the grid
    void Refresh()
    {
        if (!built) return;
        var sim = BaseSim.Instance;
        UIKit.Clear(list);
        live.Clear();
        if (sim == null) return;

        float used = sim.PowerDemand, supply = sim.PowerSupply;
        powerText.text = "Power  " + used.ToString("0") + " / " + supply.ToString("0") + " MW";
        powerFill.anchorMax = new Vector2(Mathf.Clamp01(used / Mathf.Max(1f, supply)), 1f);
        ((Image)powerFill.GetComponent<Image>()).color = used / supply > 0.9f ? new Color(1f, 0.55f, 0.35f, 1f) : UIKit.T.teal;
        contextText.text = PlotContext >= 0 ? "Building on P" + (PlotContext + 1) : "You pick the plot next";
        contextText.color = PlotContext >= 0 ? new Color(0.62f, 1f, 0.76f, 1f) : UIKit.T.dim;

        foreach (var b in BaseData.Buildings)
        {
            if (!b.listed) continue;
            Card(b, sim);
        }
    }

    void Card(BuildingDef b, BaseSim sim)
    {
        var T = UIKit.T;
        float cw = (W - 12 - 4f - 4f) / 2f, ch = 172f;
        int level = sim.LevelOf(b.id);
        var job = sim.Job(b.id);
        int next = Mathf.Min(level + 1, b.MaxLevel);
        int showLevel = job != null ? Mathf.Max(job.level, 1) : (level >= b.MaxLevel ? level : Mathf.Max(1, next));

        var card = UIKit.At(list, b.id, 0, 0, cw, ch);
        UIKit.Tint(card, new Color(1f, 1f, 1f, 0.10f));

        // name and level
        UIKit.TextAt(card, "Name", 6, 3, cw - 12, 11, b.name, 7.4f, TextAlignmentOptions.MidlineLeft, true).overflowMode = TextOverflowModes.Ellipsis;
        string state = level == 0 ? "Not built" : "Level " + level + " · " + BuildingDef.LevelNames[Mathf.Min(level, 3)];
        UIKit.TextAt(card, "State", 6, 13, cw - 12, 8, state, 5.6f, TextAlignmentOptions.MidlineLeft, true, level > 0 ? new Color(0.62f, 1f, 0.76f, 1f) : T.dim);

        // picture of the building (a screenshot of the game model) with the stars above it
        var pic = UIKit.At(card, "Picture", 4, 23, cw - 8, 70);
        UIKit.Tint(pic, new Color(0.03f, 0.07f, 0.12f, 0.55f));
        var tex = FacilityPreview.Get(b.id, showLevel);
        if (tex != null)
        {
            var raw = UIKit.At(pic, "Image", 0, 0, cw - 8, 70);
            UIKit.Fill(raw);
            var ri = raw.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
            ri.uvRect = new Rect(0f, 0.02f, 1f, 0.96f);
        }
        // what it unlocks at the level you would get
        var unlocks = BaseData.UnlocksAt(b.id, showLevel);
        float ux = 3f, uy = 3f;
        int shown = 0;
        foreach (var u in unlocks)
        {
            if (shown >= 3) break;
            string text = u.Length > 30 ? u.Substring(0, 29) + "…" : u;
            float tw = Mathf.Min(cw - 16f, 11f + text.Length * 2.55f);
            var chip = UIKit.At(pic, "Unlock", ux, uy, tw, 9);
            UIKit.Tint(chip, new Color(0.02f, 0.05f, 0.1f, 0.78f));
            UIKit.IconAt(chip, starIcon, 2f, 1.3f, 6.4f, 6.4f, new Color(1f, 0.82f, 0.3f, 1f));
            UIKit.TextAt(chip, "T", 9.5f, 0, tw - 10, 9, text, 5.3f, TextAlignmentOptions.MidlineLeft);
            uy += 10f; shown++;
        }
        if (unlocks.Count > shown)
        {
            var more = UIKit.At(pic, "More", ux, uy, 28, 9);
            UIKit.Tint(more, new Color(0.02f, 0.05f, 0.1f, 0.78f));
            UIKit.Text(more, "+" + (unlocks.Count - shown) + " more", 5.2f, TextAlignmentOptions.Center, false, T.dim);
        }
        pic.gameObject.AddComponent<TooltipTrigger>().text = b.name + "  ·  Level " + showLevel + "\n" + (unlocks.Count > 0 ? "★ " + string.Join("\n★ ", unlocks) : "Nothing new");

        // what it does
        var d = UIKit.TextAt(card, "Does", 6, 95, cw - 12, 17, b.description, 5.6f, TextAlignmentOptions.TopLeft, false, T.dim);
        d.textWrappingMode = TextWrappingModes.Normal; d.lineSpacing = -8f; d.overflowMode = TextOverflowModes.Ellipsis;

        // power, price and time of the level you would build
        float cost = BaseSim.CostOf(b, next), days = BaseSim.DaysOf(b, next), power = BaseData.PowerOf(b, next);
        float sx = 4f, sw = (cw - 8f - 4f) / 3f;
        Stat(card, sx, 113, sw, powerIcon, b.id == "PowerGrid" ? "+" + (BaseData.GridSupplyMW[Mathf.Clamp(next - 1, 0, 2)] - BaseData.GridSupplyMW[Mathf.Clamp(level - 1, 0, 2)]).ToString("0") + " MW" : power.ToString("0") + " MW", new Color(1f, 0.85f, 0.35f, 1f),
            b.id == "PowerGrid" ? "Power it adds to the base" : "Power this building needs");
        Stat(card, sx + sw + 2f, 113, sw, moneyIcon, "$" + cost.ToString("0.#", CultureInfo.InvariantCulture) + "M", new Color(0.62f, 1f, 0.76f, 1f), "Construction cost");
        Stat(card, sx + 2 * (sw + 2f), 113, sw, timeIcon, Mathf.CeilToInt(days) + " days", new Color(0.7f, 0.85f, 1f, 1f), "Construction time");

        // action
        var act = UIKit.At(card, "Action", 4, 130, cw - 8, 18);
        if (job != null)
        {
            var track = UIKit.At(act, "Progress", 0, 2, cw - 8, 14);
            UIKit.Tint(track, T.track);
            var fillRt = UIKit.At(track, "Fill", 0, 0, 10, 14);
            fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(BaseSim.Progress(job), 1); fillRt.pivot = new Vector2(0, 0.5f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fill = UIKit.Tint(fillRt, T.orange);
            var label = UIKit.Text(track, "", 6.2f, TextAlignmentOptions.Center, true);
            live.Add((job, fill, label));
        }
        else if (level >= b.MaxLevel)
        {
            var done = UIKit.Btn(act, "Fully upgraded", 0, 1, cw - 8, 16, T.chip, null, 6.4f, true, T.dim);
            done.interactable = false;
        }
        else
        {
            bool newBuild = level == 0 && b.needsPlot;
            int plot = PlotContext;
            string why = sim.WhyNotBuild(b, newBuild ? plot : -1);
            string verb = newBuild ? (plot >= 0 ? "Build on P" + (plot + 1) : "Build…") : level == 0 ? "Build" : "Upgrade to level " + next;
            if (why == null)
            {
                var btn = UIKit.Btn(act, verb, 0, 1, cw - 8, 16, T.green, () => Choose(b, newBuild), 6.6f);
                if (newBuild && plot < 0) btn.gameObject.AddComponent<TooltipTrigger>().text = "Build\nYou will pick the plot on the map next.";
            }
            else
            {
                var bt = UIKit.Btn(act, why, 0, 1, cw - 8, 16, UIKit.Wash(T.orange, 0.26f), null, 6.2f, true, new Color(1f, 0.82f, 0.55f, 1f));
                bt.interactable = false;
                var cb = bt.colors; cb.disabledColor = Color.white; bt.colors = cb;                     // a blocked button keeps its colours so the reason is readable
            }
        }
    }

    void Stat(RectTransform card, float x, float y, float w, Sprite icon, string value, Color tint, string tip)
    {
        var r = UIKit.At(card, "Stat", x, y, w, 15);
        UIKit.Tint(r, new Color(0f, 0f, 0f, 0.20f));
        UIKit.IconAt(r, icon, 3, 3.2f, 8.5f, 8.5f, tint);
        UIKit.TextAt(r, "V", 12.5f, 0, w - 13f, 15, value, 5.9f, TextAlignmentOptions.MidlineLeft, true);
        r.gameObject.AddComponent<TooltipTrigger>().text = tip;
    }

    // ---------------------------------------------------------------- choosing
    void Choose(BuildingDef b, bool newBuild)
    {
        var sim = BaseSim.Instance;
        if (sim == null) return;
        if (!newBuild)
        {
            int next = sim.LevelOf(b.id) + 1;
            string body = "Cost  <b>$" + BaseSim.CostOf(b, next).ToString("0.#", CultureInfo.InvariantCulture) + " M</b>\nTime  <b>" + Mathf.CeilToInt(BaseSim.DaysOf(b, next)) + " days</b>\nPower  <b>" + BaseData.PowerOf(b, next).ToString("0") + " MW</b>";
            UIConfirm.Show((sim.LevelOf(b.id) == 0 ? "Build the " : "Upgrade the ") + b.name + (sim.LevelOf(b.id) == 0 ? "?" : " to level " + next + "?"), body, sim.LevelOf(b.id) == 0 ? "Build" : "Upgrade", () => sim.StartBuild(b));
            return;
        }
        if (PlotContext >= 0)
        {
            int plot = PlotContext;
            string body = "Cost  <b>$" + BaseSim.CostOf(b, 1).ToString("0.#", CultureInfo.InvariantCulture) + " M</b>\nTime  <b>" + Mathf.CeilToInt(BaseSim.DaysOf(b, 1)) + " days</b>\nPower  <b>" + BaseData.PowerOf(b, 1).ToString("0") + " MW</b>";
            UIConfirm.Show("Build the " + b.name + " on P" + (plot + 1) + "?", body, "Build here", () => { PlotContext = -1; sim.StartBuild(b, plot); });
            return;
        }
        // no plot yet: hide the menu and let the player pick one on the map
        gameObject.SetActive(false);
        PlotPicker.Begin(b, plot => { sim.StartBuild(b, plot); PlotContext = -1; Panels.Open("facility"); }, () => Panels.Open("facility"));
    }
}
