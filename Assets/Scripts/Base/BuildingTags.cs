using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The labels that float over the base: a glass name tag on every building (level badge first, then the name), a glass progress bar
/// under the tag of a building that is being built or upgraded, and a "completed" bubble when the work is done.
/// All of it lives in one container (the "Labels" layer): the Layer menu switches it, and it stays visible when the interface is hidden.
/// </summary>
public class BuildingTags : MonoBehaviour
{
    public static BuildingTags Instance { get; private set; }
    public static bool Visible = true;
    public static RectTransform Root => Instance != null ? Instance.root : null;

    class Tag
    {
        public string id;
        public Transform source;
        public RectTransform rt, bar, bubble;
        public TMP_Text level, pct;
        public RectTransform fill;
        public Bounds bounds; public float boundsAge = 99f;
        public float bubbleUntil;
        public float height;
        public bool showLevel;
    }

    RectTransform root, canvasRect;
    Canvas canvas;
    Camera cam;
    Sprite star, check;
    readonly Dictionary<string, Tag> tags = new Dictionary<string, Tag>();
    float rebuildTimer;

    public static BuildingTags Create(RectTransform canvasRt)
    {
        var go = new GameObject("World Tags", typeof(RectTransform));
        go.transform.SetParent(canvasRt, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling();                                           // behind every window
        var ui = go.AddComponent<BuildingTags>();
        ui.root = rt; ui.canvasRect = canvasRt; ui.canvas = canvasRt.GetComponent<Canvas>();
        ui.star = Resources.Load<Sprite>("UIIcons/icon_star"); ui.check = Resources.Load<Sprite>("UIIcons/icon_check");
        Instance = ui;
        return ui;
    }

    void OnEnable()
    {
        if (BaseSim.Instance != null) { BaseSim.Instance.Changed += Rebuild; BaseSim.Instance.BuildFinished += OnFinished; }
        Rebuild();
    }

    void OnDisable()
    {
        if (BaseSim.Instance != null) { BaseSim.Instance.Changed -= Rebuild; BaseSim.Instance.BuildFinished -= OnFinished; }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ---------------------------------------------------------------- which buildings have a tag
    void Rebuild()
    {
        var sim = BaseSim.Instance;
        if (sim == null || root == null) return;
        var want = new Dictionary<string, Transform>();
        var fixedRoot = GameObject.Find("Base Buildings");
        if (fixedRoot != null) foreach (Transform c in fixedRoot.transform) want[c.name] = c;
        var plots = GameObject.Find("Base Plots");
        if (plots != null)
            for (int i = 0; i < plots.transform.childCount; i++)
            {
                string id = sim.FacilityAt(i);
                if (id != null) want[id] = plots.transform.GetChild(i);
            }
        var gone = new List<string>();
        foreach (var kv in tags) if (!want.ContainsKey(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone) { Destroy(tags[id].rt.gameObject); if (tags[id].bar) Destroy(tags[id].bar.gameObject); if (tags[id].bubble) Destroy(tags[id].bubble.gameObject); tags.Remove(id); }
        foreach (var kv in want)
        {
            if (tags.TryGetValue(kv.Key, out var t)) { t.source = kv.Value; continue; }
            tags[kv.Key] = MakeTag(kv.Key, kv.Value);
        }
    }

    // plain text, no glass: it sits on the real place and stays readable through a dark outline
    static TMP_Text Outlined(TMP_Text t)
    {
        t.outlineWidth = 0.3f; t.outlineColor = new Color32(0, 0, 0, 255);
        t.raycastTarget = false;
        return t;
    }

    static readonly Color OvalBlack = new Color(0f, 0f, 0f, 0.5f);

    Tag MakeTag(string id, Transform source)
    {
        var T = UIKit.T;
        var def = BaseData.Building(id);
        bool showLevel = def != null && def.MaxLevel > 1;             // a building that can never be upgraded (car park, housing...) has no level
        var t = new Tag { id = id, source = source, height = 12f, showLevel = showLevel };
        string nameStr = BaseData.BuildingName(id);
        var orange = new Color(1f, 0.72f, 0.24f, 1f);

        t.rt = NewRect("Tag " + id, 60f, t.height);
        UIKit.Tint(t.rt, OvalBlack).raycastTarget = false;            // a soft black oval behind the text keeps it readable on any ground
        float x = 6f;
        if (showLevel)
        {
            UIKit.IconAt(t.rt, star, x, 2.2f, 7.5f, 7.5f, orange).raycastTarget = false;
            t.level = Outlined(UIKit.TextAt(t.rt, "Lv", x + 8.5f, 0, 9, t.height, "1", 6.6f, TextAlignmentOptions.MidlineLeft, true, orange));
            x += 18f;
        }
        var nm = Outlined(UIKit.TextAt(t.rt, "Name", x, 0, 200, t.height, nameStr, 6.8f, TextAlignmentOptions.MidlineLeft, true, Color.white));
        float w = Mathf.Min(140f, x + nm.GetPreferredValues(nameStr).x + 7f);
        t.rt.sizeDelta = new Vector2(w, t.height);
        ((RectTransform)nm.transform.parent).sizeDelta = new Vector2(w - x - 3f, t.height);

        // progress bar under the tag while the building is being built or upgraded
        t.bar = NewRect("Bar " + id, 70f, 11f);
        UIKit.Tint(t.bar, OvalBlack).raycastTarget = false;
        var track = UIKit.At(t.bar, "Track", 6, 3.8f, 40, 3.4f);
        UIKit.Tint(track, new Color(1f, 1f, 1f, 0.22f)).raycastTarget = false;
        t.fill = UIKit.At(track, "Fill", 0, 0, 10, 3.4f);
        t.fill.anchorMin = Vector2.zero; t.fill.anchorMax = new Vector2(0f, 1f); t.fill.offsetMin = t.fill.offsetMax = Vector2.zero;
        UIKit.Tint(t.fill, orange).raycastTarget = false;
        t.pct = Outlined(UIKit.TextAt(t.bar, "Pct", 49, 0, 18, 11, "0%", 5.8f, TextAlignmentOptions.MidlineLeft, true, Color.white));
        t.bar.gameObject.SetActive(false);
        return t;
    }

    RectTransform NewRect(string name, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(root, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    // ---------------------------------------------------------------- "completed" bubble
    void OnFinished(string id, int level)
    {
        if (!tags.TryGetValue(id, out var t)) { Rebuild(); if (!tags.TryGetValue(id, out t)) return; }
        if (t.bubble != null) Destroy(t.bubble.gameObject);
        string msg = level <= 1 ? "Your construction is completed!" : "Your upgrade is completed!";
        var T = UIKit.T;
        var b = NewRect("Done " + id, 134f, 16f);
        b.pivot = new Vector2(0.5f, 0f);
        var hit = UIKit.Tint(b, OvalBlack);                          // the oval itself is the click area: click to dismiss
        var ring = UIKit.At(b, "Icon", 3, 2f, 12, 12);
        UIKit.Tint(ring, T.green).raycastTarget = false;
        UIKit.IconAt(ring, check, 2.2f, 2.2f, 7.6f, 7.6f, new Color(0.04f, 0.16f, 0.08f, 1f));
        Outlined(UIKit.TextAt(b, "Text", 18, 0, 114, 16, msg, 6.6f, TextAlignmentOptions.MidlineLeft, true, new Color(0.7f, 1f, 0.8f, 1f)));
        var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = hit; btn.transition = Selectable.Transition.None;
        var captured = t;
        btn.onClick.AddListener(() => { if (captured.bubble != null) { Destroy(captured.bubble.gameObject); captured.bubble = null; } });
        b.gameObject.AddComponent<CanvasGroup>(); b.gameObject.AddComponent<UIPopIn>();
        t.bubble = b; t.bubbleUntil = float.MaxValue;               // stays on the building until it is clicked
    }

    // ---------------------------------------------------------------- every frame: follow the buildings
    static Bounds BoundsOf(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        Bounds b = default; bool any = false;
        foreach (var r in rs)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any ? b : new Bounds(t.position, Vector3.one * 10f);
    }

    void LateUpdate()
    {
        if (root == null) return;
        if (!Visible)
        {
            // the container itself must stay active (this script lives on it, so a hidden container could never come back): hide what is inside
            foreach (var h in tags.Values) { h.rt.gameObject.SetActive(false); h.bar.gameObject.SetActive(false); if (h.bubble) h.bubble.gameObject.SetActive(false); }
            return;
        }
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        var sim = BaseSim.Instance;
        float dt = Time.unscaledDeltaTime;
        rebuildTimer += dt;
        if (rebuildTimer > 2f) { rebuildTimer = 0f; Rebuild(); }

        foreach (var t in tags.Values)
        {
            if (t.source == null || !t.source.gameObject.activeInHierarchy) { t.rt.gameObject.SetActive(false); t.bar.gameObject.SetActive(false); if (t.bubble) t.bubble.gameObject.SetActive(false); continue; }
            t.boundsAge += dt;
            if (t.boundsAge > 0.7f) { t.bounds = BoundsOf(t.source); t.boundsAge = 0f; }
            var b = t.bounds;
            var world = new Vector3(b.center.x, Mathf.Min(b.max.y, b.min.y + 34f) + 3f, b.center.z);
            // viewport -> canvas units directly. (Converting through the canvas transform lags a frame behind the moving camera,
            // which made the labels slide around while zooming.)
            Vector3 vp = cam.WorldToViewportPoint(world);
            bool vis = vp.z > 0f;
            t.rt.gameObject.SetActive(vis);
            if (!vis) { t.bar.gameObject.SetActive(false); if (t.bubble) t.bubble.gameObject.SetActive(false); continue; }
            var rect = canvasRect.rect;
            var lp = new Vector2((vp.x - 0.5f) * rect.width, (vp.y - 0.5f) * rect.height);
            t.rt.anchoredPosition = lp;

            int level = sim != null ? sim.LevelOf(t.id) : 1;
            if (t.level != null) t.level.text = level > 0 ? level.ToString() : "-";

            var job = sim != null ? sim.Job(t.id) : null;
            bool building = job != null && job.isBuild;
            t.bar.gameObject.SetActive(building);
            if (building)
            {
                float p = BaseSim.Progress(job);
                t.fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
                t.pct.text = Mathf.RoundToInt(p * 100f) + "%";
                t.bar.anchoredPosition = lp + new Vector2(0f, -t.bar.sizeDelta.y - 1f);
            }
            if (t.bubble != null)
            {
                if (Time.unscaledTime > t.bubbleUntil) { Destroy(t.bubble.gameObject); t.bubble = null; }
                else
                {
                    t.bubble.gameObject.SetActive(true);
                    float bob = Mathf.Sin(Time.unscaledTime * 2.6f) * 1.6f;                  // floats up and down
                    t.bubble.anchoredPosition = lp + new Vector2(0f, t.height + 3f + bob);
                }
            }
        }
    }
}
