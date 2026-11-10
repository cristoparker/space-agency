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
        ("Layer", new[] { "Labels", "Haze", "UI" }),
    };
    static float NavWidth(int g) => 15f;
    static readonly Dictionary<string, string> ItemIcons = new Dictionary<string, string>
    {
        { "Scientist Training", "icon_scientist" }, { "Pilot Training", "icon_pilot" }, { "Facility", "icon_facility" }, { "Payload", "icon_payload" },
        { "Rocket", "icon_rocket" }, { "Engine Test", "icon_engine" }, { "Structure Test", "icon_structure" }, { "Comms Test", "icon_comms" },
        { "Labels", "icon_labels" }, { "UI", "icon_ui" }, { "Vehicles", "icon_vehicles" }, { "Greenery", "icon_greenery" }, { "Haze", "icon_haze" },
    };
    readonly Dictionary<string, Image> layerDots = new Dictionary<string, Image>();
    static readonly Dictionary<string, bool> layerOn = new Dictionary<string, bool> { { "Labels", true }, { "Haze", true }, { "UI", true } };

    RectTransform canvasRt;
    readonly List<GameObject> subMenus = new List<GameObject>();
    readonly List<Image> navImages = new List<Image>();
    int openGroup = -1;
    GameObject facilityPanel;
    TMP_Text toastText, moneyText, repText, resText, agencyName, timeText, dateText;
    float clockTimer;
    Image repFill, playIconImg, playBg, fast2Bg, fast6Bg;
    readonly List<Image> fast2Icons = new List<Image>(), fast6Icons = new List<Image>();
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
        Canvas.ForceUpdateCanvases();

        BuildPanels();
        BuildNav();
        BuildToast();
        BuildingTags.Create(canvasRt);                      // floating name tags, progress bars and completion bubbles (the Labels layer)
        BuildTopBar();
        CalendarUI.Create(canvasRt);                        // drops down from the date in the top bar
        var objectiveMenu = ObjectiveMenuUI.Create(canvasRt);                  // the Objectives button at the left and its panel
        if (FlightManager.Instance == null) new GameObject("Flight Manager").AddComponent<FlightManager>();
        FlightPanelUI.Create(canvasRt, objectiveMenu);                          // 'Mission in flight' under it after a launch
        Agency.Changed += RefreshStats;
        RefreshStats();
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
        foreach (var k in new List<string>(layerOn.Keys)) layerOn[k] = true;                      // a fresh scene starts with every layer on
        BuildingSelector.LabelsVisible = true; BuildingTags.Visible = true; DayNightCycle.HazeEnabled = true;
        const float gap = 2f, pad = 3f, bh = 15f;
        float total = 0f; for (int q = 0; q < Nav.Length - 1; q++) total += NavWidth(q);             // the last entry (Layer) lives in the bottom-right group
        float w = pad * 2 + total + (Nav.Length - 2) * gap, h = bh + pad * 2;
        var bar = new GameObject("Bottom Nav", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(transform, false);
        bar.anchorMin = bar.anchorMax = Vector2.zero; bar.pivot = Vector2.zero;
        bar.anchoredPosition = new Vector2(8f, 8f); bar.sizeDelta = new Vector2(w, h);
        UIKit.Glass(bar);

        // bottom right: the Layer button and the cinematic camera button, side by side
        var rbar = new GameObject("Bottom Right", typeof(RectTransform)).GetComponent<RectTransform>();
        rbar.SetParent(transform, false);
        rbar.anchorMin = rbar.anchorMax = new Vector2(1f, 0f); rbar.pivot = new Vector2(1f, 0f);
        rbar.anchoredPosition = new Vector2(-8f, 8f); rbar.sizeDelta = new Vector2(pad * 2 + 15f * 3 + gap * 2, h);
        UIKit.Glass(rbar);
        var cine = UIKit.At(rbar, "Cinematic", pad + 15f + gap, pad, 15f, bh);
        UIKit.MakeButton(cine, "", UIKit.T.chip, () => { CloseMenus(); CinematicCamera.Begin(this, canvasRt); });
        UIKit.IconAt(cine, Resources.Load<Sprite>("UIIcons/icon_camera"), 3.5f, 3.5f, bh - 7f, bh - 7f, UIKit.T.ink);
        var setBtn = UIKit.At(rbar, "Settings Button", pad + 30f + gap * 2, pad, 15f, bh);
        var settingsUi = SettingsPanelUI.Create(canvasRt);
        UIKit.MakeButton(setBtn, "", UIKit.T.chip, () => { CloseMenus(); settingsUi.Toggle(); });
        UIKit.IconAt(setBtn, Resources.Load<Sprite>("UIIcons/icon_settings"), 3.5f, 3.5f, bh - 7f, bh - 7f, UIKit.T.ink);
        setBtn.gameObject.AddComponent<TooltipTrigger>().text = "Settings\nMusic, bloom, fog, main menu and exit.";
        VisualSettings.ApplyBloom();
        cine.gameObject.AddComponent<TooltipTrigger>().text = "Cinematic camera\nThe interface hides and the camera tours the base. Click or Esc to leave.";

        for (int g = 0; g < Nav.Length; g++)
        {
            int gi = g;
            bool isLayer = Nav[g].label == "Layer";
            Transform parentBar = isLayer ? (Transform)rbar : bar;
            float x = pad; if (!isLayer) for (int q = 0; q < g; q++) x += NavWidth(q) + gap;
            float bw = NavWidth(g);
            var r = UIKit.At(parentBar, Nav[g].label, x, pad, bw, bh);
            var b = UIKit.MakeButton(r, "", UIKit.T.chip, () => OnNav(gi));
            var sprite = navIcons != null && g < navIcons.Length ? navIcons[g] : null;
            if (Nav[g].label == "Layer") sprite = Resources.Load<Sprite>("UIIcons/nav_layers");
            if (sprite != null) UIKit.IconAt(r, sprite, 3.5f, 3.5f, bh - 7f, bh - 7f, UIKit.T.ink);
            else UIKit.Text(r, Nav[g].label.Substring(0, 1), 9f, TextAlignmentOptions.Center, true);
            r.gameObject.AddComponent<TooltipTrigger>().text = Nav[g].label == "Layer" ? "Layer\nShow or hide parts of the map." : Nav[g].label;
            navImages.Add((Image)b.targetGraphic);

            var items = Nav[g].items;
            if (items.Length == 0) { subMenus.Add(null); continue; }
            var sub = new GameObject(Nav[g].label + " Menu", typeof(RectTransform)).GetComponent<RectTransform>();
            sub.SetParent(parentBar, false);
            sub.anchorMin = sub.anchorMax = Vector2.zero; sub.pivot = Vector2.zero;
            sub.anchoredPosition = new Vector2(isLayer ? rbar.sizeDelta.x - 70f : x, h + 3f);
            float ch = 14f, cg = 2f, cw = 70f;
            sub.sizeDelta = new Vector2(cw, items.Length * (ch + cg));
            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i];
                var chip = UIKit.At(sub, item, 0, i * (ch + cg), cw, ch);
                UIKit.Panel(chip, false);
                UIHover.Attach(chip.gameObject, 1.05f, 0.22f);                                    // the whole chip springs and glows under the pointer
                var hit = UIKit.MakeButton(UIKit.At(chip, "Hit", 0, 0, cw, ch), item, Color.clear, () => OnSubMenu(item), 6.8f, false, null, false);
                var label = hit.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.MidlineLeft; label.margin = new Vector4(17f, 0f, 0f, 0f);
                if (ItemIcons.TryGetValue(item, out var iconName))
                {
                    var ic = Resources.Load<Sprite>("UIIcons/" + iconName);
                    if (ic != null) UIKit.IconAt(chip, ic, 4.5f, 3f, 8f, 8f, UIKit.T.ink);
                }
                if (Nav[g].label == "Layer")
                {
                    var dot = UIKit.Tint(UIKit.At(chip, "State", cw - 11f, 4.5f, 5f, 5f), layerOn[item] ? UIKit.T.green : new Color(1f, 1f, 1f, 0.22f));
                    dot.raycastTarget = false; layerDots[item] = dot;
                }
            }
            sub.gameObject.AddComponent<CanvasGroup>();
            sub.gameObject.AddComponent<UIPopIn>();                                               // the menu eases in when it opens
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

    static string LayerText(string item) => item;

    // the Layer menu stays open so several layers can be switched in a row
    void ToggleLayer(string item)
    {
        bool on = !layerOn[item];
        layerOn[item] = on;
        if (layerDots.TryGetValue(item, out var dot) && dot != null) dot.color = on ? UIKit.T.green : new Color(1f, 1f, 1f, 0.22f);
        switch (item)
        {
            case "Labels": BuildingSelector.LabelsVisible = on; BuildingTags.Visible = on; break;
            case "UI": if (!on) HideInterface(); break;
            case "Haze": DayNightCycle.HazeEnabled = on; break;
        }
    }

    // ---- the UI layer: the whole interface goes away, only the world labels stay; Esc brings it back
    readonly List<GameObject> hiddenUi = new List<GameObject>();
    bool uiHidden;
    float uiHintUntil;
    TMP_Text uiHint;

    public void HideInterface()
    {
        if (uiHidden) return;
        uiHidden = true;
        CloseMenus();
        hiddenUi.Clear();
        foreach (Transform c in canvasRt)
        {
            if (BuildingTags.Root != null && c == BuildingTags.Root) continue;
            if (c.gameObject.activeSelf) { hiddenUi.Add(c.gameObject); c.gameObject.SetActive(false); }
        }
        var hint = new GameObject("UI Hint", typeof(RectTransform)).GetComponent<RectTransform>();
        hint.SetParent(canvasRt, false);
        hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f); hint.pivot = new Vector2(0.5f, 0f); hint.anchoredPosition = new Vector2(0f, 10f); hint.sizeDelta = new Vector2(190f, 16f);
        UIKit.Glass(hint, false);
        uiHint = UIKit.Text(hint, "Interface hidden  ·  press Esc to bring it back", 6.2f, TextAlignmentOptions.Center, true);
        hint.gameObject.AddComponent<CanvasGroup>().alpha = 1f;
        uiHintUntil = Time.unscaledTime + 4f;
    }

    public void ShowInterface()
    {
        if (!uiHidden) return;
        uiHidden = false;
        foreach (var go in hiddenUi) if (go != null) go.SetActive(true);
        hiddenUi.Clear();
        if (uiHint != null) Destroy(uiHint.transform.parent.gameObject);
        layerOn["UI"] = true;
        if (layerDots.TryGetValue("UI", out var dot) && dot != null) dot.color = UIKit.T.green;
    }

    void OnSubMenu(string item)
    {
        if (layerOn.ContainsKey(item)) { ToggleLayer(item); return; }
        CloseMenus();
        switch (item)
        {
            case "Facility": FacilityPanelUI.PlotContext = -1; Panels.Open("facility"); break;
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
        UIKit.Panel(r);
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
        if (uiHidden)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) ShowInterface();
            else if (uiHint != null)
            {
                var g = uiHint.transform.parent.GetComponent<CanvasGroup>();
                if (g != null) g.alpha = Time.unscaledTime < uiHintUntil ? 1f : Mathf.MoveTowards(g.alpha, 0f, Time.unscaledDeltaTime * 1.5f);
            }
            return;
        }
        RefreshClockControls();
        clockTimer += Time.unscaledDeltaTime;
        if (clockTimer > 0.2f && timeText != null && GameClock.Instance != null)
        {
            clockTimer = 0f;
            var now = GameClock.Instance.Now;
            timeText.text = now.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            dateText.text = now.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture);
        }
    }

    // =============================================================== game speed: pause / normal / 5x / one day per second
    // One real minute is one game day at normal speed (GameClock.gameMinutesPerSecond = 24); 5x and 60x (one day per second) multiply that.
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
        bool f2 = Mathf.Approximately(c.Speed, 5f), f6 = Mathf.Approximately(c.Speed, 60f);
        fast2Bg.color = f2 ? new Color(1f, 1f, 1f, 0.26f) : Color.clear;
        fast6Bg.color = f6 ? new Color(1f, 1f, 1f, 0.26f) : Color.clear;
        foreach (var i in fast2Icons) i.color = f2 ? UIKit.T.ink : UIKit.T.dim;
        foreach (var i in fast6Icons) i.color = f6 ? UIKit.T.ink : UIKit.T.dim;
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
        bar.SetAsFirstSibling();
        var left = Cluster(bar, "Left", true);
        var right = Cluster(bar, "Right", false);

        // ---- agency: logo + name
        var badge = UIKit.At(left, "Agency", 0, 0, 60, 13);
        agencyLogo = UIKit.At(badge, "Logo", 0, -0.5f, 14, 14);
        agencyName = UIKit.TextAt(badge, "Name", 18, 0, 60, 13, Agency.Name, 7f, TextAlignmentOptions.MidlineLeft, true);
        badgeRt = badge;
        UIHover.Attach(badge.gameObject, 1.04f, 0.14f);

        // ---- game speed: pause/play (normal speed), 4x and 10x - shown as play triangles, the tooltip names the speed
        var ctl = UIKit.At(left, "Game Speed", 0, 0, 58, 13);
        UIKit.Tint(ctl, T.track);
        Size(ctl, 58f, 13f);
        var pr = UIKit.At(ctl, "Play", 1.5f, 1.5f, 19, 10);
        var pb = UIKit.MakeButton(pr, "", Color.clear, TogglePause); playBg = (Image)pb.targetGraphic;
        playIconImg = UIKit.IconAt(pr, pauseIcon, 5.5f, 1.5f, 8, 7, T.ink);
        pr.gameObject.AddComponent<TooltipTrigger>().text = "Pause / play\nNormal speed";
        var r2 = UIKit.At(ctl, "2x", 21.5f, 1.5f, 17, 10);
        var b2 = UIKit.MakeButton(r2, "", Color.clear, () => SetFast(5f)); fast2Bg = (Image)b2.targetGraphic;
        for (int k = 0; k < 2; k++) fast2Icons.Add(UIKit.IconAt(r2, playIcon, 3.4f + k * 5.2f, 1.6f, 6f, 6.8f, T.dim));
        r2.gameObject.AddComponent<TooltipTrigger>().text = "5× speed";
        var r6 = UIKit.At(ctl, "6x", 39.5f, 1.5f, 17, 10);
        var b6 = UIKit.MakeButton(r6, "", Color.clear, () => SetFast(60f)); fast6Bg = (Image)b6.targetGraphic;
        fast6Icons.Add(UIKit.IconAt(r6, Resources.Load<Sprite>("UIIcons/icon_daynight"), 3.5f, 0.5f, 10f, 9f, T.dim));
        r6.gameObject.AddComponent<TooltipTrigger>().text = "One day per second\nA full day and night passes every second.";

        // ---- clock + date (the date opens the calendar)
        var timeRt = UIKit.At(left, "Time", 0, 0, 36, 13);
        Size(timeRt, 36f, 13f);
        timeText = UIKit.Text(timeRt, "", 7f, TextAlignmentOptions.MidlineLeft);
        timeText.raycastTarget = false;
        var dateRt = UIKit.At(left, "Date", 0, 0, 84, 13);
        Size(dateRt, 84f, 13f);
        UIKit.MakeButton(dateRt, "", Color.clear, () => CalendarUI.Toggle());
        UIKit.IconAt(dateRt, Resources.Load<Sprite>("UIIcons/icon_calendar"), 3, 2f, 9, 9, T.ink);
        dateText = UIKit.TextAt(dateRt, "Label", 14, 0, 68, 13, "", 6.6f, TextAlignmentOptions.MidlineLeft);
        dateText.raycastTarget = false;
        dateRt.gameObject.AddComponent<TooltipTrigger>().text = "Calendar\nSee the launch date of your rocket.";

        // ---- right: resources, reputation, money
        var res = UIKit.At(right, "Resources Pill", 0, 0, 46, 13);
        UIKit.Tint(res, T.chip); Size(res, 46f, 13f);
        resText = UIKit.Text(res, "Staff 25", 6.5f, TextAlignmentOptions.Center, true);
        UIHover.Attach(res.gameObject, 1.05f, 0.18f);
        res.gameObject.AddComponent<TooltipTrigger>().text = "Resources\nStaff and facilities your agency runs.";

        var rep = UIKit.At(right, "Reputation Pill", 0, 0, 52, 13);
        UIKit.Tint(rep, T.chip); Size(rep, 52f, 13f);
        repText = UIKit.TextAt(rep, "Label", 5, 0, 44, 11, "Rep LVL 1", 6.3f, TextAlignmentOptions.MidlineLeft, true);
        var track = UIKit.At(rep, "Bar", 5, 9.5f, 42, 2f); UIKit.Tint(track, new Color(1f, 1f, 1f, 0.18f));
        repFill = UIKit.Tint(UIKit.At(track, "Fill", 0, 0, 10, 2f), T.teal);
        UIHover.Attach(rep.gameObject, 1.05f, 0.18f);
        rep.gameObject.AddComponent<TooltipTrigger>().text = "Reputation\nGrows when contracts finish. Higher levels unlock better contracts.";

        var money = UIKit.At(right, "Money", 0, 0, 76, 13);
        Size(money, 76f, 13f);
        UIKit.IconAt(money, Resources.Load<Sprite>("UIIcons/icon_money"), 1, 1.5f, 10, 10, new Color(0.62f, 1f, 0.76f, 1f));
        moneyText = UIKit.TextAt(money, "Amount", 13, 0, 62, 13, "", 7.4f, TextAlignmentOptions.MidlineLeft, true);
        moneyText.raycastTarget = false;
        UIHover.Attach(money.gameObject, 1.05f, 0.14f);
        money.gameObject.AddComponent<TooltipTrigger>().text = "Money\nYour agency's budget.";

        // ---- centre: progress of the tracked contract
        var mission = DeadlineBarUI.Create(left);                          // contract deadline joined with the progress bar, next to the date
        Size(mission, DeadlineBarUI.W, DeadlineBarUI.H);
        LaunchDayUI.Create(bar, left, mission);              // launch countdown next to the date, glowing Launch button on launch day
        RefreshStats();
    }

    RectTransform agencyLogo, badgeRt;

    void RefreshStats()
    {
        if (moneyText != null) moneyText.text = UIKit.MoneyM(Agency.Money);
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
