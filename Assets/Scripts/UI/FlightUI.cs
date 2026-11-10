using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The windows around a launch: the pre-launch warning, the launch report and the final mission result with stars and reward.</summary>
public static class LaunchResultUI
{
    static GameObject current;
    static int prevSpeed = -1;
    public static void ResetStatics() { current = null; prevSpeed = -1; }

    public static readonly Color Gold = new Color(1f, 0.82f, 0.28f, 1f), Amber = new Color(1f, 0.72f, 0.3f, 1f);

    static RectTransform Modal(float w, float h)
    {
        Close();
        var hud = Object.FindAnyObjectByType<HudController>();
        if (hud == null) return null;
        var block = new GameObject("Result Blocker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        block.SetParent((RectTransform)hud.transform, false);
        UIKit.Fill(block);
        block.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        block.SetAsLastSibling();
        current = block.gameObject;
        var card = UIKit.At(block, "Card", 0, 0, w, h);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition = Vector2.zero;
        UIKit.Panel(card);
        card.gameObject.AddComponent<CanvasGroup>(); card.gameObject.AddComponent<UIPopIn>();
        var clock = GameClock.Instance;
        if (clock != null && clock.Speed > 0f) { prevSpeed = clock.SpeedIndex; clock.SetSpeedIndex(0); }
        return card;
    }

    public static void Close()
    {
        if (current != null) Object.Destroy(current);
        current = null;
        var clock = GameClock.Instance;
        if (prevSpeed > 0 && clock != null) clock.SetSpeedIndex(prevSpeed);
        prevSpeed = -1;
    }

    public static void Chip(RectTransform parent, float x, float y, int sev, float size = 8f)
    {
        Color col = sev == 0 ? UIKit.T.green : sev == 1 ? Amber : UIKit.T.red;
        var chip = UIKit.At(parent, "Icon", x, y, size, size);
        UIKit.Tint(chip, UIKit.Wash(col, 0.9f)).raycastTarget = false;
        if (sev == 0) UIKit.IconAt(chip, Resources.Load<Sprite>("UIIcons/icon_check"), size * 0.125f, size * 0.125f, size * 0.75f, size * 0.75f, new Color(0.04f, 0.14f, 0.08f, 1f));
        else UIKit.TextAt(chip, "G", 0, 0, size, size, sev == 1 ? "!" : "x", size * 0.75f, TextAlignmentOptions.Center, true, new Color(0.15f, 0.05f, 0.03f, 1f)).raycastTarget = false;
    }

    static Color ResultColor(LaunchResult r) => r == LaunchResult.Success ? UIKit.T.green : r == LaunchResult.Partial ? Amber : UIKit.T.red;

    /// <summary>final = false: the launch report right after the cutscene. final = true: the mission has finished, reward paid.</summary>
    public static void Show(FlightRecord rec, bool final)
    {
        const float w = 220f;
        int n = Mathf.Min(rec.lines.Count, 8);
        float h = final ? 168f : 112f + n * 11f + (rec.result == (int)LaunchResult.Failure ? 12f : 20f);
        var card = Modal(w, h);
        if (card == null) return;
        var res = rec.Result;
        Color col = ResultColor(res);
        var accent = UIKit.At(card, "Accent", 0, 0, w, 3); UIKit.Tint(accent, col).raycastTarget = false;

        string title = final ? "MISSION COMPLETE" : res == LaunchResult.Success ? "LAUNCH SUCCESSFUL" : res == LaunchResult.Partial ? "PARTIAL SUCCESS" : "LAUNCH FAILED";
        UIKit.TextAt(card, "Title", 12, 10, w - 24, 14, title, 10f, TextAlignmentOptions.MidlineLeft, true, col);
        string sub = (string.IsNullOrEmpty(rec.rocketName) ? "Rocket" : rec.rocketName) + (string.IsNullOrEmpty(rec.missionName) ? "" : "  ·  " + rec.missionName);
        var st = UIKit.TextAt(card, "Sub", 12, 25, w - 24, 9, sub, 6.2f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim); st.overflowMode = TextOverflowModes.Ellipsis;

        float y = 40f;
        if (final)
        {
            string word = res == LaunchResult.Partial ? "Partial success" : FlightOutcome.StarWord(rec.stars);
            for (int i = 0; i < 3; i++)
                UIKit.IconAt(card, Resources.Load<Sprite>("UIIcons/icon_star"), w * 0.5f - 33f + i * 23f, y, 20, 20, i < rec.stars ? Gold : new Color(1f, 1f, 1f, 0.16f));
            UIKit.TextAt(card, "Word", 0, y + 22f, w, 11, "Mission rating: " + word, 7.4f, TextAlignmentOptions.Center, true, rec.stars > 0 ? Gold : UIKit.T.dim);
            y += 40f;
            var box = UIKit.At(card, "Reward", 12, y, w - 24, 34); UIKit.Tint(box, new Color(1f, 1f, 1f, 0.1f));
            UIKit.IconAt(box, Resources.Load<Sprite>("UIIcons/icon_money"), 8, 8, 18, 18, UIKit.T.priceText);
            UIKit.TextAt(box, "L", 32, 4, 100, 9, rec.reward > 0 ? "Reward from the contract" : "No contract reward", 5.8f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
            UIKit.TextAt(box, "V", 32, 13, 130, 17, rec.reward > 0 ? "+$" + UIKit.Money(rec.reward) : "$0", 12f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.priceText);
            int rep = res == LaunchResult.Success ? 10 + rec.stars * 5 : 5;
            UIKit.TextAt(box, "R", w - 24 - 64, 11, 58, 12, "+" + rep + " reputation", 6f, TextAlignmentOptions.MidlineRight, true, UIKit.T.ink);
            y += 40f;
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                var line = rec.lines[i];
                Chip(card, 12, y + 1f, line.sev);
                var t = UIKit.TextAt(card, "L", 24, y, w - 36, 10, line.text, 6.2f, TextAlignmentOptions.MidlineLeft, false, line.sev == 0 ? UIKit.T.ink : line.sev == 1 ? Amber : UIKit.T.red);
                t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
                y += 11f;
            }
            y += 4f;
            if (!string.IsNullOrEmpty(rec.cause))
            {
                var c = UIKit.TextAt(card, "Cause", 12, y, w - 24, 20, rec.cause, 6.2f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
                c.textWrappingMode = TextWrappingModes.Normal; c.lineSpacing = -4f; y += 18f;
            }
            if (res == LaunchResult.Failure)
            {
                UIKit.TextAt(card, "Lost", 12, y, w - 24, 11, "The contract is lost  ·  -8 reputation", 6.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.red);
            }
            else
            {
                UIKit.TextAt(card, "Fl", 12, y, w - 24, 9, "Mission in flight  ·  " + Mathf.CeilToInt(rec.durationDays) + " days", 6.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.ink);
                var track = UIKit.At(card, "Track", 12, y + 10f, w - 24, 5); UIKit.Tint(track, UIKit.T.track);
                UIKit.TextAt(card, "Hint", 12, y + 16f, w - 24, 8, "Follow it under Objectives. Reward comes when it finishes.", 5.2f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
            }
        }
        UIKit.Btn(card, "Continue", w - 12 - 70, h - 26f, 70, 18, UIKit.Wash(col, 0.7f), Close, 7f);
    }

    /// <summary>Before the launch: lists what is not met. The player may still go ahead.</summary>
    public static void PreLaunch(Deployment dep, PartLibrary lib, System.Action proceed)
    {
        var mission = MissionManager.Instance != null && !string.IsNullOrEmpty(dep.missionId) ? MissionManager.Instance.Running.FirstOrDefault(x => x.Definition.id == dep.missionId)?.Definition : null;
        var design = RocketDesign.FromJson(dep.design);
        design.stack.RemoveAll(id => lib.Get(id) == null);
        var rep = RocketStats.Evaluate(design, lib, mission);
        var core = RocketRules.MissingCore(design, lib);
        var bad = rep.checks.Where(c => c.sev != Sev.Ok).ToList();
        if (bad.Count == 0 && core.Count == 0) { proceed(); return; }

        const float w = 226f;
        int n = Mathf.Min(bad.Count + (core.Count > 0 ? 1 : 0), 8);
        float h = 78f + n * 12f + 26f;
        var card = Modal(w, h);
        if (card == null) { proceed(); return; }
        UIKit.Tint(UIKit.At(card, "Accent", 0, 0, w, 3), Amber).raycastTarget = false;
        UIKit.TextAt(card, "Title", 12, 10, w - 24, 13, "Pre-launch check", 9.5f, TextAlignmentOptions.MidlineLeft, true, Amber);
        var sub = UIKit.TextAt(card, "Sub", 12, 25, w - 24, 20, "Some requirements are not met. You can still launch, but the rocket may fail or only partly succeed.", 6.2f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
        sub.textWrappingMode = TextWrappingModes.Normal; sub.lineSpacing = -4f;
        float y = 52f;
        int shown = 0;
        if (core.Count > 0) { Chip(card, 12, y + 1f, 2); UIKit.TextAt(card, "Core", 24, y, w - 36, 11, "Missing " + string.Join(", ", core), 6.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.red); y += 12f; shown++; }
        foreach (var c in bad.OrderByDescending(c => c.sev))
        {
            if (shown++ >= 8) break;
            Chip(card, 12, y + 1f, c.sev == Sev.Warn ? 1 : 2);
            var t = UIKit.TextAt(card, "L", 24, y, w - 36, 11, c.text, 6f, TextAlignmentOptions.MidlineLeft, false, c.sev == Sev.Warn ? Amber : UIKit.T.red);
            t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
            y += 12f;
        }
        float by = h - 26f;
        UIKit.Btn(card, "Cancel", 12, by, 70, 18, UIKit.T.chip, Close, 7f);
        UIKit.Btn(card, "Launch anyway", w - 12 - 100, by, 100, 18, UIKit.Wash(Amber, 0.75f), () => { Close(); proceed(); }, 7f);
    }
}

/// <summary>The "Mission in flight" panel that hangs under the Objectives button / panel while a launched rocket is on its way.
/// Click its header to expand it: result so far, the steps of the flight, the rating and the reward that is waiting.</summary>
public class FlightPanelUI : MonoBehaviour
{
    const float W = 112f, Hc = 36f;
    RectTransform panel;
    Image fill;
    TMP_Text info, pct, chevron;
    ObjectiveMenuUI objectives;
    bool expanded;
    FlightRecord shown;
    float y = -58f, x = 8f, height = Hc;

    public static FlightPanelUI Create(RectTransform canvas, ObjectiveMenuUI obj)
    {
        var go = new GameObject("Flight Panel", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var prt = (RectTransform)go.transform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<FlightPanelUI>();
        ui.objectives = obj;
        ui.panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();      // a child: this component stays active so it can switch the panel on
        ui.panel.SetParent(prt, false);
        ui.panel.anchorMin = ui.panel.anchorMax = new Vector2(0f, 1f); ui.panel.pivot = new Vector2(0f, 1f);
        ui.panel.anchoredPosition = new Vector2(8f, -58f); ui.panel.sizeDelta = new Vector2(W, Hc);
        UIKit.Panel(ui.panel);
        ui.panel.gameObject.AddComponent<UIPopIn>(); ui.panel.gameObject.AddComponent<CanvasGroup>();
        ui.panel.gameObject.SetActive(false);
        return ui;
    }

    // everything inside the panel is rebuilt when the flight changes or the panel is expanded / collapsed
    void Rebuild(FlightRecord c)
    {
        shown = c;
        for (int i = panel.childCount - 1; i >= 0; i--) Destroy(panel.GetChild(i).gameObject);
        float w = W;
        float h = Hc;
        if (expanded) h = Hc + 8f + 12f + Mathf.Min(c.lines.Count, 6) * 10f + 16f + (string.IsNullOrEmpty(c.cause) ? 0f : 14f);
        panel.sizeDelta = new Vector2(W, h); height = h;

        var head = UIKit.At(panel, "Head", 0, 0, W, Hc);
        var hb = UIKit.MakeButton(head, "", Color.clear, () => { expanded = !expanded; Rebuild(c); });
        UIKit.IconAt(head, Resources.Load<Sprite>("UIIcons/icon_launch"), 5, 4, 9, 9, UIKit.T.ink).raycastTarget = false;
        UIKit.TextAt(head, "Title", 17, 3, W - 32, 10, "Mission in flight", 6.2f, TextAlignmentOptions.MidlineLeft, true).raycastTarget = false;
        chevron = UIKit.TextAt(head, "Chev", W - 14, 3, 10, 10, expanded ? "v" : ">", 6.6f, TextAlignmentOptions.Center, true, UIKit.T.dim);
        chevron.raycastTarget = false;
        info = UIKit.TextAt(head, "Info", 5, 14, W - 10, 8, "", 5.4f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
        info.overflowMode = TextOverflowModes.Ellipsis; info.raycastTarget = false;
        var track = UIKit.At(head, "Track", 5, 25, W - 36, 6);
        UIKit.Tint(track, UIKit.T.track).raycastTarget = false;
        var f = UIKit.At(track, "Fill", 0, 0, 10, 6); UIKit.Fill(f);
        fill = UIKit.Tint(f, UIKit.T.teal); fill.raycastTarget = false;
        pct = UIKit.TextAt(head, "Pct", W - 29, 22, 24, 11, "", 5.8f, TextAlignmentOptions.MidlineRight, true);
        pct.raycastTarget = false;
        head.gameObject.AddComponent<TooltipTrigger>().text = "Mission in flight\nClick to " + (expanded ? "collapse" : "expand") + ".\nThe reward arrives when the bar is full.";

        if (!expanded) return;
        float yy = Hc + 2f;
        var res = c.Result;
        Color rc = res == LaunchResult.Success ? UIKit.T.green : new Color(1f, 0.72f, 0.3f, 1f);
        UIKit.TextAt(panel, "Result", 6, yy, W - 12, 11, res == LaunchResult.Success ? "Launch successful" : "Partial success", 6.4f, TextAlignmentOptions.MidlineLeft, true, rc).raycastTarget = false;
        for (int i = 0; i < 3; i++) UIKit.IconAt(panel, Resources.Load<Sprite>("UIIcons/icon_star"), W - 32 + i * 9f, yy + 1.5f, 8, 8, i < c.stars ? LaunchResultUI.Gold : new Color(1f, 1f, 1f, 0.16f));
        yy += 12f;
        for (int i = 0; i < Mathf.Min(c.lines.Count, 6); i++)
        {
            var l = c.lines[i];
            LaunchResultUI.Chip(panel, 6, yy + 1f, l.sev, 7f);
            var t = UIKit.TextAt(panel, "L", 16, yy, W - 20, 9, l.text, 5.4f, TextAlignmentOptions.MidlineLeft, false, l.sev == 0 ? UIKit.T.ink : l.sev == 1 ? new Color(1f, 0.72f, 0.3f, 1f) : UIKit.T.red);
            t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
            yy += 10f;
        }
        if (!string.IsNullOrEmpty(c.cause))
        {
            var ct = UIKit.TextAt(panel, "Cause", 6, yy + 1f, W - 12, 12, c.cause, 5f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
            ct.textWrappingMode = TextWrappingModes.Normal; ct.lineSpacing = -6f; ct.raycastTarget = false; yy += 14f;
        }
        UIKit.IconAt(panel, Resources.Load<Sprite>("UIIcons/icon_money"), 6, yy + 1f, 8, 8, UIKit.T.priceText).raycastTarget = false;
        UIKit.TextAt(panel, "Reward", 17, yy, W - 22, 10, c.reward > 0 ? "Waiting: $" + UIKit.Money(c.reward) : "No contract reward", 5.8f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.priceText).raycastTarget = false;
    }

    void Update()
    {
        var fm = FlightManager.Instance;
        bool show = fm != null && fm.Current != null;
        if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        if (!show) { shown = null; return; }
        if (shown != fm.Current) { expanded = false; Rebuild(fm.Current); }

        // under the Objectives button; under its panel while that is open; beside it when expanded and the panel is open (no room below)
        bool objOpen = objectives != null && objectives.IsOpen;
        float tx = 8f, ty = -58f;
        if (objOpen) { if (expanded) tx = 8f + ObjectiveMenuUI.PW + 6f; else ty = -(58f + ObjectiveMenuUI.PanelH + 4f); }
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        x = Mathf.Lerp(x, tx, k); y = Mathf.Lerp(y, ty, k);
        panel.anchoredPosition = new Vector2(x, y);

        float p = fm.Progress;
        if (fill != null) fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
        if (pct != null) pct.text = Mathf.RoundToInt(p * 100f) + "%";
        var c = fm.Current;
        if (info != null) info.text = (string.IsNullOrEmpty(c.missionName) ? c.rocketName : c.missionName) + "  ·  " + fm.DaysLeft + " d left";
        if (fill != null) fill.color = c.Result == LaunchResult.Partial ? new Color(1f, 0.72f, 0.3f, 1f) : UIKit.T.teal;
    }
}
