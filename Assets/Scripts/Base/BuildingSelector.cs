using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hover and select the buildings of the base. The building under the mouse lights up and shows its name floating above it
/// (the name follows the building on screen). Clicking selects it: it keeps an outline and its name, and an info card
/// shows what it does, what is running there and shortcuts to the matching windows.
/// </summary>
public class BuildingSelector : MonoBehaviour
{
    public Material outline;                         // cyan inflated back-face material (from the PartLibrary)
    public RectTransform canvasRect;
    public Camera cam;

    sealed class Entry { public Transform t; public Renderer r; public Color baseColor; public MaterialPropertyBlock mpb = new MaterialPropertyBlock(); public GameObject outlineGo; }

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
        { "Hangar", new (string, System.Action)[] { ("Pilot training", () => Panels.Open("programs:0")), ("Structure test", () => Panels.Open("programs:1")) } },
        { "FuelDepot", new (string, System.Action)[] { ("Engine test", () => Panels.Open("programs:1")) } },
        { "RadarTower", new (string, System.Action)[] { ("Comms test", () => Panels.Open("programs:1")) } },
    };

    public void Init()
    {
        if (cam == null) cam = Camera.main;
        var root = GameObject.Find("Base Buildings");
        if (root != null)
            foreach (Transform c in root.transform)
            {
                var r = c.GetComponent<Renderer>() ?? c.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                Color col = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor") ? r.sharedMaterial.GetColor("_BaseColor") : Color.white;
                entries.Add(new Entry { t = c, r = r, baseColor = col });
            }
        hoverLabel = MakeLabel("Hover Label", out hoverText, false);
        selectLabel = MakeLabel("Selected Label", out selectText, true);
        BuildCard();
        hoverLabel.gameObject.SetActive(false); selectLabel.gameObject.SetActive(false); card.gameObject.SetActive(false);
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
        // a little pointer under the pill
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
        card.anchoredPosition = new Vector2(0f, 10f); card.sizeDelta = new Vector2(190f, 66f);
        UIKit.Glass(card);
        cardTitle = UIKit.TextAt(card, "Title", 10, 7, 120, 13, "", 9.5f, TextAlignmentOptions.MidlineLeft, true);
        cardState = UIKit.TextAt(card, "State", 90, 7, 90, 13, "", 6.5f, TextAlignmentOptions.MidlineRight, true);
        cardBody = UIKit.TextAt(card, "Body", 10, 22, 170, 20, "", 6.5f, TextAlignmentOptions.TopLeft, false, UIKit.T.dim);
        cardBody.textWrappingMode = TextWrappingModes.Normal; cardBody.lineSpacing = -6f;
        cardButtons = UIKit.At(card, "Buttons", 10, 44, 170, 16);
    }

    void FillCard()
    {
        if (selected == null) return;
        var id = selected.t.name;
        var def = BaseData.Building(id);
        var sim = BaseSim.Instance;
        cardTitle.text = def != null ? def.name : id;
        cardState.text = "Operational";
        cardState.color = new Color(0.62f, 1f, 0.76f, 1f);
        var sb = new System.Text.StringBuilder();
        sb.Append(def != null ? def.description : "");
        if (sim != null)
            foreach (var j in sim.RunningPrograms(id))
            {
                var p = BaseData.Program(j.id);
                sb.Append("\n<color=#FFB35C>Running: " + p.name + ", " + BaseSim.DaysLeft(j) + " days left</color>");
            }
        cardBody.text = sb.ToString();

        UIKit.Clear(cardButtons);
        if (Actions.TryGetValue(id, out var acts))
        {
            float x = 0f, w = Mathf.Min(82f, 170f / acts.Length - 3f);
            foreach (var (label, action) in acts)
            {
                var act = action;
                UIKit.Btn(cardButtons, label, x, 0, w, 16, UIKit.T.teal, () => act(), 7f);
                x += w + 4f;
            }
        }
        var close = UIKit.Btn(cardButtons, "Close", 170f - 40f, 0, 40, 16, UIKit.T.chip, Deselect, 7f);
    }

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
            if (e.r.bounds.IntersectRay(ray, out float d) && d < bestT) { bestT = d; best = e; }
        }
        return best;
    }

    void SetTint(Entry e, float mul)
    {
        if (e == null || e.r == null) return;
        if (mul <= 1.001f) { e.r.SetPropertyBlock(null); return; }
        e.mpb.SetColor("_BaseColor", new Color(e.baseColor.r * mul, e.baseColor.g * mul, e.baseColor.b * mul, e.baseColor.a));
        e.r.SetPropertyBlock(e.mpb);
    }

    void SetOutline(Entry e, bool on)
    {
        if (e == null) return;
        if (!on) { if (e.outlineGo != null) Destroy(e.outlineGo); e.outlineGo = null; return; }
        if (e.outlineGo != null || outline == null) return;
        var mf = e.r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var go = new GameObject("Selection Outline");
        go.transform.SetParent(e.r.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = outline; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var b = mf.sharedMesh.bounds; Vector3 ext = b.extents, c = b.center;
        float thick = 0.16f;
        Vector3 scale = new Vector3(1f + thick / Mathf.Max(0.02f, ext.x * e.r.transform.lossyScale.x), 1f + thick / Mathf.Max(0.02f, ext.y * e.r.transform.lossyScale.y), 1f + thick / Mathf.Max(0.02f, ext.z * e.r.transform.lossyScale.z));
        go.transform.localScale = scale;
        go.transform.localPosition = c - Vector3.Scale(c, scale);
        e.outlineGo = go;
    }

    void Select(Entry e)
    {
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

    void PlaceLabel(RectTransform label, TMP_Text text, Entry e)
    {
        if (e == null || cam == null || e.t == null) { if (label.gameObject.activeSelf) label.gameObject.SetActive(false); return; }
        var b = e.r.bounds;
        Vector3 world = new Vector3(b.center.x, b.max.y + 0.6f, b.center.z);          // floats above the roof
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) { label.gameObject.SetActive(false); return; }
        var def = BaseData.Building(e.t.name);
        string n = def != null ? def.name : e.t.name;
        if (text.text != n) { text.text = n; label.sizeDelta = new Vector2(text.GetPreferredValues(n).x + 14f, 13f); }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, canvasRect.GetComponentInParent<Canvas>().worldCamera, out var lp))
        {
            label.anchoredPosition = lp + new Vector2(0f, 3f);
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
        }
    }
}
