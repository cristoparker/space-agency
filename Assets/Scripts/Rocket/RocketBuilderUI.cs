using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The Rocket Builder window. Pick parts by category, see the rocket in 3D, edit the build, and watch the numbers
/// and the contract checks update live. The layout is created in code the first time the window opens.
/// </summary>
public class RocketBuilderUI : MonoBehaviour
{
    [Header("Data")]
    public PartLibrary library;

    [Header("Style")]
    public Material flatMat;
    public Sprite roundSprite;
    public TMP_FontAsset bodyFont, headFont;
    public Color normalColor = new Color(1f, 1f, 1f, 0.14f);
    public Color activeColor = new Color(0.25f, 0.8f, 1f, 0.38f);

    static readonly (string label, string[] cats)[] Tabs =
    {
        ("Engines", new[] { "Engine", "Thruster" }), ("Tanks", new[] { "Tank" }), ("Heads", new[] { "Head" }),
        ("Boosters", new[] { "Booster" }), ("Payloads", new[] { "Payload" }), ("Comms", new[] { "Comms" }),
        ("Power", new[] { "Power" }), ("Recovery", new[] { "Recovery" }), ("Structure", new[] { "Structure" }),
    };

    static readonly Color[] Liveries =
    {
        new Color32(232, 232, 228, 255), new Color32(44, 46, 52, 255), new Color32(205, 135, 80, 255), new Color32(86, 112, 150, 255),
        new Color32(170, 82, 72, 255), new Color32(130, 134, 140, 255), new Color32(96, 128, 100, 255), new Color32(196, 168, 88, 255),
    };

    const string SaveKey = "SpaceAgency.RocketDesign";
    static readonly Color Ink = new Color(0.92f, 0.95f, 1f, 1f), Dim = new Color(0.92f, 0.95f, 1f, 0.5f);
    const string Good = "#8FE3B0", Warn = "#FFB878", Bad = "#FF8C8C", Soft = "#9FB4C8", Cyan = "#7FDBFF";

    // ---- state ----
    RocketDesign design = new RocketDesign();
    RocketPreview preview;
    RocketReport report;
    PartInfo selected;
    int tab;
    bool built;
    readonly List<MissionDefinition> missions = new List<MissionDefinition> { null };
    int missionIndex;

    // ---- ui ----
    readonly List<Image> tabImages = new List<Image>();
    Transform listContent, designContent;
    TMP_Text detailsText, statsText, checksText, missionLabel, addLabel, headerSummary;
    Button addButton;
    readonly List<Image> swatchRings = new List<Image>();

    MissionDefinition Mission => missionIndex > 0 && missionIndex < missions.Count ? missions[missionIndex] : null;

    // =============================================================== lifecycle
    void OnEnable()
    {
        if (library == null) { Debug.LogWarning("RocketBuilderUI: no PartLibrary assigned."); return; }
        if (!built) Build();
        design = RocketDesign.FromJson(PlayerPrefs.GetString(SaveKey, ""));
        design.stack.RemoveAll(id => library.Get(id) == null);
        RefreshMissions();
        preview.SetActive(true);
        RefreshAll();
    }

    void OnDisable()
    {
        if (preview != null) preview.SetActive(false);
        if (built) PlayerPrefs.SetString(SaveKey, design.ToJson());
    }

    void OnDestroy()
    {
        if (preview != null) Destroy(preview.gameObject);
    }

    // =============================================================== layout
    RectTransform Rt(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static void Stretch(RectTransform r, float l = 0, float t = 0, float rr = 0, float b = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
    }

    Image Img(RectTransform r, Color c)
    {
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = roundSprite; i.type = Image.Type.Sliced; i.material = flatMat; i.color = c;
        return i;
    }

    TMP_Text Txt(Transform parent, string s, float size, TextAlignmentOptions al, bool head = false)
    {
        var r = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        Stretch(r);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = head ? headFont : bodyFont;
        t.fontSize = size; t.color = Ink; t.alignment = al; t.text = s;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        return t;
    }

    Button Btn(Transform parent, string label, float x, float y, float w, float h, UnityAction click, float size = 8f)
    {
        var r = Rt(parent, label, x, y, w, h);
        var img = Img(r, normalColor);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(click);
        Txt(r, label, size, TextAlignmentOptions.Center);
        return b;
    }

    // A vertical scroll list; returns the content rect (rows are added by a VerticalLayoutGroup).
    Transform ScrollList(Transform parent, string name, float x, float y, float w, float h)
    {
        var root = Rt(parent, name, x, y, w, h);
        Img(root, new Color(1f, 1f, 1f, 0.06f));
        var vp = Rt(root, "Viewport", 0, 0, w, h); Stretch(vp, 2, 2, 2, 2);
        vp.gameObject.AddComponent<RectMask2D>();
        var content = Rt(vp, "Content", 0, 0, w - 4, 10);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0, 0); content.offsetMax = new Vector2(0, 0);
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 2; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = root.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = content; sr.horizontal = false; sr.scrollSensitivity = 14f; sr.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    // A scrolling block of wrapped rich text.
    TMP_Text ScrollText(Transform parent, string name, float x, float y, float w, float h)
    {
        var root = Rt(parent, name, x, y, w, h);
        Img(root, new Color(1f, 1f, 1f, 0.06f));
        var vp = Rt(root, "Viewport", 0, 0, w, h); Stretch(vp, 2, 2, 2, 2);
        vp.gameObject.AddComponent<RectMask2D>();
        var t = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        t.SetParent(vp, false);
        t.anchorMin = new Vector2(0, 1); t.anchorMax = new Vector2(1, 1); t.pivot = new Vector2(0.5f, 1f);
        t.offsetMin = new Vector2(3, 0); t.offsetMax = new Vector2(-3, 0);
        var tm = t.gameObject.AddComponent<TextMeshProUGUI>();
        tm.font = bodyFont; tm.fontSize = 8f; tm.color = Ink; tm.alignment = TextAlignmentOptions.TopLeft;
        tm.textWrappingMode = TextWrappingModes.Normal; tm.raycastTarget = false; tm.lineSpacing = -5f;
        t.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = root.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = t; sr.horizontal = false; sr.scrollSensitivity = 14f; sr.movementType = ScrollRect.MovementType.Clamped;
        return tm;
    }

    void Build()
    {
        var root = (RectTransform)transform;
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        float W = root.rect.width > 10 ? root.rect.width : 600f;

        // darker backing so text stays readable over a bright scene
        var back = Rt(root, "Backdrop", 0, 0, W, root.rect.height > 10 ? root.rect.height : 340f);
        Stretch(back); Img(back, new Color(0.02f, 0.035f, 0.07f, 0.62f)).raycastTarget = false;

        // ---- header ----
        var header = Rt(root, "Header", 0, 0, W, 24);
        var hi = header.gameObject.AddComponent<Image>(); hi.color = new Color(1, 1, 1, 0);
        header.gameObject.AddComponent<UIDragPanel>().target = root;
        var title = Txt(header, "Rocket Builder", 10f, TextAlignmentOptions.MidlineLeft, true);
        Stretch((RectTransform)title.transform, 12, 0, 30, 0);
        headerSummary = Txt(header, "", 8f, TextAlignmentOptions.MidlineRight);
        Stretch((RectTransform)headerSummary.transform, 140, 0, 34, 0);
        Btn(header, "x", W - 28, 4, 18, 16, () => gameObject.SetActive(false));

        // ---- left: categories + parts ----
        for (int i = 0; i < Tabs.Length; i++)
        {
            int idx = i;
            var b = Btn(root, Tabs[i].label, 8 + (i % 3) * 53, 28 + (i / 3) * 17, 50, 15, () => { tab = idx; selected = null; RefreshAll(); }, 7.5f);
            tabImages.Add(b.targetGraphic as Image);
        }
        listContent = ScrollList(root, "Part List", 8, 82, 160, 118);
        detailsText = ScrollText(root, "Part Details", 8, 204, 160, 92);
        addButton = Btn(root, "Add", 8, 300, 160, 18, AddSelected);
        addLabel = addButton.GetComponentInChildren<TMP_Text>();

        // ---- middle: preview ----
        var pr = Rt(root, "Preview", 176, 28, 160, 272);
        preview = RocketPreview.Create(320, 544);
        var raw = pr.gameObject.AddComponent<RawImage>();
        raw.texture = preview.Texture;
        pr.gameObject.AddComponent<RocketPreviewInput>().preview = preview;
        var hint = Txt(pr, "drag: rotate   scroll: zoom", 7f, TextAlignmentOptions.Bottom);
        hint.color = Dim;
        swatchRings.Clear();
        for (int i = 0; i < Liveries.Length; i++)
        {
            int idx = i;
            var s = Rt(root, "Livery " + i, 176 + i * 20, 304, 16, 14);
            var ring = Img(s, new Color(1, 1, 1, 0.14f));
            var inner = Rt(s, "Colour", 2, 2, 12, 10); Img(inner, Liveries[i]);
            var bt = s.gameObject.AddComponent<Button>(); bt.targetGraphic = ring;
            bt.onClick.AddListener(() => { design.livery = idx; RefreshAll(); });
            swatchRings.Add(ring);
        }
        Btn(root, "Reset view", 176, 320, 160, 14, () => preview.ResetView(), 7.5f);

        // ---- right: contract, build list, stats, checks ----
        float rx = 344, rw = W - rx - 8;
        Btn(root, "<", rx, 28, 16, 15, () => ChangeMission(-1));
        missionLabel = Txt(Rt(root, "Contract", rx + 18, 28, rw - 36, 15), "", 8f, TextAlignmentOptions.Midline);
        missionLabel.transform.parent.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        Btn(root, ">", rx + rw - 16, 28, 16, 15, () => ChangeMission(1));
        var bh = Txt(Rt(root, "Build Title", rx, 47, 100, 12), "Rocket (top to bottom)", 7.5f, TextAlignmentOptions.MidlineLeft); bh.color = Dim;
        Btn(root, "Clear", rx + rw - 44, 46, 44, 13, () => { design.Clear(); RefreshAll(); }, 7.5f);
        designContent = ScrollList(root, "Build List", rx, 62, rw, 96);
        statsText = ScrollText(root, "Stats", rx, 162, rw, 74);
        checksText = ScrollText(root, "Checks", rx, 240, rw, 94);

        built = true;
    }

    // =============================================================== actions
    void AddSelected()
    {
        if (selected == null) return;
        RocketRules.Add(design, selected, library);
        RefreshAll();
    }

    void ChangeMission(int dir)
    {
        if (missions.Count == 0) return;
        missionIndex = (missionIndex + dir + missions.Count) % missions.Count;
        RefreshAll();
    }

    void RefreshMissions()
    {
        var mm = MissionManager.Instance;
        var prev = Mission;
        missions.Clear(); missions.Add(null);
        if (mm != null)
        {
            foreach (var m in mm.Running) if (m.Definition != null && !missions.Contains(m.Definition)) missions.Add(m.Definition);
            foreach (var d in mm.Available) if (d != null && !missions.Contains(d)) missions.Add(d);
        }
        missionIndex = Mathf.Max(0, missions.IndexOf(prev));
        if (prev == null && mm != null && mm.Tracked != null) missionIndex = Mathf.Max(0, missions.IndexOf(mm.Tracked.Definition));
    }

    // =============================================================== refresh
    void RefreshAll()
    {
        if (!built) return;
        for (int i = 0; i < tabImages.Count; i++) tabImages[i].color = i == tab ? activeColor : normalColor;
        for (int i = 0; i < swatchRings.Count; i++) swatchRings[i].color = i == design.livery ? activeColor : new Color(1, 1, 1, 0.14f);

        RefreshPartList();
        RefreshDetails();
        RefreshDesignList();

        report = RocketStats.Evaluate(design, library, Mission);
        RefreshTexts();

        var rocket = RocketAssembler.Build(design, library, out var b);
        preview.Show(rocket, b);
        PlayerPrefs.SetString(SaveKey, design.ToJson());
    }

    void RefreshPartList()
    {
        foreach (Transform c in listContent) Destroy(c.gameObject);
        var cats = Tabs[tab].cats;
        foreach (var p in library.Parts.Where(x => cats.Contains(x.category)).OrderBy(x => x.category).ThenBy(x => PartInfo.SizeRank(x.size)).ThenBy(x => x.costM))
        {
            var part = p;
            bool ok = RocketRules.CanAdd(design, part, library, out _);
            var r = Rt(listContent, part.id, 0, 0, 100, 14);
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
            var img = Img(r, part == selected ? activeColor : normalColor);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            b.onClick.AddListener(() => { selected = part; RefreshAll(); });
            var name = Txt(r, part.DisplayName, 8f, TextAlignmentOptions.MidlineLeft); Stretch((RectTransform)name.transform, 4, 0, 40, 0);
            var cost = Txt(r, "$" + part.costM.ToString("0.#") + "M", 7.5f, TextAlignmentOptions.MidlineRight); Stretch((RectTransform)cost.transform, 60, 0, 4, 0);
            name.color = ok ? Ink : Dim; cost.color = Dim;
        }
    }

    void RefreshDetails()
    {
        if (selected == null)
        {
            detailsText.text = "<color=" + Soft + ">Pick a part from the list to see its stats.</color>";
            addButton.interactable = false; addLabel.text = "Add";
            return;
        }
        var p = selected;
        var sb = new StringBuilder();
        sb.AppendLine("<b>" + p.DisplayName + "</b>  <color=" + Soft + ">" + p.category + " " + p.size + "</color>");
        sb.AppendLine("Height " + p.heightM.ToString("0.0") + " m   Cost $" + p.costM.ToString("0.##") + "M");
        if (p.dryMassKg > 0) sb.AppendLine("Dry mass " + p.dryMassKg.ToString("N0") + " kg");
        if (p.massKg > 0) sb.AppendLine("Mass " + p.massKg.ToString("N0") + " kg");
        if (p.fuelKg > 0) sb.AppendLine("Fuel " + p.fuelKg.ToString("N0") + " kg");
        if (p.thrustKn > 0 || p.thrustVacKn > 0) sb.AppendLine("Thrust " + p.thrustKn.ToString("0.##") + " / " + p.thrustVacKn.ToString("0.##") + " kN (sea / vac)");
        if (p.ispVac > 0) sb.AppendLine("Isp " + p.ispSl.ToString("0") + " / " + p.ispVac.ToString("0") + " s");
        if (p.bells > 1) sb.AppendLine(p.bells + " engines");
        if (p.powerW != 0) sb.AppendLine((p.powerW > 0 ? (p.category == "Power" ? "Makes " : "Uses ") : "Needs ") + Mathf.Abs(p.powerW).ToString("0") + " W");
        if (p.storageWh > 0) sb.AppendLine("Stores " + p.storageWh.ToString("0") + " Wh");
        if (p.dataMbps > 0) sb.AppendLine("Data " + p.dataMbps.ToString("0.##") + " Mbps");
        if (p.commMbps > 0) sb.AppendLine("Comms " + p.commMbps.ToString("0.##") + " Mbps");
        if (p.payloadMaxKg > 0) sb.AppendLine("Carries up to " + p.payloadMaxKg.ToString("N0") + " kg");
        if (p.crew > 0) sb.AppendLine("Crew " + p.crew);
        if (p.controlLevel > 0) sb.AppendLine("Steering " + p.reactionWheelNm + " Nm");
        if (!string.IsNullOrEmpty(p.note)) sb.AppendLine("<color=" + Soft + ">" + p.note + "</color>");
        bool ok = RocketRules.CanAdd(design, p, library, out var why);
        if (!ok) sb.AppendLine("<color=" + Bad + ">" + why + "</color>");
        detailsText.text = sb.ToString();

        addButton.interactable = ok;
        switch (RocketRules.SlotOf(p))
        {
            case PartSlot.Booster: addLabel.text = "Set side boosters"; break;
            case PartSlot.Fin: addLabel.text = "Set fins"; break;
            case PartSlot.Payload: addLabel.text = "Set payload"; break;
            case PartSlot.Equipment: addLabel.text = "Add equipment"; break;
            default: addLabel.text = RocketRules.IsHead(p) ? "Set head" : "Add to rocket"; break;
        }
    }

    // One row in the build list: label + small buttons on the right.
    void BuildRow(string label, params (string text, UnityAction act)[] buttons)
    {
        var r = Rt(designContent, "Row", 0, 0, 100, 14);
        r.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
        Img(r, new Color(1, 1, 1, 0.08f));
        var t = Txt(r, label, 8f, TextAlignmentOptions.MidlineLeft);
        Stretch((RectTransform)t.transform, 4, 0, buttons.Length * 15 + 4, 0);
        for (int i = 0; i < buttons.Length; i++)
        {
            var (text, act) = buttons[buttons.Length - 1 - i];
            var b = Btn(r, text, 0, 1, 13, 12, act, 7.5f);
            var br = (RectTransform)b.transform;
            br.anchorMin = br.anchorMax = new Vector2(1, 1); br.pivot = new Vector2(1, 1);
            br.anchoredPosition = new Vector2(-2 - i * 15, -1);
        }
    }

    void RefreshDesignList()
    {
        foreach (Transform c in designContent) Destroy(c.gameObject);
        for (int i = design.stack.Count - 1; i >= 0; i--)
        {
            int idx = i;
            var p = library.Get(design.stack[i]);
            string stage = "";
            BuildRow((i + 1) + ". " + p.DisplayName + stage,
                ("^", () => Swap(idx, idx + 1)), ("v", () => Swap(idx, idx - 1)), ("x", () => { design.stack.RemoveAt(idx); RefreshAll(); }));
        }
        var booster = library.Get(design.boosterId);
        if (booster != null)
            BuildRow("<color=" + Cyan + ">Side</color> " + booster.DisplayName + "  x" + design.boosterCount,
                ("-", () => { design.boosterCount = Mathf.Max(1, design.boosterCount - 1); RefreshAll(); }),
                ("+", () => { design.boosterCount = Mathf.Min(6, design.boosterCount + 1); RefreshAll(); }),
                ("x", () => { design.boosterId = null; RefreshAll(); }));
        var fin = library.Get(design.finId);
        if (fin != null)
            BuildRow("<color=" + Cyan + ">Fins</color> " + fin.DisplayName + "  x" + design.finCount,
                ("-", () => { design.finCount = Mathf.Max(2, design.finCount - 1); RefreshAll(); }),
                ("+", () => { design.finCount = Mathf.Min(6, design.finCount + 1); RefreshAll(); }),
                ("x", () => { design.finId = null; RefreshAll(); }));
        var pay = library.Get(design.payloadId);
        if (pay != null) BuildRow("<color=" + Cyan + ">Payload</color> " + pay.DisplayName, ("x", () => { design.payloadId = null; RefreshAll(); }));
        for (int i = 0; i < design.equipment.Count; i++)
        {
            int idx = i; var e = library.Get(design.equipment[i]);
            if (e != null) BuildRow("<color=" + Cyan + ">Equip</color> " + e.DisplayName, ("x", () => { design.equipment.RemoveAt(idx); RefreshAll(); }));
        }
        if (designContent.childCount == 0)
        {
            var t = Txt(Rt(designContent, "Empty", 0, 0, 100, 40), "Empty. Start with an engine, add a tank, then a head.", 8f, TextAlignmentOptions.Center);
            t.color = Dim; t.textWrappingMode = TextWrappingModes.Normal;
            t.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        }
    }

    void Swap(int a, int b)
    {
        if (a < 0 || b < 0 || a >= design.stack.Count || b >= design.stack.Count) return;
        (design.stack[a], design.stack[b]) = (design.stack[b], design.stack[a]);
        RefreshAll();
    }

    void RefreshTexts()
    {
        var m = Mission;
        missionLabel.text = m == null ? "<color=" + Soft + ">Free build (no contract)</color>" : "<color=" + Cyan + ">" + m.id + "</color>  " + m.displayName;

        var r = report;
        var s = new StringBuilder();
        s.AppendLine("Height " + r.heightM.ToString("0.0") + " m   Width " + r.widthM.ToString("0.0") + " m");
        s.AppendLine("Launch mass <b>" + r.launchKg.ToString("N0") + " kg</b>  (dry " + r.dryKg.ToString("N0") + ", fuel " + r.fuelKg.ToString("N0") + ", payload " + r.payloadKg.ToString("N0") + ")");
        s.AppendLine("Thrust <b>" + r.thrustKn.ToString("N0") + " kN</b>   Liftoff TWR <b>" + r.liftoffTwr.ToString("0.00") + "</b>");
        s.Append("Delta-v <b>" + r.dvTotal.ToString("N0") + " m/s</b>");
        if (r.stages.Count > 1) s.Append("  <color=" + Soft + ">(" + string.Join(" + ", r.stages.Select(x => x.dv.ToString("N0"))) + ")</color>");
        s.AppendLine();
        s.AppendLine("Cost <b>$" + r.costM.ToString("0.0") + "M</b>   Stages " + r.stages.Count(x => x.parts.Count > 0));
        s.AppendLine("Power " + r.powerSupplyW.ToString("0") + " W made / " + r.powerDemandW.ToString("0") + " W used   Comms " + r.commMbps.ToString("0.##") + " Mbps");
        statsText.text = s.ToString();

        var c = new StringBuilder();
        c.AppendLine(r.valid ? "<color=" + Good + "><b>READY TO LAUNCH</b></color>" : "<color=" + Bad + "><b>NOT READY</b></color>");
        foreach (var ch in r.checks)
        {
            string col = ch.sev == Sev.Ok ? Good : ch.sev == Sev.Warn ? Warn : Bad;
            string tag = ch.sev == Sev.Ok ? "OK" : ch.sev == Sev.Warn ? "!" : "X";
            c.AppendLine("<color=" + col + ">[" + tag + "]</color> " + ch.text);
        }
        if (r.checks.Count == 0) c.AppendLine("<color=" + Soft + ">Add parts to see checks.</color>");
        checksText.text = c.ToString();

        headerSummary.text = "<color=" + Soft + ">$" + r.costM.ToString("0.0") + "M   " + r.launchKg.ToString("N0") + " kg   dv " + r.dvTotal.ToString("N0") + "</color>";
    }
}
