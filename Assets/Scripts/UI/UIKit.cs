using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Small helpers that build glass-styled UI in code. All sizes are in canvas units (the canvas is 640 x 360).</summary>
public static class UIKit
{
    public static UITheme T => UITheme.Current;

    // ------------------------------------------------------------------ rects
    /// <summary>A child rect positioned from the parent's top-left corner (y grows downwards).</summary>
    public static RectTransform At(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    public static RectTransform Fill(RectTransform r, float l = 0, float t = 0, float rr = 0, float b = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
        return r;
    }

    /// <summary>Anchor a rect to the right edge of its parent (x measured inward from the right edge).</summary>
    public static RectTransform AnchorRight(RectTransform r, float xFromRight, float y)
    {
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-xFromRight, -y);
        return r;
    }

    // ------------------------------------------------------------------ surfaces
    /// <summary>Top-level glass panel (blurs the scene behind it). Use for the outermost card only.</summary>
    public static Image Glass(RectTransform r, bool shadow = true)
    {
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = T.round; i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f;
        i.material = T.glass; i.color = T.dark;
        if (shadow) UIShadow.Attach(r);
        return i;
    }

    /// <summary>Flat translucent rounded fill for anything inside a glass panel.</summary>
    public static Image Tint(RectTransform r, Color c)
    {
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = T.round; i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f;
        i.material = T.flat; i.color = c;
        return i;
    }

    public static RectTransform Card(Transform parent, string name, float x, float y, float w, float h, bool glass = true)
    {
        var r = At(parent, name, x, y, w, h);
        if (glass) Glass(r); else Tint(r, T.well);
        return r;
    }

    // ------------------------------------------------------------------ text
    public static TMP_Text Text(Transform parent, string s, float size, TextAlignmentOptions al = TextAlignmentOptions.Center, bool head = false, Color? color = null)
    {
        var r = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        Fill(r);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = head ? T.head : T.body;
        t.fontStyle = head ? FontStyles.Bold : FontStyles.Normal;
        t.fontSize = size; t.color = color ?? T.ink; t.alignment = al; t.text = s;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        return t;
    }

    public static TMP_Text TextAt(Transform parent, string name, float x, float y, float w, float h, string s, float size, TextAlignmentOptions al = TextAlignmentOptions.MidlineLeft, bool head = false, Color? color = null)
    {
        var r = At(parent, name, x, y, w, h);
        return Text(r, s, size, al, head, color);
    }

    // ------------------------------------------------------------------ buttons
    /// <summary>Hover brightens, press darkens, disabled dims. Works on translucent tinted fills.</summary>
    public static void StyleButton(Button b)
    {
        var cb = b.colors;
        cb.normalColor = Color.white; cb.selectedColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        cb.fadeDuration = 0.06f;
        b.colors = cb;
    }

    public static Button Btn(Transform parent, string label, float x, float y, float w, float h, Color tint, UnityAction click, float size = 7.5f, bool head = true, Color? labelColor = null)
    {
        var r = At(parent, label, x, y, w, h);
        return MakeButton(r, label, tint, click, size, head, labelColor);
    }

    /// <summary>A translucent wash of an accent colour: the fill of a secondary (tinted) button. Pair with the accent as label colour.</summary>
    public static Color Wash(Color accent, float a = 0.24f) => new Color(accent.r, accent.g, accent.b, a);

    public static Button MakeButton(RectTransform r, string label, Color tint, UnityAction click, float size = 7.5f, bool head = true, Color? labelColor = null)
    {
        var img = Tint(r, tint);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        if (click != null) b.onClick.AddListener(click);
        StyleButton(b);
        if (!string.IsNullOrEmpty(label)) Text(r, label, size, TextAlignmentOptions.Center, head, labelColor);
        return b;
    }

    public static Image IconAt(Transform parent, Sprite s, float x, float y, float w, float h, Color? color = null)
    {
        var r = At(parent, "Icon", x, y, w, h);
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = s; i.preserveAspect = true; i.raycastTarget = false; i.enabled = s != null;
        i.color = color ?? Color.white;
        return i;
    }

    // ------------------------------------------------------------------ title bar of a panel
    /// <summary>Panel title (bold, left) with a small accent capsule and a round close button, Apple style.</summary>
    public static TMP_Text TitleBar(Transform card, string title, Color accent, float width, UnityAction close)
    {
        var bar = At(card, "Title Bar", 10, 8, width - 20, 16);
        var acc = At(bar, "Accent", 0, 3, 3, 10);
        Tint(acc, accent);
        var tr = At(bar, "Title", 8, 0, width - 56, 16);
        var t = Text(tr, title, 9.5f, TextAlignmentOptions.MidlineLeft, true);
        if (close != null)
        {
            var x = At(bar, "Close", width - 20 - 14, 1, 14, 14);
            MakeButton(x, "", new Color(1f, 1f, 1f, 0.2f), close);
            var ic = IconAt(x, T.closeIcon, 3.5f, 3.5f, 7, 7, new Color(1f, 1f, 1f, 0.85f));
            if (ic.sprite == null) Text(x, "x", 8f, TextAlignmentOptions.Center, true);
        }
        return t;
    }

    /// <summary>The coloured capsule next to a title made by <see cref="TitleBar"/>.</summary>
    public static Image TitleAccent(TMP_Text title) => title.transform.parent.parent.Find("Accent").GetComponent<Image>();

    // ------------------------------------------------------------------ scroll areas
    /// <summary>A vertically scrolling list. Returns the content rect (add children with a LayoutElement height).</summary>
    public static RectTransform ScrollList(Transform parent, string name, float x, float y, float w, float h, Color? bg = null, float spacing = 2f, bool grid = false, Vector2? cell = null, int columns = 1)
    {
        var root = At(parent, name, x, y, w, h);
        if (bg.HasValue) Tint(root, bg.Value);
        var vp = At(root, "Viewport", 0, 0, w, h); Fill(vp, 2, 2, 2, 2);
        vp.gameObject.AddComponent<RectMask2D>();
        var content = At(vp, "Content", 0, 0, w - 4, 10);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
        if (grid)
        {
            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell ?? new Vector2(50, 70); g.spacing = new Vector2(spacing, spacing);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns;
            g.childAlignment = TextAnchor.UpperCenter;
        }
        else
        {
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(0, 0, 0, 0);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        }
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = root.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = content; sr.horizontal = false; sr.scrollSensitivity = 14f; sr.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) { var c = t.GetChild(i).gameObject; c.SetActive(false); Object.Destroy(c); }
    }

    // ------------------------------------------------------------------ input
    public static TMP_InputField Input(Transform parent, float x, float y, float w, float h, string hint, int limit = 20, float size = 8f)
    {
        var r = At(parent, "Input", x, y, w, h);
        var bg = Tint(r, T.track);
        var inp = r.gameObject.AddComponent<TMP_InputField>();
        var area = At(r, "Text Area", 0, 0, w, h); Fill(area, 4, 0, 4, 0);
        area.gameObject.AddComponent<RectMask2D>();
        var txt = Text(area, "", size, TextAlignmentOptions.MidlineLeft);
        var ph = Text(area, hint, size, TextAlignmentOptions.MidlineLeft, false, T.dim); ph.fontStyle = FontStyles.Italic;
        inp.textViewport = area; inp.textComponent = txt; inp.placeholder = ph; inp.targetGraphic = bg;
        inp.characterLimit = limit; inp.lineType = TMP_InputField.LineType.SingleLine;
        return inp;
    }

    static Sprite radial;
    /// <summary>White circle that fades smoothly to nothing at the edge (for background colour glows).</summary>
    public static Sprite RadialSprite()
    {
        if (radial != null) return radial;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt(Mathf.Pow((x + 0.5f) / n * 2f - 1f, 2) + Mathf.Pow((y + 0.5f) / n * 2f - 1f, 2));
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px); tex.Apply();
        radial = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return radial;
    }

    /// <summary>Money for the top bar: millions written with an M ("24,875M", "12.5M"); below a million just the amount.</summary>
    public static string MoneyM(long v)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (System.Math.Abs((double)v) >= 1e6) return (v / 1e6).ToString("#,0.#", inv) + "M";
        return v.ToString("N0", inv);
    }

    public static string Money(long v)
    {
        double a = System.Math.Abs((double)v);
        if (a >= 1e9) return (v / 1e9).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " B";
        if (a >= 1e6) return (v / 1e6).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " M";
        return v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Which big window is open. Opening one closes the others, so Contracts / Rocket / Payload never overlap.</summary>
public static class Panels
{
    static readonly System.Collections.Generic.Dictionary<string, System.Action> openers = new System.Collections.Generic.Dictionary<string, System.Action>();
    static readonly System.Collections.Generic.Dictionary<string, GameObject> roots = new System.Collections.Generic.Dictionary<string, GameObject>();

    public static void Register(string id, GameObject root, System.Action open)
    {
        openers[id] = open; roots[id] = root;
    }

    public static void Unregister(string id) { openers.Remove(id); roots.Remove(id); }

    public static void CloseAll()
    {
        foreach (var kv in roots) if (kv.Value != null) kv.Value.SetActive(false);
    }

    public static bool IsOpen(string id) => roots.TryGetValue(id, out var g) && g != null && g.activeSelf;

    public static bool Open(string id)
    {
        if (!openers.TryGetValue(id, out var act)) return false;
        CloseAll();
        act();
        return true;
    }

    public static void Toggle(string id)
    {
        if (IsOpen(id)) CloseAll(); else Open(id);
    }
}

/// <summary>A soft drop shadow drawn behind a glass panel (a sibling placed under it that follows its size and visibility).</summary>
public class UIShadow : MonoBehaviour
{
    static Sprite sprite;
    RectTransform me;
    public float spread = 14f, dropY = 4f;

    static Sprite Soft()
    {
        if (sprite != null) return sprite;
        const int n = 128; const float r = 30f, blur = 40f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        float half = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // signed distance to a rounded box, turned into a smooth falloff that reaches zero at the texture edge
                float dx = Mathf.Abs(x + 0.5f - half) - Mathf.Max(0f, half - blur - r), dy = Mathf.Abs(y + 0.5f - half) - Mathf.Max(0f, half - blur - r);
                float d = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
                float a = Mathf.Clamp01(0.5f - d / (blur * 1.6f));
                a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color(0f, 0f, 0f, a);
            }
        tex.SetPixels32(px); tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 46f, 0, SpriteMeshType.FullRect, new Vector4(56, 56, 56, 56));
        return sprite;
    }

    /// <summary>Put the shadow on a panel: the component lives on the panel, the shadow object is its sibling.</summary>
    public static void Attach(RectTransform panel)
    {
        if (panel.parent == null) return;
        var go = new GameObject(panel.name + " Shadow", typeof(RectTransform));
        go.transform.SetParent(panel.parent, false);
        go.transform.SetSiblingIndex(panel.GetSiblingIndex());
        var img = go.AddComponent<Image>();
        img.sprite = Soft(); img.type = Image.Type.Sliced; img.color = new Color(0f, 0f, 0f, 0.38f); img.raycastTarget = false;
        var s = panel.gameObject.AddComponent<UIShadow>();
        s.me = (RectTransform)go.transform;
        go.SetActive(panel.gameObject.activeInHierarchy);
        if (go.activeSelf) s.Sync();
    }

    void OnEnable() { if (me != null) { me.gameObject.SetActive(true); Sync(); } }
    void OnDisable() { if (me != null) me.gameObject.SetActive(false); }
    void OnDestroy() { if (me != null) Destroy(me.gameObject); }
    void LateUpdate() { if (me != null) Sync(); }

    void Sync()
    {
        var target = (RectTransform)transform;
        me.anchorMin = target.anchorMin; me.anchorMax = target.anchorMax; me.pivot = new Vector2(0.5f, 0.5f);
        var size = target.sizeDelta;
        me.sizeDelta = size + new Vector2(spread * 2f, spread * 2f);
        // centre of the panel, whatever its pivot, pushed down a little (light comes from above)
        me.anchoredPosition = target.anchoredPosition + new Vector2((0.5f - target.pivot.x) * size.x, (0.5f - target.pivot.y) * size.y - dropY);
        int want = Mathf.Max(0, target.GetSiblingIndex() - 1);          // just under the panel when it moves to the front
        if (me.GetSiblingIndex() != want) me.SetSiblingIndex(want);
    }
}

/// <summary>Apple style segmented control: a dark track with a lighter pill under the selected segment.</summary>
public class Segmented
{
    readonly Image[] pills;
    readonly TMP_Text[] labels;
    int sel = -1;
    readonly Color selColor;

    public Segmented(Transform parent, float x, float y, float w, float h, string[] names, int selected, System.Action<int> onChange, Color? selectedFill = null)
    {
        var T = UIKit.T;
        selColor = selectedFill ?? new Color(1f, 1f, 1f, 0.26f);
        var track = UIKit.At(parent, "Segmented", x, y, w, h);
        UIKit.Tint(track, T.track);
        pills = new Image[names.Length]; labels = new TMP_Text[names.Length];
        float seg = (w - 4f) / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var r = UIKit.At(track, names[i], 2 + i * seg, 2, seg, h - 4);
            var img = UIKit.Tint(r, Color.clear);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Select(idx); onChange?.Invoke(idx); });
            labels[i] = UIKit.Text(r, names[i], Mathf.Min(7f, h * 0.5f), TextAlignmentOptions.Center, true);
            pills[i] = img;
        }
        Select(selected);
    }

    public void Select(int i)
    {
        sel = i;
        for (int k = 0; k < pills.Length; k++)
        {
            pills[k].color = k == i ? selColor : Color.clear;
            labels[k].color = k == i ? UIKit.T.ink : UIKit.T.dim;
        }
    }

    public void SetLabel(int i, string s) { if (i >= 0 && i < labels.Length) labels[i].text = s; }
    public int Selected => sel;
}
