using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Training and Testing programmes. Each one costs money and game time and needs a specific building.
/// Cards show the requirement, cost, duration and effect, and a Start button, a live progress bar, or the reason it is locked.
/// </summary>
public class ProgramsPanelUI : MonoBehaviour
{
    public const float W = 198f, H = 290f;
    int tab;                                   // 0 training, 1 testing
    bool built;
    TMP_Text title;
    Segmented tabs;
    RectTransform list;
    readonly List<(BaseJob job, Image fill, TMP_Text label)> live = new List<(BaseJob, Image, TMP_Text)>();
    float refreshTimer;

    public void Init()
    {
        var r = (RectTransform)transform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -32f); r.sizeDelta = new Vector2(W, H);
        UIKit.Panel(r);
        title = UIKit.TitleBar(r, "Training", UIKit.T.teal, W, () => gameObject.SetActive(false));
        tabs = new Segmented(r, 10, 28, W - 20, 15, new[] { "Training", "Testing" }, 0, i => { tab = i; Refresh(); });
        list = UIKit.ScrollList(r, "List", 6, 48, W - 12, H - 54, null, 5f);
        built = true;
    }

    public void SetTab(int t) { tab = t; if (built) tabs.Select(t); }

    void OnEnable()
    {
        if (!built) return;
        transform.SetAsLastSibling();
        if (BaseSim.Instance != null) BaseSim.Instance.Changed += Request;
        Agency.Changed += OnMoney;
        moneySig = MoneySig();
        tabs.Select(tab);
        Refresh();
    }

    void OnDisable()
    {
        if (BaseSim.Instance != null) BaseSim.Instance.Changed -= Request;
        Agency.Changed -= OnMoney;
    }

    bool dirty; string moneySig;
    void Request() { dirty = true; }
    // Money changes every game hour. Only rebuild the list when something the cards show (a lock reason) really changed.
    string MoneySig()
    {
        var sim = BaseSim.Instance; if (sim == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var p in BaseData.Programs) sb.Append(sim.WhyNot(p)).Append('|');
        return sb.ToString();
    }
    void OnMoney() { var s = MoneySig(); if (s != moneySig) { moneySig = s; dirty = true; } }

    void Update()
    {
        if (dirty && !UIKit.PointerHeld) { dirty = false; Refresh(); }       // never swap the buttons under a pressed pointer
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < 0.25f) return;
        refreshTimer = 0f;
        foreach (var (job, fill, label) in live)
        {
            if (fill == null) continue;
            ((RectTransform)fill.transform).anchorMax = new Vector2(Mathf.Clamp01(BaseSim.Progress(job)), 1f);
            label.text = BaseSim.DaysLeft(job) + " days left  ·  " + Mathf.FloorToInt(BaseSim.Progress(job) * 100f) + "%";
        }
    }

    void Refresh()
    {
        if (!built) return;
        var sim = BaseSim.Instance;
        title.text = tab == 0 ? "Training" : "Testing";
        tabs.Select(tab);
        UIKit.Clear(list);
        live.Clear();
        moneySig = MoneySig();
        if (sim == null) return;
        var kind = tab == 0 ? ProgramKind.Training : ProgramKind.Testing;
        foreach (var p in BaseData.Programs)
            if (p.kind == kind) Card(p, sim);
    }

    void Card(ProgramDef p, BaseSim sim)
    {
        var T = UIKit.T;
        float w = W - 16, h = 86;
        var card = UIKit.At(list, p.id, 0, 0, w, h);
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        UIKit.Tint(card, new Color(0f, 0f, 0f, 0.18f));

        UIKit.TextAt(card, "Name", 7, 4, w - 32, 11, p.name, 7.8f, TextAlignmentOptions.MidlineLeft, true);
        // the (i) button: read the benefit, cost, risk and chance again at any time (also while the programme runs or after it is done)
        var infoRt = UIKit.At(card, "Info", w - 22, 4, 13, 13);
        UIKit.MakeButton(infoRt, "", UIKit.Wash(T.blue, 0.55f), () => ShowProgramCard(sim, p, true));
        UIKit.Text(infoRt, "i", 7.5f, TextAlignmentOptions.Center, true).raycastTarget = false;
        infoRt.gameObject.AddComponent<TooltipTrigger>().text = "Details\nBenefit, cost, risk and chance of success.";
        var d = UIKit.TextAt(card, "Desc", 7, 15, w - 14, 16, p.description, 6.2f, TextAlignmentOptions.TopLeft, false, T.dim);
        d.textWrappingMode = TextWrappingModes.Normal; d.lineSpacing = -6f;

        bool hasBuilding = sim.LevelOf(p.building) >= p.minLevel;
        UIKit.TextAt(card, "Req", 7, 33, w * 0.55f, 10, (hasBuilding ? "Uses: " : "Needs: ") + BaseData.BuildingName(p.building) + (p.minLevel > 1 ? "  L" + p.minLevel : ""), 6.4f, TextAlignmentOptions.MidlineLeft, true,
            hasBuilding ? new Color(0.62f, 1f, 0.76f, 1f) : new Color(1f, 0.6f, 0.5f, 1f));
        float days = p.days * (p.kind == ProgramKind.Testing ? BaseSim.TestTimeFactor : 1f);
        UIKit.TextAt(card, "Cost", w * 0.45f, 33, w * 0.55f - 7, 10, "$" + p.costM.ToString("0.#", CultureInfo.InvariantCulture) + " M  ·  " + Mathf.CeilToInt(days) + " days  ·  " + Mathf.RoundToInt(BaseSim.ChanceOf(p) * 100f) + "%", 6.4f, TextAlignmentOptions.MidlineRight);

        var e = UIKit.TextAt(card, "Effect", 7, 44, w - 14, 16, "Effect: " + p.effect + (p.validDays > 0f ? "  (lasts " + Mathf.RoundToInt(p.validDays) + " days)" : ""), 6.2f, TextAlignmentOptions.TopLeft, false, new Color(0.62f, 1f, 0.76f, 1f));
        e.textWrappingMode = TextWrappingModes.Normal; e.lineSpacing = -6f;

        // action row: start, progress, completed or locked
        var job = sim.Job(p.id);
        if (sim.IsDone(p.id))
        {
            var r = UIKit.At(card, "Done", 7, 66, w - 14, 14);
            UIKit.Tint(r, UIKit.Wash(T.green, 0.4f));
            int left = sim.DaysValid(p.id);
            UIKit.Text(r, left > 0 ? "Completed  ·  fades in " + left + " d" : "Completed", 6.8f, TextAlignmentOptions.Center, true);
        }
        else if (job != null)
        {
            var track = UIKit.At(card, "Progress", 7, 66, w - 14, 14);
            UIKit.Tint(track, T.track);
            var fillRt = UIKit.At(track, "Fill", 0, 0, 10, 14);
            fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(BaseSim.Progress(job), 1); fillRt.pivot = new Vector2(0, 0.5f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fill = UIKit.Tint(fillRt, T.teal);
            var label = UIKit.Text(track, "", 6.5f, TextAlignmentOptions.Center, true);
            live.Add((job, fill, label));
        }
        else
        {
            string why = sim.WhyNot(p);
            if (why == null)
                UIKit.Btn(card, "Start  ·  $" + p.costM.ToString("0.#", CultureInfo.InvariantCulture) + " M", 7, 66, w - 14, 14, T.teal, () => ShowProgramCard(sim, p, false), 7f);
            else
            {
                var b = UIKit.Btn(card, why, 7, 66, w - 14, 14, T.chip, null, 6.5f, true, T.dim);
                b.interactable = false;
            }
        }
    }

    // before a training or test starts: the consequence card (benefit, cost, risk and the chance of success)
    static void ShowProgramCard(BaseSim sim, ProgramDef p, bool infoOnly)
    {
        float days = p.days * (p.kind == ProgramKind.Testing ? BaseSim.TestTimeFactor : 1f);
        int fails = BaseSave.Current.FailsOf(p.id);
        var benefits = new System.Collections.Generic.List<string> { p.effect };
        if (p.validDays > 0f) benefits.Add("Valid for " + Mathf.RoundToInt(p.validDays) + " days, then it fades");
        var costs = new[] { "$" + p.costM.ToString("0.#", CultureInfo.InvariantCulture) + " M, paid now", Mathf.CeilToInt(days) + " days of game time", BaseData.BuildingName(p.building) + " is busy until it ends" };
        var risks = new System.Collections.Generic.List<string> { "If it fails, the money and the days are lost for good" };
        if (fails > 0) risks.Add("Earlier failures make this try " + fails * 8 + "% safer");
        if (p.validDays > 0f) risks.Add("You must repeat it when it fades");
        string title = p.name;
        if (infoOnly)
        {
            var job = sim.Job(p.id);
            title += sim.IsDone(p.id) ? "  ·  completed" : job != null ? "  ·  running, " + BaseSim.DaysLeft(job) + " d left" : "";
        }
        ImpactCard.Show(title, benefits.ToArray(), costs, risks.ToArray(), BaseSim.ChanceOf(p), infoOnly ? (System.Action)null : () => sim.StartProgram(p), null, "Start");
    }
}
