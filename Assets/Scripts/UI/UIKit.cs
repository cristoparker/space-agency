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
    public static Image Glass(RectTransform r)
    {
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = T.round; i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f;
        i.material = T.glass; i.color = T.dark;
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

    public static Button Btn(Transform parent, string label, float x, float y, float w, float h, Color tint, UnityAction click, float size = 7.5f, bool head = true)
    {
        var r = At(parent, label, x, y, w, h);
        return MakeButton(r, label, tint, click, size, head);
    }

    public static Button MakeButton(RectTransform r, string label, Color tint, UnityAction click, float size = 7.5f, bool head = true)
    {
        var img = Tint(r, tint);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        if (click != null) b.onClick.AddListener(click);
        StyleButton(b);
        if (!string.IsNullOrEmpty(label)) Text(r, label, size, TextAlignmentOptions.Center, head);
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
    /// <summary>Coloured pill title with a round close button, as in the reference layouts.</summary>
    public static TMP_Text TitleBar(Transform card, string title, Color accent, float width, UnityAction close)
    {
        var bar = At(card, "Title Bar", 6, 6, width - 12, 16);
        Tint(bar, accent);
        var t = Text(bar, title, 8f, TextAlignmentOptions.Center, true);
        if (close != null)
        {
            var x = At(bar, "Close", width - 12 - 15, 1, 14, 14);
            var b = MakeButton(x, "", new Color(1f, 1f, 1f, 0.9f), close);
            var ic = IconAt(x, T.closeIcon, 2, 2, 10, 10, new Color(0.1f, 0.15f, 0.25f, 1f));
            if (ic.sprite == null) Text(x, "x", 8f, TextAlignmentOptions.Center, true, new Color(0.1f, 0.15f, 0.25f, 1f));
        }
        return t;
    }

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
        var bg = Tint(r, T.chip);
        var inp = r.gameObject.AddComponent<TMP_InputField>();
        var area = At(r, "Text Area", 0, 0, w, h); Fill(area, 4, 0, 4, 0);
        area.gameObject.AddComponent<RectMask2D>();
        var txt = Text(area, "", size, TextAlignmentOptions.MidlineLeft);
        var ph = Text(area, hint, size, TextAlignmentOptions.MidlineLeft, false, T.dim); ph.fontStyle = FontStyles.Italic;
        inp.textViewport = area; inp.textComponent = txt; inp.placeholder = ph; inp.targetGraphic = bg;
        inp.characterLimit = limit; inp.lineType = TMP_InputField.LineType.SingleLine;
        return inp;
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
