using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hover and select the facilities of the space centre (an empty plot counts too). The one under the mouse lights up and shows its
/// name floating above it. Clicking selects it: it keeps an outline and its name, and an info card shows its level, what is
/// running there, shortcuts to the matching windows and the Build / Upgrade button.
/// Each facility object has children "Plot", "L1", "L2", "L3"; whichever is visible is what gets picked.
/// </summary>
public class BuildingSelector : MonoBehaviour
{
    public Material outline;                         // cyan inflated back-face material (from the PartLibrary)
    public RectTransform canvasRect;
    public Camera cam;

    sealed class Entry
    {
        public Transform t; public Color baseColor = Color.white;
        public int plot = -1;                       // 0..4 for the empty/buildable plots, -1 for fixed buildings
        public MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        public GameObject outlineGo; public Renderer outlined;
    }

    readonly List<Entry> entries = new List<Entry>();
    Entry hover, selected;
    RectTransform hoverLabel, selectLabel, card;
    TMP_Text hoverText, selectText, cardTitle, cardState, cardBody;
    RectTransform cardButtons;
    float cardTimer;

    static readonly Dictionary<string, (string label, System.Action action)[]> Actions = new Dictionary<string, (string, System.Action)[]>
    {
        { "LaunchPad", new (string, System.Action)[] { ("Rocket designer", () => Panels.Open("rocket")) } },
        { "MissionControl", new (string, System.Action)[] { ("Contracts", () => Panels.Open("contracts")) } },
        { "AdminOffice", new (string, System.Action)[] { ("Training", () => Panels.Open("programs:0")) } },
        { "Hangar", new (string, System.Action)[] { ("Pilot training", () => Panels.Open("programs:0")) } },
        { "FuelDepot", new (string, System.Action)[0] },
        { "RadarTower", new (string, System.Action)[] { ("Comms test", () => Panels.Open("programs:1")) } },
        { "ResearchLab", new (string, System.Action)[] { ("Structure test", () => Panels.Open("programs:1")) } },
        { "TestStand", new (string, System.Action)[] { ("Engine test", () => Panels.Open("programs:1")) } },
    };

    public void Init()
    {
        if (cam == null) cam = Camera.main;
        var root = GameObject.Find("Base Buildings");
        if (root != null)
            foreach (Transform c in root.transform)
            {
                var r = c.GetComponentInChildren<Renderer>(true);
                Color col = r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor") ? r.sharedMaterial.GetColor("_BaseColor") : Color.white;
                entries.Add(new Entry { t = c, baseColor = col });
            }
        var plots = GameObject.Find("Base Plots");
        if (plots != null)
            for (int i = 0; i < plots.transform.childCount; i++)
                entries.Add(new Entry { t = plots.transform.GetChild(i), plot = i, baseColor = Color.white });
        hoverLabel = MakeLabel("Hover Label", out hoverText, false);
        selectLabel = MakeLabel("Selected Label", out selectText, true);
        BuildCard();
        hoverLabel.gameObject.SetActive(false); selectLabel.gameObject.SetActive(false); card.gameObject.SetActive(false);
        if (BaseSim.Instance != null) BaseSim.Instance.Changed += OnBaseChanged;
    }

    void OnDestroy()
    {
        if (BaseSim.Instance != null) BaseSim.Instance.Changed -= OnBaseChanged;
    }

    // the visible model (plot or the current level)
    static Renderer Rend(Entry e)
    {
        if (e == null || e.t == null) return null;
        for (int i = e.t.childCount - 1; i >= 0; i--)           // the facility (added last) wins over the plot marker
        {
            var ch = e.t.GetChild(i);
            if (ch.gameObject.activeSelf) { var r = ch.GetComponentInChildren<MeshRenderer>(); if (r != null) return r; }
        }
        return null;
    }

    static Bounds BoundsOf(Entry e)
    {
        Bounds b = default; bool first = true;
        foreach (Transform ch in e.t)
        {
            if (!ch.gameObject.activeSelf) continue;
            foreach (var r in ch.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name == "Selection Outline") continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
        }
        return b;
    }

    // ---------------------------------------------------------------- ui
    RectTransform MakeLabel(string name, out TMP_Text text, bool selectedStyle)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(canvasRect, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(60f, 13f);
        var img = UIKit.Tint(r, selectedStyle ? UIKit.T.teal : new Color(0.07f, 0.08f, 0.12f, 0.8f));
        img.raycastTarget = false;
        text = UIKit.Text(r, "", 7f, TextAlignmentOptions.Center, true);
        var tip = new GameObject("Tip", typeof(RectTransform)).GetComponent<RectTransform>();
        tip.SetParent(r, false);
        tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 0f); tip.pivot = new Vector2(0.5f, 0.5f);
        tip.sizeDelta = new Vector2(5f, 5f); tip.anchoredPosition = new Vector2(0f, -1.5f); tip.localRotation = Quaternion.Euler(0, 0, 45f);
        var ti = tip.gameObject.AddComponent<Image>(); ti.color = img.color; ti.raycastTarget = false;
        r.SetAsFirstSibling();                       // behind every window, so an open window is never covered by a name
        return r;
    }

    void BuildCard()
    {
        card = new GameObject("Building Card", typeof(RectTransform)).GetComponent<RectTransform>();
        card.SetParent(canvasRect, false);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f); card.pivot = new Vector2(0.5f, 0f);
        card.anchoredPosition = new Vector2(0f, 10f); card.sizeDelta = new Vector2(210f, 84f);
        UIKit.Panel(card);
        cardTitle = UIKit.TextAt(card, "Title", 10, 7, 120, 13, "", 9.5f, TextAlignmentOptions.MidlineLeft, true);
        cardState = UIKit.TextAt(card, "State", 100, 7, 100, 13, "", 6.5f, TextAlignmentOptions.MidlineRight, true);
        cardBody = UIKit.TextAt(card, "Body", 10, 22, 190, 28, "", 6.5f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
        cardBody.textWrappingMode = TextWrappingModes.Normal; cardBody.lineSpacing = -6f;
        cardButtons = UIKit.At(card, "Buttons", 10, 62, 190, 16);
        cardButtons.anchorMin = cardButtons.anchorMax = new Vector2(0f, 1f); cardButtons.pivot = new Vector2(0f, 1f);
    }

    void OnBaseChanged()
    {
        if (selected == null) return;
        SetOutline(selected, false);
        SetOutline(selected, true);
        SetTint(selected, 1.25f);
        FillCard();
    }

    static string IdOf(Entry e) => e.plot >= 0 ? (BaseSim.Instance != null ? BaseSim.Instance.FacilityAt(e.plot) : null) : e.t.name;
    static string PlotName(Entry e) => "P" + (e.plot + 1);

    void FillCard()
    {
        if (selected == null) return;
        var sim = BaseSim.Instance;
        var id = IdOf(selected);

        // ---- an empty plot: choose what to build here
        if (selected.plot >= 0 && id == null)
        {
            UIKit.Clear(cardButtons); lastSig = null;
            cardTitle.text = "Plot " + PlotName(selected);
            cardState.text = "Empty plot";
            cardState.color = UIKit.T.dim;
            cardBody.text = "Choose a facility to build here. Each facility can be built once, and it needs power from the Power Grid.";
            card.sizeDelta = new Vector2(236f, 122f);
            cardButtons.anchoredPosition = new Vector2(10f, -52f);
            cardButtons.sizeDelta = new Vector2(216f, 64f);
            int n = 0;
            foreach (var d in BaseData.Buildings)
            {
                if (!d.needsPlot) continue;
                var def2 = d;
                bool built = sim != null && sim.LevelOf(def2.id) > 0 || sim != null && sim.Job(def2.id) != null;
                string why = sim != null ? sim.WhyNotBuild(def2, selected.plot) : "-";
                bool ok = !built && why == null;
                string label = built ? def2.name + "  (built)" : def2.name + "  $" + BaseSim.CostOf(def2, 1).ToString("0.#", CultureInfo.InvariantCulture) + " M";
                float x = (n % 2) * 109f, y = (n / 2) * 20f;
                var b = UIKit.Btn(cardButtons, label, x, y, 107, 18, ok ? UIKit.T.green : UIKit.T.chip, () => { int plot = selected.plot; sim.StartBuild(def2, plot); }, 6.2f, true, ok ? (Color?)null : UIKit.T.dim);
                b.interactable = ok;
                if (!ok && !built) b.gameObject.AddComponent<TooltipTrigger>().text = why;
                n++;
            }
            return;
        }

        // ---- a facility (fixed or built on a plot)
        var def = BaseData.Building(id);
        int level = sim != null ? sim.LevelOf(id) : 1;
        cardTitle.text = (def != null ? def.name : id) + (selected.plot >= 0 ? "  ·  " + PlotName(selected) : "");
        cardState.text = level == 0 ? "Under construction" : "Level " + level + "  ·  " + BuildingDef.LevelNames[Mathf.Min(level, 3)];
        cardState.color = level == 0 ? new Color(1f, 0.75f, 0.4f, 1f) : new Color(0.62f, 1f, 0.76f, 1f);

        var sb = new System.Text.StringBuilder();
        sb.Append(def != null ? def.description : "");
        if (sim != null)
        {
            var job = sim.Job(id);
            if (job != null) sb.Append("\n<color=#FFB35C>" + (job.level <= 1 ? "Under construction" : "Upgrading to level " + job.level) + ", " + BaseSim.DaysLeft(job) + " days left</color>");
            foreach (var j in sim.RunningPrograms(id))
                sb.Append("\n<color=#FFB35C>Running: " + BaseData.Program(j.id).name + ", " + BaseSim.DaysLeft(j) + " days left</color>");
        }
        cardBody.text = sb.ToString();

        // The buttons are rebuilt only when what they show changes. (Rebuilding them every half second made them tremble and
        // swallowed clicks that landed between the rebuild and the release.)
        bool canUpgrade = def != null && sim != null && sim.Job(id) == null && level + 1 <= def.MaxLevel && def.MaxLevel > 1;
        string blockedBy = canUpgrade ? sim.WhyNotBuild(def) : null;
        string sig = id + "|" + level + "|" + canUpgrade + "|" + blockedBy + "|" + (sim != null && sim.Job(id) != null);
        if (sig == lastSig) return;
        lastSig = sig;
        UIKit.Clear(cardButtons);

        card.sizeDelta = new Vector2(236f, 94f);
        cardButtons.anchoredPosition = new Vector2(10f, -64f);
        cardButtons.sizeDelta = new Vector2(216f, 24f);
        const float total = 216f, gap = 4f, closeW = 24f, actW = 52f;
        var acts = new List<(string label, System.Action action)>();
        if (level > 0 && id != null && Actions.TryGetValue(id, out var list)) acts.AddRange(list);
        float upgW = total - closeW - gap - acts.Count * (actW + gap);
        float bx = 0f;

        if (canUpgrade)
        {
            int next = level + 1;
            bool ok = blockedBy == null;
            string cost = "$" + BaseSim.CostOf(def, next).ToString("0.#", CultureInfo.InvariantCulture) + " M  ·  " + Mathf.CeilToInt(BaseSim.DaysOf(def, next)) + " days";
            var rt = UIKit.At(cardButtons, "Upgrade", bx, 0, upgW, 24);
            var b = UIKit.MakeButton(rt, "", ok ? UIKit.T.green : UIKit.Wash(UIKit.T.orange, 0.30f), () => sim.StartBuild(def));
            // an arrow, the action, and under it the price and time (or why it cannot start yet)
            UIKit.IconAt(rt, Resources.Load<Sprite>("UIIcons/icon_upgrade"), 6, 5.5f, 12, 12, ok ? Color.white : new Color(1f, 0.78f, 0.45f, 1f)).raycastTarget = false;
            UIKit.TextAt(rt, "Label", 21, 2, upgW - 24, 11, "Upgrade to level " + next, 7f, TextAlignmentOptions.MidlineLeft, true, ok ? Color.white : new Color(1f, 0.85f, 0.6f, 1f)).raycastTarget = false;
            UIKit.TextAt(rt, "Sub", 21, 12.5f, upgW - 24, 9, ok ? cost : blockedBy, 5.6f, TextAlignmentOptions.MidlineLeft, false, ok ? new Color(1f, 1f, 1f, 0.8f) : new Color(1f, 0.72f, 0.4f, 1f)).raycastTarget = false;
            b.interactable = ok;
            var cb = b.colors; cb.disabledColor = Color.white; b.colors = cb;                         // blocked: still bright, so the reason is readable
            if (!ok) rt.gameObject.AddComponent<TooltipTrigger>().text = "Upgrade to level " + next + "\n" + cost + "\n" + blockedBy;
            bx += upgW + gap;
        }
        else bx += 0f;

        foreach (var (label, action) in acts)
        {
            var act = action;
            var rt = UIKit.At(cardButtons, label, bx, 0, canUpgrade ? actW : actW + 8f, 24);
            UIKit.MakeButton(rt, label, UIKit.T.teal, () => act(), 6.6f);
            bx += (canUpgrade ? actW : actW + 8f) + gap;
        }
        // close: a round button at the right end
        var close = UIKit.At(cardButtons, "Close", total - closeW, 0, closeW, 24);
        UIKit.MakeButton(close, "", UIKit.T.chip, Deselect);
        UIKit.IconAt(close, UIKit.T.closeIcon, 7, 7, 10, 10, UIKit.T.ink).raycastTarget = false;
    }

    string lastSig;

    // ---------------------------------------------------------------- picking
    static bool OverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    Entry Pick()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null || OverUi()) return null;
        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Entry best = null; float bestT = float.MaxValue;
        foreach (var e in entries)
        {
            if (e.t == null || !e.t.gameObject.activeInHierarchy) continue;
            var b = BoundsOf(e);
            if (b.size == Vector3.zero) continue;
            if (b.IntersectRay(ray, out float d) && d < bestT) { bestT = d; best = e; }
        }
        return best;
    }

    void SetTint(Entry e, float mul)
    {
        var r = Rend(e);
        if (r == null) return;
        if (mul <= 1.001f) { r.SetPropertyBlock(null); return; }
        e.mpb.SetColor("_BaseColor", new Color(e.baseColor.r * mul, e.baseColor.g * mul, e.baseColor.b * mul, e.baseColor.a));
        r.SetPropertyBlock(e.mpb);
    }

    void SetOutline(Entry e, bool on)
    {
        if (e == null) return;
        if (!on) { if (e.outlineGo != null) Destroy(e.outlineGo); e.outlineGo = null; return; }
        var r = Rend(e);
        if (e.outlineGo != null || outline == null || r == null) return;
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var go = new GameObject("Selection Outline");
        go.transform.SetParent(r.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = outline; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var b = mf.sharedMesh.bounds; Vector3 ext = b.extents, c = b.center;
        Vector3 ls = r.transform.lossyScale;
        float thick = Mathf.Clamp(b.size.magnitude * 0.004f, 0.18f, 0.7f);        // thicker for bigger buildings
        Vector3 scale = new Vector3(1f + thick / Mathf.Max(0.02f, ext.x * ls.x), 1f + thick / Mathf.Max(0.02f, ext.y * ls.y), 1f + thick / Mathf.Max(0.02f, ext.z * ls.z));
        go.transform.localScale = scale;
        go.transform.localPosition = c - Vector3.Scale(c, scale);
        e.outlineGo = go; e.outlined = r;
    }

    void Select(Entry e)
    {
        lastSig = null;                                    // a new selection builds its buttons from scratch
        // an empty plot opens the Facility menu with that plot already chosen
        if (e != null && e.plot >= 0 && IdOf(e) == null)
        {
            if (selected != null) { SetOutline(selected, false); SetTint(selected, 1f); selected = null; }
            card.gameObject.SetActive(false);
            FacilityPanelUI.OpenForPlot(e.plot);
            return;
        }
        if (selected == e) return;
        if (selected != null) { SetOutline(selected, false); SetTint(selected, 1f); }
        selected = e;
        if (e != null) { SetOutline(e, true); SetTint(e, 1.25f); }
        card.gameObject.SetActive(e != null);
        if (e != null) { card.SetAsLastSibling(); FillCard(); }
    }

    public void Deselect() => Select(null);

    void Update()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (PlotPicker.Active || UIConfirm.IsOpen) { if (selected != null) Deselect(); return; }          // the plot picker owns the map clicks
        if (kb != null && kb.escapeKey.wasPressedThisFrame && selected != null) Deselect();

        var over = Pick();
        if (over != hover)
        {
            if (hover != null && hover != selected) SetTint(hover, 1f);
            hover = over;
            if (hover != null && hover != selected) SetTint(hover, 1.22f);
        }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !OverUi())
            Select(hover);          // empty ground deselects

        if (selected != null && !selected.t.gameObject.activeInHierarchy) Deselect();

        cardTimer += Time.unscaledDeltaTime;
        if (selected != null && cardTimer > 0.5f) { cardTimer = 0f; FillCard(); }
    }

    void LateUpdate()
    {
        PlaceLabel(hoverLabel, hoverText, hover != null && hover != selected ? hover : null);
        PlaceLabel(selectLabel, selectText, selected);
    }

    /// <summary>Layer menu switch for the floating building names.</summary>
    public static bool LabelsVisible = true;

    void PlaceLabel(RectTransform label, TMP_Text text, Entry e)
    {
        if (!LabelsVisible || BuildingTags.Instance != null) e = null;          // the floating name tags already show every building's name
        if (e == null || cam == null || e.t == null) { if (label.gameObject.activeSelf) label.gameObject.SetActive(false); return; }
        var b = BoundsOf(e);
        Vector3 world = new Vector3(b.center.x, b.max.y + Mathf.Max(1.5f, b.size.y * 0.06f), b.center.z);          // floats above the roof
        Vector3 vp = cam.WorldToViewportPoint(world);
        if (vp.z <= 0f) { label.gameObject.SetActive(false); return; }
        var id = IdOf(e);
        var def = id != null ? BaseData.Building(id) : null;
        string n = e.plot >= 0 ? (id == null ? "Plot " + PlotName(e) + "  (empty)" : (def != null ? def.name : id) + "  ·  " + PlotName(e)) : (def != null ? def.name : e.t.name);
        if (text.text != n) { text.text = n; label.sizeDelta = new Vector2(text.GetPreferredValues(n).x + 14f, 13f); }
        label.anchoredPosition = new Vector2((vp.x - 0.5f) * canvasRect.rect.width, (vp.y - 0.5f) * canvasRect.rect.height) + new Vector2(0f, 3f);
        if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
    }
}
