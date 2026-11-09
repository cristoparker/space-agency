using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tiny global message line (bottom centre). Anything can call Hud.Toast("...").</summary>
public static class Hud
{
    public static System.Action<string> OnToast;
    public static void Toast(string msg) => OnToast?.Invoke(msg);
}

/// <summary>
/// Builds the game HUD from the reference layouts on top of the existing canvas:
/// bottom navigation (Contract / Training / Building / Testing with their sub menus), the Contracts and Facility windows,
/// the agency badge, Resources and Reputation pills and a play / pause button in the top bar.
/// Panels open one at a time through <see cref="Panels"/>.
/// </summary>
public class HudController : MonoBehaviour
{
    [Header("Existing scene objects")]
    public RocketBuilderUI builder;
    public Sprite playIcon, pauseIcon;
    [Tooltip("Contract, Training, Building, Testing")] public Sprite[] navIcons;

    static readonly (string label, string[] items)[] Nav =
    {
        ("Contract", new string[0]),
        ("Training", new[] { "Scientist Training", "Pilot Training" }),
        ("Building", new[] { "Facility", "Payload", "Rocket" }),
        ("Testing", new[] { "Engine Test", "Structure Test", "Comms Test" }),
    };

    RectTransform canvasRt, top;
    readonly List<GameObject> subMenus = new List<GameObject>();
    readonly List<Image> navImages = new List<Image>();
    int openGroup = -1;
    GameObject facilityPanel;
    TMP_Text toastText, moneyText, repText, resText, agencyName;
    Image repFill, playIconImg, playBg, fast3Bg, fast6Bg;
    TMP_Text fast3Label, fast6Label;
    float toastTimer;
    RectTransform toastBox;
    int resumeIndex = 1;

    void Awake()
    {
        Hud.OnToast = ShowToast;
        if (BaseSim.Instance == null) new GameObject("Base Sim").AddComponent<BaseSim>();      // buildings, training and testing programmes
    }

    void OnDestroy()
    {
        if (Hud.OnToast == ShowToast) Hud.OnToast = null;
        Agency.Changed -= RefreshStats;
        Panels.Unregister("contracts"); Panels.Unregister("facility"); Panels.Unregister("programs:0"); Panels.Unregister("programs:1"); Panels.Unregister("rocket"); Panels.Unregister("payload");
    }

    void Start()
    {
        if (UITheme.Current == null) { Debug.LogError("HudController: Resources/UITheme is missing. Run Tools / Space Agency / Setup Game UI."); return; }
        canvasRt = (RectTransform)transform;
        top = transform.Find("Top Panel") as RectTransform;
        Canvas.ForceUpdateCanvases();

        var cal = transform.Find("Calendar Panel") as RectTransform;
        if (cal != null)
        {
            if (cal.GetComponent<UIShadow>() == null) UIShadow.Attach(cal);
            cal.anchoredPosition = new Vector2(96f, -30f);                         // drops down from the date in the top bar
        }
        BuildPanels();
        BuildNav();
        BuildToast();
        if (top != null) BuildTopBar();
        Agency.Changed += RefreshStats;
        RefreshStats();

        var left = transform.Find("Left Panel");
        if (left != null) left.gameObject.SetActive(false);                       // replaced by the bottom navigation
        var oldMm = transform.Find("Mission Manager Panel");
        if (oldMm != null) oldMm.gameObject.SetActive(false);                      // replaced by the Contracts window
    }

    // =============================================================== windows
    GameObject NewPanel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        return go;
    }

    void BuildPanels()
    {
        var cgo = NewPanel("Contracts Panel");
        var cp = cgo.AddComponent<ContractsPanelUI>();
        cp.Init();
        Panels.Register("contracts", cgo, () => { cgo.SetActive(true); });

        facilityPanel = NewPanel("Facility Panel");
        facilityPanel.AddComponent<FacilityPanelUI>().Init();
        Panels.Register("facility", facilityPanel, () => { facilityPanel.SetActive(true); });

        var pgo = NewPanel("Programs Panel");
        var pp = pgo.AddComponent<ProgramsPanelUI>();
        pp.Init();
        Panels.Register("programs:0", pgo, () => { pp.SetTab(0); pgo.SetActive(true); });
        Panels.Register("programs:1", pgo, () => { pp.SetTab(1); pgo.SetActive(true); });

        // hover / select buildings in the world
        var sgo = new GameObject("Building Selector");
        var bs = sgo.AddComponent<BuildingSelector>();
        bs.canvasRect = (RectTransform)transform;
        bs.outline = builder != null && builder.library != null ? builder.library.outlineMaterial : null;
        bs.Init();

        if (builder != null)
        {
            Panels.Register("rocket", builder.gameObject, () => { builder.mode = DesignerMode.Rocket; builder.gameObject.SetActive(true); });
            Panels.Register("payload", builder.gameObject, () => { builder.mode = DesignerMode.Payload; builder.gameObject.SetActive(true); });
        }
    }

    // =============================================================== bottom navigation
    void BuildNav()
    {
        const float bw = 22f, gap = 3f, pad = 5f, bh = 22f;
        float w = pad * 2 + Nav.Length * bw + (Nav.Length - 1) * gap, h = bh + pad * 2;
        var bar = new GameObject("Bottom Nav", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(transform, false);
        bar.anchorMin = bar.anchorMax = Vector2.zero; bar.pivot = Vector2.zero;
        bar.anchoredPosition = new Vector2(8f, 8f); bar.sizeDelta = new Vector2(w, h);
        UIKit.Glass(bar);

        for (int g = 0; g < Nav.Length; g++)
        {
            int gi = g;
            float x = pad + g * (bw + gap);
            var r = UIKit.At(bar, Nav[g].label, x, pad, bw, bh);
            var b = UIKit.MakeButton(r, "", UIKit.T.chip, () => OnNav(gi));
            var sprite = navIcons != null && g < navIcons.Length ? navIcons[g] : null;
            if (sprite != null) UIKit.IconAt(r, sprite, 4.5f, 4.5f, bh - 9f, bh - 9f, UIKit.T.ink);
            else UIKit.Text(r, Nav[g].label.Substring(0, 1), 9f, TextAlignmentOptions.Center, true);
            r.gameObject.AddComponent<TooltipTrigger>().text = Nav[g].label;
            navImages.Add((Image)b.targetGraphic);

            var items = Nav[g].items;
            if (items.Length == 0) { subMenus.Add(null); continue; }
            var sub = new GameObject(Nav[g].label + " Menu", typeof(RectTransform)).GetComponent<RectTransform>();
            sub.SetParent(bar, false);
            sub.anchorMin = sub.anchorMax = Vector2.zero; sub.pivot = Vector2.zero;
            sub.anchoredPosition = new Vector2(x, h + 3f);
            float ch = 14f, cg = 2f, cw = 70f;
            sub.sizeDelta = new Vector2(cw, items.Length * (ch + cg));
            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i];
                var chip = UIKit.At(sub, item, 0, i * (ch + cg), cw, ch);
                UIKit.Glass(chip, false);
                UIKit.MakeButton(UIKit.At(chip, "Hit", 0, 0, cw, ch), item, Color.clear, () => OnSubMenu(item), 6.8f, false);
            }
            sub.gameObject.SetActive(false);
            subMenus.Add(sub.gameObject);
        }
    }

    void OnNav(int g)
    {
        if (Nav[g].label == "Contract") { CloseMenus(); Panels.Toggle("contracts"); return; }
        bool wasOpen = openGroup == g;
        CloseMenus();
        if (wasOpen) return;
        openGroup = g;
        subMenus[g].SetActive(true);
        navImages[g].color = UIKit.T.teal;
    }

    void CloseMenus()
    {
        openGroup = -1;
        foreach (var s in subMenus) if (s != null) s.SetActive(false);
        foreach (var i in navImages) i.color = UIKit.T.chip;
    }

    void OnSubMenu(string item)
    {
        CloseMenus();
        switch (item)
        {
            case "Facility": Panels.Open("facility"); break;
            case "Payload": Panels.Open("payload"); break;
            case "Rocket": Panels.Open("rocket"); break;
            case "Scientist Training": case "Pilot Training": Panels.Open("programs:0"); break;
            default: Panels.Open("programs:1"); break;                  // Engine / Structure / Comms test
        }
    }

    // =============================================================== toast
    void BuildToast()
    {
        var r = new GameObject("Toast", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(transform, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, 40f); r.sizeDelta = new Vector2(220f, 16f);
        UIKit.Glass(r);
        toastText = UIKit.Text(r, "", 7.5f, TextAlignmentOptions.Center);
        toastBox = r;
        r.gameObject.SetActive(false);
    }

    void ShowToast(string msg)
    {
        if (toastBox == null) return;
        toastText.text = msg; toastTimer = 3.2f;
        toastBox.gameObject.SetActive(true);
        toastBox.SetAsLastSibling();
    }

    void Update()
    {
        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f && toastBox != null) toastBox.gameObject.SetActive(false);
        }
        RefreshClockControls();
    }

    // =============================================================== game speed: pause / 3x / 6x
    // One real minute is one game hour at normal speed (GameClock.gameMinutesPerSecond = 1); 3x and 6x multiply that.
    int IndexOfSpeed(float v)
    {
        var c = GameClock.Instance; if (c == null) return -1;
        for (int i = 0; i < c.speedLevels.Length; i++) if (Mathf.Approximately(c.speedLevels[i], v)) return i;
        return -1;
    }

    void TogglePause()
    {
        var c = GameClock.Instance; if (c == null) return;
        if (c.Speed <= 0f) c.SetSpeedIndex(Mathf.Max(1, resumeIndex));
        else { resumeIndex = c.SpeedIndex; c.SetSpeedIndex(0); }
    }

    void SetFast(float factor)
    {
        var c = GameClock.Instance; if (c == null) return;
        int fast = IndexOfSpeed(factor), normal = IndexOfSpeed(1f);
        if (fast < 0) return;
        c.SetSpeedIndex(c.SpeedIndex == fast ? normal : fast);        // pressing the active one goes back to normal speed
    }

    void RefreshClockControls()
    {
        var c = GameClock.Instance;
        if (c == null || playIconImg == null) return;
        bool paused = c.Speed <= 0f;
        if (c.SpeedIndex > 0) resumeIndex = c.SpeedIndex;
        var want = paused ? playIcon : pauseIcon;
        if (want != null && playIconImg.sprite != want) playIconImg.sprite = want;
        playBg.color = paused ? UIKit.T.teal : Color.clear;
        bool f3 = Mathf.Approximately(c.Speed, 3f), f6 = Mathf.Approximately(c.Speed, 6f);
        fast3Bg.color = f3 ? new Color(1f, 1f, 1f, 0.26f) : Color.clear;
        fast6Bg.color = f6 ? new Color(1f, 1f, 1f, 0.26f) : Color.clear;
        fast3Label.color = f3 ? UIKit.T.ink : UIKit.T.dim;
        fast6Label.color = f6 ? UIKit.T.ink : UIKit.T.dim;
    }

    // =============================================================== top bar (thin): agency, game speed, clock, timeline, resources, money
    RectTransform Cluster(RectTransform bar, string name, bool left)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(bar, false);
        r.anchorMin = new Vector2(left ? 0f : 1f, 0f); r.anchorMax = new Vector2(left ? 0f : 1f, 1f);
        r.pivot = new Vector2(left ? 0f : 1f, 0.5f);
        r.anchoredPosition = new Vector2(left ? 9f : -9f, 0f); r.sizeDelta = Vector2.zero;
        var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 7f; h.childAlignment = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
        r.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        return r;
    }

    static LayoutElement Size(Component c, float w, float h)
    {
        var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.minWidth = w > 0 ? w : -1; le.preferredHeight = h; le.minHeight = h;
        le.flexibleWidth = 0; le.flexibleHeight = 0;
        return le;
    }

    static void Small(Component root, float size)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) { t.enableAutoSizing = false; t.fontSize = size; }
    }

    void BuildTopBar()
    {
        var T = UIKit.T;
        var bar = new GameObject("Top Bar", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(transform, false);
        bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(-16f, 19f); bar.anchoredPosition = new Vector2(0f, -5f);
        UIKit.Glass(bar);
        bar.SetSiblingIndex(top.GetSiblingIndex());
        var left = Cluster(bar, "Left", true);
        var right = Cluster(bar, "Right", false);

        // ---- agency: logo + name
        var badge = UIKit.At(left, "Agency", 0, 0, 60, 13);
        agencyLogo = UIKit.At(badge, "Logo", 0, -0.5f, 14, 14);
        agencyName = UIKit.TextAt(badge, "Name", 18, 0, 60, 13, Agency.Name, 7f, TextAlignmentOptions.MidlineLeft, true);
        badgeRt = badge;

        // ---- game speed: pause/play, 3x, 6x
        var ctl = UIKit.At(left, "Game Speed", 0, 0, 58, 13);
        UIKit.Tint(ctl, T.track);
        Size(ctl, 58f, 13f);
        var pr = UIKit.At(ctl, "Play", 1.5f, 1.5f, 19, 10);
        var pb = UIKit.MakeButton(pr, "", Color.clear, TogglePause); playBg = (Image)pb.targetGraphic;
        playIconImg = UIKit.IconAt(pr, pauseIcon, 5.5f, 1.5f, 8, 7, T.ink);
        pr.gameObject.AddComponent<TooltipTrigger>().text = "Pause / play";
        var r3 = UIKit.At(ctl, "3x", 21.5f, 1.5f, 17, 10);
        var b3 = UIKit.MakeButton(r3, "", Color.clear, () => SetFast(3f)); fast3Bg = (Image)b3.targetGraphic;
        fast3Label = UIKit.Text(r3, "3×", 6.8f, TextAlignmentOptions.Center, true);
        r3.gameObject.AddComponent<TooltipTrigger>().text = "3× speed";
        var r6 = UIKit.At(ctl, "6x", 39.5f, 1.5f, 17, 10);
        var b6 = UIKit.MakeButton(r6, "", Color.clear, () => SetFast(6f)); fast6Bg = (Image)b6.targetGraphic;
        fast6Label = UIKit.Text(r6, "6×", 6.8f, TextAlignmentOptions.Center, true);
        r6.gameObject.AddComponent<TooltipTrigger>().text = "6× speed";

        // ---- clock + date, reusing the existing objects (TimeBarUI keeps updating them)
        var timeText = top.Find("Time Bar/Time Text");
        var dateBtn = top.Find("Time Bar/Date Button");
        if (timeText != null) { timeText.SetParent(left, false); Size(timeText, 32f, 13f); Small(timeText, 7f); }
        if (dateBtn != null) { dateBtn.SetParent(left, false); Size(dateBtn, 70f, 13f); Small(dateBtn, 6.8f); }
        foreach (var n in new[] { "Time Bar/Speed Controls", "Time Bar/Day Progress Slider" })
        {
            var o = top.Find(n); if (o != null) o.gameObject.SetActive(false);              // replaced by the controls above
        }

        // ---- right: resources, reputation, money
        var res = UIKit.At(right, "Resources Pill", 0, 0, 46, 13);
        UIKit.Tint(res, T.chip); Size(res, 46f, 13f);
        resText = UIKit.Text(res, "Staff 25", 6.5f, TextAlignmentOptions.Center, true);
        res.gameObject.AddComponent<TooltipTrigger>().text = "Resources\nStaff and facilities your agency runs.";

        var rep = UIKit.At(right, "Reputation Pill", 0, 0, 52, 13);
        UIKit.Tint(rep, T.chip); Size(rep, 52f, 13f);
        repText = UIKit.TextAt(rep, "Label", 5, 0, 44, 11, "Rep LVL 1", 6.3f, TextAlignmentOptions.MidlineLeft, true);
        var track = UIKit.At(rep, "Bar", 5, 9.5f, 42, 2f); UIKit.Tint(track, new Color(1f, 1f, 1f, 0.18f));
        repFill = UIKit.Tint(UIKit.At(track, "Fill", 0, 0, 10, 2f), T.teal);
        rep.gameObject.AddComponent<TooltipTrigger>().text = "Reputation\nGrows when contracts finish. Higher levels unlock better contracts.";

        var money = top.Find("Money");
        if (money != null)
        {
            money.SetParent(right, false);
            Size(money, -1f, 13f);
            Small(money, 7.5f);
            var icon = money.Find("Money Icon");
            if (icon != null) Size(icon, 10f, 10f);
            var mt = money.Find("Money Text");
            moneyText = mt != null ? mt.GetComponent<TMP_Text>() : null;
        }

        // ---- centre: mission timeline
        var mission = top.Find("Mission Bar") as RectTransform;
        if (mission != null)
        {
            mission.SetParent(bar, false);
            mission.anchorMin = mission.anchorMax = new Vector2(0.5f, 0.5f); mission.pivot = new Vector2(0.5f, 0.5f);
            mission.anchoredPosition = new Vector2(34f, 0f);
            Small(mission, 6.5f);
            var ms = mission.Find("Mission Progress Slider");
            if (ms != null) Size(ms, 54f, 4f);
        }

        // the old top panel stays (TimeBarUI lives on it) but draws nothing
        var oldImg = top.GetComponent<Image>();
        if (oldImg != null) oldImg.enabled = false;
        var oldShadow = top.GetComponent<UIShadow>();
        if (oldShadow != null) oldShadow.enabled = false;
        RefreshStats();
    }

    RectTransform agencyLogo, badgeRt;

    void RefreshStats()
    {
        if (moneyText != null) moneyText.text = Agency.Money.ToString("N0", CultureInfo.InvariantCulture);
        if (repText != null) repText.text = "Rep LVL " + Agency.RepLevel;
        if (repFill != null) ((RectTransform)repFill.transform).sizeDelta = new Vector2(42f * Agency.RepProgress, 2f);
        if (resText != null) resText.text = "Staff " + Agency.Staff;
        if (agencyName != null)
        {
            agencyName.text = Agency.Name;
            float tw = Mathf.Min(90f, agencyName.GetPreferredValues(Agency.Name).x + 2f);       // the badge hugs the name
            ((RectTransform)agencyName.transform.parent).sizeDelta = new Vector2(tw, 13f);
            if (badgeRt != null) Size(badgeRt, 18f + tw, 13f);
        }
        if (agencyLogo != null) LogoArt.Draw(agencyLogo, Agency.Logo, 14f);
    }
}
