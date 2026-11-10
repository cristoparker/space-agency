using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Launch-day signs in the top bar. A small pill next to the date counts down to a scheduled launch ("Launch in 3 days", "Launch tomorrow").
/// On launch day the mission progress bar is replaced by a glowing yellow Launch button; clicking it starts the launch cutscene.
/// The game pauses when launch day begins so it cannot be skipped by accident.
/// </summary>
public class LaunchDayUI : MonoBehaviour
{
    RectTransform missionBar, hintRt, buttonRt;
    TMP_Text hintText;
    Image hintBg, glow, buttonBg;
    float timer;
    bool wasReady;
    int lastDay = -1;

    public static LaunchDayUI Create(RectTransform topBar, RectTransform leftCluster, RectTransform missionBar)
    {
        var go = new GameObject("Launch Day", typeof(RectTransform));
        go.transform.SetParent(topBar, false);
        var ui = go.AddComponent<LaunchDayUI>();
        ui.Build(topBar, leftCluster, missionBar);
        return ui;
    }

    void Build(RectTransform topBar, RectTransform left, RectTransform mission)
    {
        missionBar = mission;
        var T = UIKit.T;

        // the countdown pill, after the date in the left cluster
        hintRt = UIKit.At(left, "Launch Hint", 0, 0, 56, 13);
        var le = hintRt.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 56; le.minWidth = 56; le.preferredHeight = 13; le.minHeight = 13;
        hintBg = UIKit.Tint(hintRt, UIKit.Wash(T.teal, 0.5f));
        UIKit.IconAt(hintRt, Resources.Load<Sprite>("UIIcons/icon_launch"), 3, 2.5f, 8, 8, T.ink);
        hintText = UIKit.TextAt(hintRt, "T", 12, 0, 44, 13, "", 5.8f, TextAlignmentOptions.MidlineLeft, true);
        hintRt.gameObject.AddComponent<TooltipTrigger>().text = "Launch day\nYour rocket is on the pad.\nThe launch button appears on that day.";
        hintRt.gameObject.SetActive(false);

        // the glowing Launch button, where the mission bar is
        buttonRt = new GameObject("Launch Button", typeof(RectTransform)).GetComponent<RectTransform>();
        buttonRt.SetParent(topBar, false);
        buttonRt.anchorMin = buttonRt.anchorMax = new Vector2(0.5f, 0.5f); buttonRt.pivot = new Vector2(0.5f, 0.5f);
        buttonRt.anchoredPosition = mission != null ? mission.anchoredPosition : new Vector2(34f, 0f); buttonRt.sizeDelta = new Vector2(80f, 15f);
        var gr = new GameObject("Glow", typeof(RectTransform)).GetComponent<RectTransform>();
        gr.SetParent(buttonRt, false);
        gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = new Vector2(-5f, -4f); gr.offsetMax = new Vector2(5f, 4f);
        glow = gr.gameObject.AddComponent<Image>();
        glow.sprite = T.round; glow.type = Image.Type.Sliced; glow.pixelsPerUnitMultiplier = 1f; glow.color = new Color(1f, 0.85f, 0.2f, 0.3f); glow.raycastTarget = false;
        UIRound.Attach(glow);
        var btn = UIKit.At(buttonRt, "Button", 0, 0, 80, 15);
        var b = UIKit.MakeButton(btn, "", new Color(1f, 0.82f, 0.22f, 1f), () => { var d = DeploymentManager.Instance; if (d == null || d.Current == null || d.library == null) return; LaunchResultUI.PreLaunch(d.Current, d.library, () => LaunchSequence.Begin()); });
        buttonBg = (Image)b.targetGraphic;
        UIKit.IconAt(btn, Resources.Load<Sprite>("UIIcons/icon_launch"), 6, 2.5f, 10, 10, new Color(0.25f, 0.16f, 0f, 1f));
        UIKit.TextAt(btn, "Label", 19, 0, 58, 15, "LAUNCH", 7.4f, TextAlignmentOptions.MidlineLeft, true, new Color(0.25f, 0.16f, 0f, 1f)).raycastTarget = false;
        buttonRt.gameObject.AddComponent<TooltipTrigger>().text = "Launch day\nPress to launch your rocket.";
        buttonRt.gameObject.SetActive(false);

        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed += Refresh;
        if (GameClock.Instance != null) GameClock.Instance.DayChanged += OnDay;
        Refresh();
    }

    void OnDestroy()
    {
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed -= Refresh;
        if (GameClock.Instance != null) GameClock.Instance.DayChanged -= OnDay;
    }

    void OnDay(System.DateTime _) => Refresh();

    void Update()
    {
        // the glow breathes
        if (buttonRt != null && buttonRt.gameObject.activeSelf)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            glow.color = new Color(1f, 0.85f, 0.2f, Mathf.Lerp(0.18f, 0.6f, p));
            glow.rectTransform.offsetMin = new Vector2(-3f - 3f * p, -2f - 3f * p); glow.rectTransform.offsetMax = new Vector2(3f + 3f * p, 2f + 3f * p);
        }
        timer += Time.unscaledDeltaTime;
        if (timer < 0.5f) return;
        timer = 0f;
        Refresh();
    }

    void Refresh()
    {
        var dep = DeploymentManager.Instance;
        bool scheduled = dep != null && dep.Current != null && dep.LaunchDate.HasValue && dep.PadRocket != null;
        bool dateReached = scheduled && dep.LaunchReady;
        bool ready = dateReached && MissionObjectives.LaunchAllowed(dep) && !LaunchSequence.Running;       // launch day AND every other objective done

        if (hintRt != null)
        {
            hintRt.gameObject.SetActive(scheduled && !ready && !LaunchSequence.Running);
            if (scheduled && !ready)
            {
                int d = dep.DaysToLaunch;
                hintText.text = dateReached ? "Finish goals" : d <= 1 ? "Launch 1 d" : "Launch " + d + " d";
                bool soon = d <= 2 || dateReached;
                hintBg.color = soon ? UIKit.Wash(new Color(1f, 0.7f, 0.25f, 1f), 0.6f) : UIKit.Wash(UIKit.T.teal, 0.5f);
            }
        }
        if (buttonRt != null) buttonRt.gameObject.SetActive(ready);
        if (missionBar != null) missionBar.gameObject.SetActive(!ready);          // the mission bar is replaced by the Launch button

        // launch day begins: pause once so the player cannot sail past it
        if (ready && !wasReady)
        {
            var clock = GameClock.Instance;
            if (clock != null && clock.Speed > 0f) clock.SetSpeedIndex(0);
            Hud.Toast("Launch day! Press the glowing LAUNCH button.");
        }
        wasReady = ready;
    }
}
