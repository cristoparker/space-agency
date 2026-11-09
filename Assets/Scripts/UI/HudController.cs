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

    static readonly (string label, string[] items)[] Nav =
    {
        ("Contract", new string[0]),
        ("Training", new[] { "Scientist Training", "Pilot Training" }),
        ("Building", new[] { "Facility", "Payload", "Rocket" }),
        ("Testing", new[] { "Module 1", "Module 2", "Module 3" }),
    };

    RectTransform canvasRt, top;
    readonly List<GameObject> subMenus = new List<GameObject>();
    readonly List<Image> navImages = new List<Image>();
    int openGroup = -1;
    GameObject facilityPanel;
    TMP_Text toastText, moneyText, repText, resText, agencyName;
    Image repFill, playIconImg;
    float toastTimer;
    RectTransform toastBox;
    int lastSpeedIndex = 1;

    void Awake()
    {
        Hud.OnToast = ShowToast;
    }

    void OnDestroy()
    {
        if (Hud.OnToast == ShowToast) Hud.OnToast = null;
        Agency.Changed -= RefreshStats;
        Panels.Unregister("contracts"); Panels.Unregister("facility"); Panels.Unregister("rocket"); Panels.Unregister("payload");
    }

    void Start()
    {
        if (UITheme.Current == null) { Debug.LogError("HudController: Resources/UITheme is missing. Run Tools / Space Agency / Setup Game UI."); return; }
        canvasRt = (RectTransform)transform;
        top = transform.Find("Top Panel") as RectTransform;
        Canvas.ForceUpdateCanvases();

        BuildPanels();
        BuildNav();
        BuildToast();
        if (top != null) BuildTopExtras();
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
        BuildFacility((RectTransform)facilityPanel.transform);
        Panels.Register("facility", facilityPanel, () => { facilityPanel.SetActive(true); facilityPanel.transform.SetAsLastSibling(); });

        if (builder != null)
        {
            Panels.Register("rocket", builder.gameObject, () => { builder.mode = DesignerMode.Rocket; builder.gameObject.SetActive(true); });
            Panels.Register("payload", builder.gameObject, () => { builder.mode = DesignerMode.Payload; builder.gameObject.SetActive(true); });
        }
    }

    void BuildFacility(RectTransform r)
    {
        const float W = 198f, H = 190f;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -36f); r.sizeDelta = new Vector2(W, H);
        UIKit.Glass(r);
        UIKit.TitleBar(r, "FACILITY", UIKit.T.teal, W, () => facilityPanel.SetActive(false));
        var list = UIKit.ScrollList(r, "List", 6, 26, W - 12, H - 32, null, 3f);
        var root = GameObject.Find("Base Buildings");
        var names = new List<string>();
        if (root != null) foreach (Transform c in root.transform) names.Add(c.name);
        if (names.Count == 0) names.AddRange(new[] { "LaunchPad", "Hangar", "MissionControl" });
        foreach (var n in names)
        {
            var row = UIKit.At(list, n, 0, 0, 100, 22);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            UIKit.Tint(row, UIKit.T.chip);
            UIKit.TextAt(row, "Name", 6, 0, 110, 22, Nicify(n), 7.5f, TextAlignmentOptions.MidlineLeft, true);
            UIKit.TextAt(row, "Level", 0, 0, 80, 22, "Level 1  -  upgrades soon", 6f, TextAlignmentOptions.MidlineRight, false, UIKit.T.dim);
            ((RectTransform)row.Find("Level")).anchorMin = ((RectTransform)row.Find("Level")).anchorMax = new Vector2(1, 1);
            ((RectTransform)row.Find("Level")).pivot = new Vector2(1, 1);
            ((RectTransform)row.Find("Level")).anchoredPosition = new Vector2(-4, 0);
        }
    }

    static string Nicify(string s) => System.Text.RegularExpressions.Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ");

    // =============================================================== bottom navigation
    void BuildNav()
    {
        const float bw = 46f, gap = 4f, pad = 5f, bh = 18f;
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
            var b = UIKit.Btn(bar, Nav[g].label, x, pad, bw, bh, UIKit.T.teal, () => OnNav(gi), 7f);
            navImages.Add((Image)b.targetGraphic);

            var items = Nav[g].items;
            if (items.Length == 0) { subMenus.Add(null); continue; }
            var sub = new GameObject(Nav[g].label + " Menu", typeof(RectTransform)).GetComponent<RectTransform>();
            sub.SetParent(bar, false);
            sub.anchorMin = sub.anchorMax = Vector2.zero; sub.pivot = Vector2.zero;
            sub.anchoredPosition = new Vector2(x, h + 3f);
            float ch = 13f, cg = 2f;
            sub.sizeDelta = new Vector2(bw + 12f, items.Length * (ch + cg));
            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i];
                var chip = UIKit.At(sub, item, 0, i * (ch + cg), bw + 12f, ch);
                UIKit.MakeButton(chip, item, new Color(0.04f, 0.07f, 0.12f, 0.92f), () => OnSubMenu(item), 6.2f, false);
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
        navImages[g].color = new Color(0.45f, 0.88f, 1f, 1f);
    }

    void CloseMenus()
    {
        openGroup = -1;
        foreach (var s in subMenus) if (s != null) s.SetActive(false);
        foreach (var i in navImages) i.color = UIKit.T.teal;
    }

    void OnSubMenu(string item)
    {
        CloseMenus();
        switch (item)
        {
            case "Facility": Panels.Open("facility"); break;
            case "Payload": Panels.Open("payload"); break;
            case "Rocket": Panels.Open("rocket"); break;
            case "Scientist Training": case "Pilot Training": Hud.Toast(item + " is coming soon."); break;
            default: Hud.Toast("Test " + item.ToLowerInvariant() + " is coming soon."); break;
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
        var clock = GameClock.Instance;
        if (clock != null && playIconImg != null)
        {
            var want = clock.Speed <= 0f ? playIcon : pauseIcon;
            if (want != null && playIconImg.sprite != want) playIconImg.sprite = want;
            if (clock.SpeedIndex > 0) lastSpeedIndex = clock.SpeedIndex;
        }
    }

    // =============================================================== top bar extras
    void BuildTopExtras()
    {
        // play / pause: a copy of the slow-down button so it matches the existing glass buttons
        var speed = top.Find("Time Bar/Speed Controls");
        var slow = speed != null ? speed.Find("Slow Down Button") : null;
        if (slow != null)
        {
            var play = Instantiate(slow.gameObject, speed);
            play.name = "Play Pause Button";
            play.transform.SetSiblingIndex(0);
            var btn = play.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.interactable = true;
            btn.onClick.AddListener(() =>
            {
                var c = GameClock.Instance; if (c == null) return;
                if (c.Speed <= 0f) c.SetSpeedIndex(Mathf.Max(1, lastSpeedIndex)); else { lastSpeedIndex = c.SpeedIndex; c.SetSpeedIndex(0); }
            });
            playIconImg = play.transform.Find("Icon") != null ? play.transform.Find("Icon").GetComponent<Image>() : null;
            if (playIconImg != null && pauseIcon != null) playIconImg.sprite = pauseIcon;
        }

        // free some width: the day slider gets slimmer
        var daySlider = top.Find("Time Bar/Day Progress Slider");
        var le = daySlider != null ? daySlider.GetComponent<LayoutElement>() : null;
        if (le != null) { le.preferredWidth = 34f; le.minWidth = 34f; }

        var money = top.Find("Money") as RectTransform;
        if (money != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(money);
            var mt = money.Find("Money Text");
            moneyText = mt != null ? mt.GetComponent<TMP_Text>() : null;
        }
        float mw = money != null ? Mathf.Max(60f, LayoutUtility.GetPreferredWidth(money)) : 80f;
        float right = 10f + mw + 8f;

        // Reputation pill (level + progress) and Resources pill, right of the timeline
        var rep = UIKit.At(top, "Reputation Pill", 0, 0, 58, 18);
        UIKit.AnchorRight(rep, right, 5.5f);
        UIKit.Tint(rep, UIKit.T.chip);
        repText = UIKit.TextAt(rep, "Label", 4, 0, 52, 13, "Rep LVL 1", 6.5f, TextAlignmentOptions.MidlineLeft, true);
        var bar = UIKit.At(rep, "Bar", 4, 13, 50, 2.5f); UIKit.Tint(bar, new Color(1f, 1f, 1f, 0.18f));
        var fill = UIKit.At(bar, "Fill", 0, 0, 20, 2.5f); repFill = UIKit.Tint(fill, UIKit.T.purple);
        right += 58f + 4f;

        var res = UIKit.At(top, "Resources Pill", 0, 0, 58, 18);
        UIKit.AnchorRight(res, right, 5.5f);
        UIKit.Tint(res, UIKit.T.chip);
        resText = UIKit.Text(res, "Staff 25", 6.5f, TextAlignmentOptions.Center, true);
        res.gameObject.AddComponent<TooltipTrigger>().text = "Resources\nStaff and facilities your agency runs.";
        rep.gameObject.AddComponent<TooltipTrigger>().text = "Reputation\nGrows when contracts finish. Higher levels unlock better contracts.";
        right += 58f + 6f;

        var mission = top.Find("Mission Bar") as RectTransform;
        if (mission != null)
        {
            mission.anchoredPosition = new Vector2(-(right - 12f), mission.anchoredPosition.y);
            var ms = mission.Find("Mission Progress Slider");
            var mle = ms != null ? ms.GetComponent<LayoutElement>() : null;
            if (mle != null) { mle.preferredWidth = 40f; mle.minWidth = 40f; }       // keeps the timeline clear of the date
        }

        // agency badge under the top bar, top-left
        var badge = UIKit.At(transform, "Agency Badge", 8, 31, 124, 20);
        UIKit.Glass(badge);
        var logoBox = UIKit.At(badge, "Logo", 3, 2, 16, 16);
        agencyLogo = logoBox;
        agencyName = UIKit.TextAt(badge, "Name", 23, 0, 97, 20, Agency.Name, 6.5f, TextAlignmentOptions.MidlineLeft, true);
        agencyName.overflowMode = TextOverflowModes.Ellipsis;
        badge.SetAsFirstSibling();
        badge.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
        RefreshStats();
    }

    RectTransform agencyLogo;

    void RefreshStats()
    {
        if (moneyText != null) moneyText.text = Agency.Money.ToString("N0", CultureInfo.InvariantCulture);
        if (repText != null) repText.text = "Rep LVL " + Agency.RepLevel;
        if (repFill != null) ((RectTransform)repFill.transform).sizeDelta = new Vector2(50f * Agency.RepProgress, 2.5f);
        if (resText != null) resText.text = "Staff " + Agency.Staff;
        if (agencyName != null) agencyName.text = Agency.Name;
        if (agencyLogo != null) LogoArt.Draw(agencyLogo, Agency.Logo, 16f);
    }
}
