using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One skill of the infographic: name, icon, score 0..100.</summary>
public class LearnSkill { public string name, icon, lesson, real, tip; public int score; }

/// <summary>
/// The pretend "AI": turns the mission diary into skill scores, learning cards and real-life notes. There is no model behind it,
/// only fixed rocketry facts picked by how the mission went, so the output is always correct, instant and offline.
/// </summary>
public static class LearningAI
{
    static float Norm(float v) => v <= 0f ? 0f : v <= 1f ? v : v / 100f;

    public static List<LearnSkill> Analyse(LearningReport r)
    {
        var list = new List<LearnSkill>();

        // preparation
        int prep = r.trainingTotal > 0 ? Mathf.RoundToInt(100f * r.trainingDone / r.trainingTotal) : 65;
        list.Add(new LearnSkill
        {
            name = "Preparation", icon = "icon_scientist", score = prep,
            lesson = prep >= 70 ? "Training pays off before liftoff" : "Skipped training raises the risk",
            real = prep >= 70 ? "Real crews and ground teams rehearse every phase for months, and simulations catch procedure errors that hardware tests cannot."
                              : "Space agencies treat training as a launch requirement, because many flight anomalies involve people and procedures, not only hardware.",
            tip = "Finish every training course before the build starts.",
        });

        // design
        float dvScore = r.dvRequired > 0f ? Mathf.Clamp01(r.dvTotal / r.dvRequired) : 0.8f;
        float twrScore = r.twr >= 1.2f && r.twr <= 2.6f ? 1f : r.twr >= 1f ? 0.6f : 0.2f;
        float stab = r.stability > 0f ? Mathf.Clamp01(Norm(r.stability)) : 0.7f;
        int design = Mathf.RoundToInt(100f * (dvScore * 0.6f + twrScore * 0.25f + stab * 0.15f));
        bool dvShort = r.dvRequired > 0f && r.dvTotal < r.dvRequired;
        list.Add(new LearnSkill
        {
            name = "Rocket design", icon = "icon_rocket", score = design,
            lesson = dvShort ? "A delta-v shortfall ends in orbit-less flights" : "The rocket equation sets your limits",
            real = dvShort ? "A mission is redesigned when its delta-v budget does not close, so engineers keep a margin on top of the calculated need."
                           : "Every stage is sized with the Tsiolkovsky rocket equation. Reaching low Earth orbit takes roughly 9.4 km/s once gravity and drag losses are counted.",
            tip = dvShort ? "Add propellant or a stage until delta-v reaches the contract's need." : "Keep liftoff thrust-to-weight between 1.2 and 1.5 as real launchers do.",
        });
        if (r.stages > 1)
            list.Add(new LearnSkill
            {
                name = "Staging", icon = "icon_upgrade", score = Mathf.Clamp(design + 5, 0, 100),
                lesson = "Staging drops dead weight",
                real = "Multi-stage rockets such as Saturn V and Falcon 9 shed empty tanks and engines, so the rest of the vehicle accelerates more efficiently.",
                tip = "Use more than one stage for targets beyond low Earth orbit.",
            });

        // testing
        float rel = r.reliability > 0f ? Mathf.Clamp01(Norm(r.reliability)) : 0.5f;
        int test = r.testsTotal > 0 ? Mathf.RoundToInt(100f * (0.7f * r.testsDone / r.testsTotal + 0.3f * rel)) : Mathf.RoundToInt(100f * rel);
        list.Add(new LearnSkill
        {
            name = "Testing", icon = "icon_flask", score = test,
            lesson = test >= 70 ? "Test as you fly" : "Untested hardware is a gamble",
            real = test >= 70 ? "Engines are static-fired and structures load-tested on the ground first. A failure on the test stand costs far less than one on the pad."
                              : "Many historic launch failures traced back to parts that were never tested under flight conditions.",
            tip = "Run the engine, comms and structure tests before deployment.",
        });

        // budget and risk
        float cr = r.budgetM > 0f ? r.costM / r.budgetM : 0.8f;
        int money = cr <= 0.7f ? 100 : cr <= 1f ? 75 : 35;
        if (r.riskyDecision) money -= 20;
        if (r.extraBudget) money += 5;
        money = Mathf.Clamp(money, 0, 100);
        list.Add(new LearnSkill
        {
            name = "Budget and risk", icon = "icon_money", score = money,
            lesson = r.riskyDecision ? "Rushed schedules add risk" : "Margins protect the schedule",
            real = r.riskyDecision ? "Schedule pressure is a recognised risk factor in accident investigations, which is why programmes defend their reviews and reserves."
                                   : "Programmes hold cost and schedule reserves, so one surprise does not force a rushed or unsafe launch decision.",
            tip = "Keep the rocket cost well under the contract budget and avoid risky shortcuts.",
        });

        // flight operations
        int ops = r.Result == LaunchResult.Success ? 70 + r.stars * 10 : r.Result == LaunchResult.Partial ? 50 : 20;
        list.Add(new LearnSkill
        {
            name = "Flight operations", icon = "icon_launch", score = ops,
            lesson = r.Result == LaunchResult.Failure ? "Failures teach the most" : "Go / no-go discipline",
            real = r.Result == LaunchResult.Failure ? "After an anomaly an investigation board traces the root cause before the next launch is allowed. Many reliable rockets earned their record this way."
                                                    : "Launch teams poll every console for a go before liftoff, and flight data is reviewed afterwards to improve the next vehicle.",
            tip = "Read the launch report: every warning there points at a fix.",
        });
        return list;
    }

    public static int Mastery(List<LearnSkill> skills)
    {
        if (skills.Count == 0) return 0;
        int sum = 0; foreach (var s in skills) sum += s.score;
        return Mathf.RoundToInt((float)sum / skills.Count);
    }

    public static string Grade(int m) => m >= 90 ? "A+" : m >= 80 ? "A" : m >= 70 ? "B" : m >= 55 ? "C" : "D";
    public static Color ScoreColor(int s) => s >= 75 ? UIKit.T.green : s >= 50 ? LaunchResultUI.Amber : UIKit.T.red;
}

/// <summary>The window after a launch: the mission ledger (everything the player did, with icons) and the fake-AI learning outcome.</summary>
public static class LearningOutcomeUI
{
    static Sprite Icon(string n) => Resources.Load<Sprite>("UIIcons/" + n);
    static Color SevColor(int sev) => sev == 0 ? UIKit.T.green : sev == 1 ? LaunchResultUI.Amber : UIKit.T.red;

    // ------------------------------------------------------------------ small prompt after a launch
    public static void Prompt()
    {
        if (LearningLog.Last == null) return;
        const float w = 214f, h = 104f;
        var card = LaunchResultUI.Modal(w, h);
        if (card == null) return;
        var col = UIKit.T.purple;
        UIKit.Tint(UIKit.At(card, "Accent", 0, 0, w, 3), col).raycastTarget = false;
        var chip = UIKit.At(card, "Chip", 12, 14, 26, 26); UIKit.Tint(chip, UIKit.Wash(col, 0.3f)).raycastTarget = false;
        UIKit.IconAt(chip, Icon("icon_learn"), 4, 4, 18, 18, col);
        UIKit.TextAt(card, "Title", 46, 12, w - 58, 14, "Learning outcome ready", 9f, TextAlignmentOptions.MidlineLeft, true);
        var sub = UIKit.TextAt(card, "Sub", 46, 27, w - 58, 26, "See what you did in this mission and what it teaches about real rocket launches.", 6f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
        sub.textWrappingMode = TextWrappingModes.Normal; sub.lineSpacing = -4f;
        UIKit.Btn(card, "Later", 12, h - 28, 70, 18, UIKit.T.chip, LaunchResultUI.Close, 7f);
        UIKit.SolidBtn(card, "Open learning outcome", w - 12 - 112, h - 28, 112, 18, col, OpenLast, 7f);
    }

    public static void OpenLast()
    {
        var r = LearningLog.Last;
        if (r == null) { LaunchResultUI.Close(); return; }
        ShowLedger(r);
    }

    // ------------------------------------------------------------------ page 1: the ledger
    static void ShowLedger(LearningReport r)
    {
        const float w = 312f, h = 262f;
        var card = LaunchResultUI.Modal(w, h);
        if (card == null) return;
        var col = UIKit.T.purple;
        UIKit.Tint(UIKit.At(card, "Accent", 0, 0, w, 3), col).raycastTarget = false;
        UIKit.TitleBar(card, "Mission ledger", col, w, LaunchResultUI.Close);

        // header strip: mission, rocket, result
        var head = UIKit.At(card, "Head", 8, 28, w - 16, 24); UIKit.Tint(head, new Color(1f, 1f, 1f, 0.09f)).raycastTarget = false;
        var hchip = UIKit.At(head, "Chip", 5, 4, 16, 16); UIKit.Tint(hchip, UIKit.Wash(col, 0.3f)).raycastTarget = false;
        UIKit.IconAt(hchip, Icon("icon_ledger"), 2, 2, 12, 12, col);
        var nm = UIKit.TextAt(head, "Name", 27, 2, w - 16 - 100, 11, r.missionName, 7f, TextAlignmentOptions.MidlineLeft, true); nm.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.TextAt(head, "Sub", 27, 13, w - 16 - 100, 9, r.rocketName + "  -  " + r.launchDate, 5.4f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim).overflowMode = TextOverflowModes.Ellipsis;
        var resCol = r.Result == LaunchResult.Success ? UIKit.T.green : r.Result == LaunchResult.Partial ? LaunchResultUI.Amber : UIKit.T.red;
        string resWord = r.Result == LaunchResult.Success ? "Success" : r.Result == LaunchResult.Partial ? "Partial" : "Failed";
        UIKit.TextAt(head, "Res", w - 16 - 74, 2, 68, 20, resWord, 7.2f, TextAlignmentOptions.MidlineRight, true, resCol);

        // the list, grouped by section
        var content = UIKit.ScrollList(card, "Ledger", 8, 56, w - 16, h - 56 - 52, new Color(0f, 0f, 0f, 0.14f), 1f);
        string section = null;
        foreach (var it in r.items)
        {
            if (it.section != section)
            {
                section = it.section;
                var sh = UIKit.At(content, "Section", 0, 0, 100, 13); sh.gameObject.AddComponent<LayoutElement>().preferredHeight = 13;
                UIKit.TextAt(sh, "T", 5, 3, 200, 9, section.ToUpperInvariant(), 5.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.dim).raycastTarget = false;
            }
            bool tall = it.text.Length > 62;
            float rh = tall ? 22f : 14f;
            var row = UIKit.At(content, "Row", 0, 0, 100, rh); row.gameObject.AddComponent<LayoutElement>().preferredHeight = rh;
            var c = SevColor(it.sev);
            var chip = UIKit.At(row, "Chip", 5, (rh - 11f) * 0.5f, 11, 11); UIKit.Tint(chip, UIKit.Wash(c, 0.28f)).raycastTarget = false;
            UIKit.IconAt(chip, Icon(it.icon), 1.5f, 1.5f, 8, 8, c);
            var t = UIKit.TextAt(row, "T", 21, 0, w - 16 - 30, rh, it.text, 6f, TextAlignmentOptions.MidlineLeft, false, it.sev == 0 ? UIKit.T.ink : c);
            t.raycastTarget = false;
            if (tall) { t.textWrappingMode = TextWrappingModes.Normal; t.lineSpacing = -4f; } else t.overflowMode = TextOverflowModes.Ellipsis;
        }

        // the AI button
        var ai = UIKit.At(card, "AI", 8, h - 46, w - 16, 22);
        UIKit.MakeButton(ai, "", col, () => ShowThinking(r), 7f);
        UIKit.IconAt(ai, Icon("icon_ai"), 10, 4, 14, 14, Color.white);
        UIKit.TextAt(ai, "L", 28, 0, w - 16 - 36, 22, "Summarize & Get Learning Outcome with AI", 7.4f, TextAlignmentOptions.MidlineLeft, true, Color.white).raycastTarget = false;
        UIKit.TextAt(card, "Note", 8, h - 21, w - 16, 14, "Prototype AI: the analysis is generated from your ledger above.", 5.2f, TextAlignmentOptions.Center, false, UIKit.T.dim).raycastTarget = false;
    }

    // ------------------------------------------------------------------ page 2: the AI "thinking"
    static void ShowThinking(LearningReport r)
    {
        const float w = 214f, h = 118f;
        var card = LaunchResultUI.Modal(w, h);
        if (card == null) return;
        var col = UIKit.T.purple;
        UIKit.Tint(UIKit.At(card, "Accent", 0, 0, w, 3), col).raycastTarget = false;
        var halo = UIKit.At(card, "Halo", w * 0.5f - 22f, 14, 44, 44); UIKit.Tint(halo, UIKit.Wash(col, 0.22f)).raycastTarget = false;
        var big = UIKit.IconAt(halo, Icon("icon_ai"), 6, 6, 32, 32, Color.Lerp(col, Color.white, 0.55f));
        var msg = UIKit.TextAt(card, "Msg", 10, 66, w - 20, 11, "Reading your mission ledger...", 6.6f, TextAlignmentOptions.Center, true);
        var track = UIKit.At(card, "Track", 20, 84, w - 40, 5); UIKit.Tint(track, UIKit.T.track).raycastTarget = false;
        var fill = UIKit.At(track, "Fill", 0, 0, 10, 5);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
        UIKit.Tint(fill, col).raycastTarget = false;
        UIKit.TextAt(card, "Hint", 10, 95, w - 20, 10, "Generating charts and real-life notes", 5.4f, TextAlignmentOptions.Center, false, UIKit.T.dim).raycastTarget = false;
        var th = card.gameObject.AddComponent<LearningThinking>();
        th.icon = (RectTransform)big.transform; th.fill = fill; th.message = msg; th.onDone = () => ShowOutcome(r);
    }

    // ------------------------------------------------------------------ page 3: the generated infographic
    static void ShowOutcome(LearningReport r)
    {
        var skills = LearningAI.Analyse(r);
        int mastery = LearningAI.Mastery(skills);
        const float w = 438f, h = 276f;
        var card = LaunchResultUI.Modal(w, h);
        if (card == null) return;
        var col = UIKit.T.purple;
        UIKit.Tint(UIKit.At(card, "Accent", 0, 0, w, 3), col).raycastTarget = false;
        UIKit.TitleBar(card, "Learning outcome", col, w, LaunchResultUI.Close);
        var pill = UIKit.At(card, "Pill", w - 118, 9, 84, 13); UIKit.Tint(pill, UIKit.Wash(col, 0.25f)).raycastTarget = false;
        UIKit.IconAt(pill, Icon("icon_ai"), 4, 2, 9, 9, Color.Lerp(col, Color.white, 0.5f));
        UIKit.TextAt(pill, "T", 15, 0, 68, 13, "Generated by AI", 5.4f, TextAlignmentOptions.MidlineLeft, true, Color.Lerp(col, Color.white, 0.6f)).raycastTarget = false;

        // left: mastery grade and skill bars
        float lx = 10f, lw = 152f, top = 30f;
        var gradeBox = UIKit.At(card, "Grade", lx, top, lw, 40); UIKit.Tint(gradeBox, new Color(1f, 1f, 1f, 0.09f)).raycastTarget = false;
        var gc = LearningAI.ScoreColor(mastery);
        var gsq = UIKit.At(gradeBox, "Sq", 5, 5, 30, 30); UIKit.Tint(gsq, UIKit.Wash(gc, 0.3f)).raycastTarget = false;
        UIKit.TextAt(gsq, "G", 0, 0, 30, 30, LearningAI.Grade(mastery), 13f, TextAlignmentOptions.Center, true, gc).raycastTarget = false;
        UIKit.TextAt(gradeBox, "L", 41, 5, lw - 46, 11, "Mission mastery", 6.2f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim).raycastTarget = false;
        UIKit.TextAt(gradeBox, "V", 41, 16, lw - 46, 18, mastery + " / 100", 12f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.ink).raycastTarget = false;

        float y = top + 46f;
        UIKit.TextAt(card, "BarsT", lx + 2, y, lw, 10, "SKILLS YOU PRACTISED", 5.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.dim).raycastTarget = false;
        y += 11f;
        foreach (var s in skills)
        {
            var c = LearningAI.ScoreColor(s.score);
            UIKit.IconAt(card, Icon(s.icon), lx + 2, y + 1, 9, 9, c);
            UIKit.TextAt(card, "N", lx + 15, y, lw - 40, 11, s.name, 6.2f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.ink).raycastTarget = false;
            UIKit.TextAt(card, "S", lx + lw - 28, y, 28, 11, s.score + "%", 6.2f, TextAlignmentOptions.MidlineRight, true, c).raycastTarget = false;
            var tr = UIKit.At(card, "Track", lx + 2, y + 12, lw - 2, 4); UIKit.Tint(tr, UIKit.T.track).raycastTarget = false;
            var f = UIKit.At(tr, "Fill", 0, 0, 10, 4);
            f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(0f, 1f); f.offsetMin = f.offsetMax = Vector2.zero;
            UIKit.Tint(f, c).raycastTarget = false;
            f.gameObject.AddComponent<LearningBarGrow>().target = s.score / 100f;
            y += 19.5f;
        }

        // next-mission tip: the weakest skill
        LearnSkill weak = skills[0];
        foreach (var s in skills) if (s.score < weak.score) weak = s;
        float tipY = y + 1f, tipH = h - 38f - tipY;
        var tip = UIKit.At(card, "Tip", lx, tipY, lw, tipH); UIKit.Tint(tip, UIKit.Wash(UIKit.T.orange, 0.14f)).raycastTarget = false;
        UIKit.IconAt(tip, Icon("icon_bulb"), 5, 5, 11, 11, UIKit.T.orange);
        UIKit.TextAt(tip, "H", 20, 3, lw - 24, 11, "Next mission", 6.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.orange).raycastTarget = false;
        var tt = UIKit.TextAt(tip, "T", 20, 14, lw - 25, tipH - 16, weak.tip, 5.7f, TextAlignmentOptions.TopLeft, false, UIKit.T.ink);
        tt.textWrappingMode = TextWrappingModes.Normal; tt.lineSpacing = -4f; tt.raycastTarget = false;

        // right: learning cards with the real-life application
        float rx = lx + lw + 10f, rw = w - rx - 10f;
        UIKit.IconAt(card, Icon("icon_globe"), rx + 1, top + 1, 9, 9, UIKit.T.dim);
        UIKit.TextAt(card, "RT", rx + 14, top, rw, 11, "WHAT YOU LEARNED  >  HOW REAL ROCKET MISSIONS USE IT", 5.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.dim).raycastTarget = false;
        var list = UIKit.ScrollList(card, "Cards", rx, top + 13, rw, h - top - 13 - 38f, new Color(0f, 0f, 0f, 0.14f), 3f);
        foreach (var s in skills)
        {
            var c = LearningAI.ScoreColor(s.score);
            var row = UIKit.At(list, "Card", 0, 0, 100, 45); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 45;
            UIKit.Tint(row, new Color(1f, 1f, 1f, 0.07f)).raycastTarget = false;
            var chip = UIKit.At(row, "Chip", 5, 5, 16, 16); UIKit.Tint(chip, UIKit.Wash(c, 0.3f)).raycastTarget = false;
            UIKit.IconAt(chip, Icon(s.icon), 3, 3, 10, 10, c);
            UIKit.TextAt(row, "L", 27, 3, rw - 34, 12, s.lesson, 6.8f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.ink).overflowMode = TextOverflowModes.Ellipsis;
            UIKit.IconAt(row, Icon("icon_globe"), 27, 17, 7, 7, UIKit.T.blue);
            var rt = UIKit.TextAt(row, "R", 37, 15, rw - 44, 28, s.real, 5.5f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
            rt.textWrappingMode = TextWrappingModes.Normal; rt.lineSpacing = -4f; rt.raycastTarget = false;
        }

        UIKit.TextAt(card, "Foot", 10, h - 28, 250, 18, "Prototype: this analysis is generated from your mission ledger, not by a real AI.", 5.2f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim).raycastTarget = false;
        UIKit.Btn(card, "Back to ledger", w - 12 - 74 - 4 - 56, h - 26, 74, 18, UIKit.T.chip, () => ShowLedger(r), 6.6f);
        UIKit.SolidBtn(card, "Done", w - 12 - 56, h - 26, 56, 18, col, LaunchResultUI.Close, 7f);
    }
}

/// <summary>Drives the "AI is thinking" page: pulsing sparkle, progress bar and changing messages, then moves on.</summary>
public class LearningThinking : MonoBehaviour
{
    public RectTransform icon, fill;
    public TMP_Text message;
    public System.Action onDone;
    const float Duration = 2.6f;
    static readonly string[] Steps =
    {
        "Reading your mission ledger...",
        "Comparing with real launch programmes...",
        "Scoring the skills you practised...",
        "Writing your learning outcome...",
    };
    float t;
    bool done;

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01(t / Duration);
        if (icon != null)
        {
            float s = 1f + 0.12f * Mathf.Sin(t * 7f);
            icon.localScale = new Vector3(s, s, 1f);
            icon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.2f) * 9f);
        }
        if (fill != null) fill.anchorMax = new Vector2(p * p * (3f - 2f * p), 1f);
        if (message != null) message.text = Steps[Mathf.Min(Steps.Length - 1, (int)(p * Steps.Length))];
        if (p >= 1f && !done) { done = true; onDone?.Invoke(); }
    }
}

/// <summary>Grows a skill bar from empty to its score.</summary>
public class LearningBarGrow : MonoBehaviour
{
    public float target;
    float t;
    RectTransform rt;
    void Update()
    {
        if (rt == null) rt = (RectTransform)transform;
        t += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01((t - 0.15f) / 0.8f);
        p = 1f - (1f - p) * (1f - p);
        rt.anchorMax = new Vector2(target * p, 1f);
        if (p >= 1f) enabled = false;
    }
}
