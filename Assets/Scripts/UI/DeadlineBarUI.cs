using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The contract deadline, next to the date in the top bar, joined with the mission progress bar into one capsule:
/// the text is how long is left, the bar under it is how far the objectives are, and a small tick on the bar shows how much of the
/// time has already gone (a tick ahead of the fill means you are behind). The colour warns: blue normally, orange in the last week,
/// red in the last two days. Hovering lists every contract's deadline and launch date.
/// </summary>
public class DeadlineBarUI : MonoBehaviour
{
    public const float W = 112f, H = 15f;
    static readonly Color Calm = new Color(0.04f, 0.52f, 1f, 1f), Warn = new Color(1f, 0.62f, 0.04f, 1f), Alarm = new Color(1f, 0.27f, 0.23f, 1f);

    TMP_Text text;
    Image fill, bg, tick, icon;
    RectTransform fillRt, tickRt, trackRt;
    TooltipTrigger tip;
    float timer = 99f;

    public static RectTransform Create(RectTransform parent)
    {
        var rt = UIKit.At(parent, "Deadline", 0, 0, W, H);
        var ui = rt.gameObject.AddComponent<DeadlineBarUI>();
        ui.Build(rt);
        return rt;
    }

    void Build(RectTransform rt)
    {
        var T = UIKit.T;
        bg = UIKit.Tint(rt, T.chip);
        UIHover.Attach(rt.gameObject, 1.04f, 0.14f);
        icon = UIKit.IconAt(rt, Resources.Load<Sprite>("UIIcons/icon_time"), 5, 1.2f, 7.5f, 7.5f, T.ink);
        text = UIKit.TextAt(rt, "Text", 15, 0, W - 18, 9.6f, "No contract", 6.3f, TextAlignmentOptions.MidlineLeft, true, T.dim);
        text.raycastTarget = false;
        // the progress bar sits inside the same capsule, under the text
        var track = UIKit.At(rt, "Track", 6, 10.2f, W - 12, 2.6f);
        trackRt = track;
        UIKit.Tint(track, new Color(0f, 0f, 0f, 0.4f)).raycastTarget = false;
        fillRt = UIKit.At(track, "Fill", 0, 0, 10, 2.6f);
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0f, 1f); fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        fill = UIKit.Tint(fillRt, Calm); fill.raycastTarget = false;
        tickRt = UIKit.At(track, "Time", 0, -1.2f, 1.6f, 5f);
        tickRt.anchorMin = new Vector2(0f, 0.5f); tickRt.anchorMax = new Vector2(0f, 0.5f); tickRt.pivot = new Vector2(0.5f, 0.5f); tickRt.anchoredPosition = Vector2.zero;
        tick = UIKit.Tint(tickRt, new Color(1f, 1f, 1f, 0.95f)); tick.raycastTarget = false;
        tip = rt.gameObject.AddComponent<TooltipTrigger>();
        tip.text = "Contract deadline\nNo active contract.";
    }

    static string Left(double days)
    {
        if (days < 1) return Mathf.Max(0, Mathf.CeilToInt((float)(days * 24))) + " h";
        if (days >= 90) return Mathf.RoundToInt((float)(days / 30)) + " mo";
        return Mathf.CeilToInt((float)days) + " d";
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < 0.3f) return;
        timer = 0f;
        Refresh();
    }

    void Refresh()
    {
        var mm = MissionManager.Instance; var clock = GameClock.Instance;
        if (mm == null || clock == null) return;
        var running = mm.Running.ToList();
        if (running.Count == 0)
        {
            text.text = "No contract"; text.color = UIKit.T.dim; bg.color = UIKit.T.chip;
            fillRt.anchorMax = new Vector2(0f, 1f); tickRt.gameObject.SetActive(false);
            tip.text = "Contract deadline\nNo active contract.\nAccept one in Contracts.";
            return;
        }
        var now = clock.Now;
        // the most urgent contract is the one shown
        var show = running.OrderBy(m => (m.StartDate.AddDays(m.Definition.DurationDays) - now).TotalDays).First();
        double days = (show.StartDate.AddDays(show.Definition.DurationDays) - now).TotalDays;
        var objs = MissionObjectives.For(show);
        float done = objs.Count == 0 ? 0f : (float)objs.Count(o => o.done) / objs.Count;

        Color c = days <= 2 ? Alarm : days <= 7 ? Warn : Calm;
        float pulse = days <= 2 ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
        text.text = "Deadline  " + Left(days) + (running.Count > 1 ? "  +" + (running.Count - 1) : "");
        text.color = UIKit.T.ink;
        bg.color = days <= 7 ? UIKit.Wash(c, 0.28f * pulse + 0.1f) : UIKit.T.chip;
        fill.color = c;
        fillRt.anchorMax = new Vector2(Mathf.Clamp01(done), 1f);
        tickRt.gameObject.SetActive(true);
        tickRt.anchorMin = tickRt.anchorMax = new Vector2(Mathf.Clamp01(show.Progress), 0.5f);          // share of the time already used
        tip.text = Tooltip(running, now);
    }

    static string Tooltip(System.Collections.Generic.List<MissionInstance> running, System.DateTime now)
    {
        var inv = CultureInfo.InvariantCulture;
        var dep = DeploymentManager.Instance;
        var sb = new StringBuilder();
        sb.Append("Contract deadline");
        foreach (var m in running.OrderBy(x => x.StartDate.AddDays(x.Definition.DurationDays)).Take(3))
        {
            var d = m.StartDate.AddDays(m.Definition.DurationDays);
            double days = (d - now).TotalDays;
            var objs = MissionObjectives.For(m);
            sb.Append("\n\n<b>" + m.Definition.displayName + "</b>");
            sb.Append("\nDeadline: " + d.ToString("dd MMM yyyy", inv) + "  (" + Left(days) + " left)");
            sb.Append("\nObjectives: " + objs.Count(o => o.done) + " / " + objs.Count);
            if (dep != null && dep.IsMissionDeployed(m.Definition.id) && dep.LaunchDate.HasValue)
                sb.Append("\nLaunch: " + dep.LaunchDate.Value.ToString("dd MMM yyyy", inv));
            else
            {
                sb.Append("\nNo launch date assigned.");
                sb.Append(MissionObjectives.HasRocketDesign() ? "\nAssign one in the Rocket Designer (Deploy)." : "\nThe rocket is not complete yet.");
            }
            if (days <= 7) sb.Append("\n<color=#FFB35C>Less than a week left!</color>");
        }
        return sb.ToString();
    }
}
