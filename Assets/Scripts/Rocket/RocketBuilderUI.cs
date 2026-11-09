using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Rocket Builder screen, laid out like a space-flight-simulator editor:
/// icon sidebar (hover = name) -> part flyout, full-screen 3D view (click a part to select, outline + highlight),
/// slim right panel (drag-to-reorder hierarchy, stats, contract, saved rockets) and a top bar with
/// Undo / Redo / Delete / Auto order / Save / Deploy. Everything is created in code the first time it opens.
/// </summary>
public class RocketBuilderUI : MonoBehaviour
{
    [Header("Data")]
    public PartLibrary library;

    [Header("Style")]
    public Material flatMat;
    public Sprite roundSprite;
    public TMP_FontAsset bodyFont, headFont;
    public Color normalColor = new Color(0.17f, 0.22f, 0.31f, 1f);
    public Color activeColor = new Color(0.13f, 0.5f, 0.72f, 1f);

    static readonly (string label, string[] cats, string icon)[] Tabs =
    {
        ("Engines", new[] { "Engine" }, "Eng_M_Lifter"), ("Thrusters", new[] { "Thruster" }, "Thr_RCS_Block"), ("Fuel tanks", new[] { "Tank" }, "Tank_M_Short"),
        ("Rocket heads", new[] { "Head" }, "Head_Fairing_M"), ("Side boosters", new[] { "Booster" }, "Booster_M"),
        ("Payloads", new[] { "Payload" }, "Pay_Satellite_M"), ("Communication", new[] { "Comms" }, "Com_Dish_S"),
        ("Power", new[] { "Power" }, "Pow_Solar_S"), ("Landing & recovery", new[] { "Recovery" }, "Rec_Parachute_M"),
        ("Structure & control", new[] { "Structure" }, "Dec_M"),
    };

    static readonly string[] RightTabs = { "Build", "Stats", "Mission", "Saved" };

    const string SaveKey = "SpaceAgency.RocketDesign", NameKey = "SpaceAgency.RocketName";
    static readonly Color Ink = new Color(0.92f, 0.95f, 1f, 1f), Dim = new Color(0.92f, 0.95f, 1f, 0.5f);
    static readonly Color Bar = new Color(0.05f, 0.07f, 0.11f, 1f);
    static readonly Color Well = new Color(0.085f, 0.115f, 0.17f, 1f), RowColor = new Color(0.13f, 0.17f, 0.25f, 1f), AttachColor = new Color(0.1f, 0.24f, 0.33f, 1f);
    static readonly Color Ready = new Color(0.16f, 0.5f, 0.36f, 1f);
    const string Good = "#8FE3B0", Warn = "#FFB878", Bad = "#FF8C8C", Soft = "#9FB4C8", Cyan = "#7FDBFF";
    const int LB = 34, RB = 134;

    // ---------------- state ----------------
    RocketDesign design = new RocketDesign();
    RocketPreview preview;
    RocketReport report;
    Sel sel = Sel.None;
    int openTab = -1, rightTab;
    bool built;
    float W = 640f, H = 360f;
    float toastTimer;
    readonly List<string> history = new List<string>();
    int hIdx = -1;
    readonly List<MissionDefinition> missions = new List<MissionDefinition> { null };
    int missionIndex;

    // ---------------- ui refs ----------------
    readonly List<Image> tabImages = new List<Image>();
    readonly List<Image> rightTabImages = new List<Image>();
    GameObject flyout, selCard, deployDialog;
    GameObject[] rightViews;
    Transform flyList, hierList, savedList;
    TMP_Text flyTitle, statsText, checksText, missionLabel, summaryText, toastText, cardText, deployInfo, deployDate, monthLabel;
    TMP_InputField nameInput;
    Button undoBtn, redoBtn, deleteBtn, deployBtn;
    readonly List<Button> dayButtons = new List<Button>();
    DateTime viewMonth, minDate;
    DateTime? pickDate;

    MissionDefinition Mission => missionIndex > 0 && missionIndex < missions.Count ? missions[missionIndex] : null;

    // =============================================================== lifecycle
    void OnEnable()
    {
        if (library == null) { Debug.LogWarning("RocketBuilderUI: no PartLibrary assigned."); return; }
        Canvas.ForceUpdateCanvases();
        if (!built) Build();
        transform.SetAsLastSibling();
        design = RocketDesign.FromJson(PlayerPrefs.GetString(SaveKey, ""));
        design.stack.RemoveAll(id => library.Get(id) == null);
        history.Clear(); hIdx = -1; Commit();
        sel = Sel.None; openTab = -1; rightTab = 0;
        nameInput.text = PlayerPrefs.GetString(NameKey, "");
        deployDialog.SetActive(false);
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

    void Update()
    {
        if (!built) return;
        if (toastTimer > 0f) { toastTimer -= Time.unscaledDeltaTime; if (toastTimer <= 0f) toastText.text = ""; }
        var kb = Keyboard.current;
        if (kb == null || nameInput.isFocused || deployDialog.activeSelf) return;
        bool ctrl = kb.ctrlKey.isPressed;
        if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) DeleteSelected();
        if (ctrl && kb.zKey.wasPressedThisFrame) Undo();
        if (ctrl && kb.yKey.wasPressedThisFrame) Redo();
    }

    // =============================================================== ui helpers
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

    static Sprite roundedSprite;

    // A plain white rounded rectangle (9-sliced), drawn with the normal UI shader: flat colours, no blur or glass.
    static Sprite Rounded()
    {
        if (roundedSprite != null) return roundedSprite;
        const int n = 32; const float r = 10f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (n - r), 0f), dy = Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (n - r), 0f);
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px); tex.Apply();
        roundedSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 150f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return roundedSprite;
    }

    Image Img(RectTransform r, Color c)
    {
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = Rounded(); i.type = Image.Type.Sliced; i.color = c;
        return i;
    }

    // Hover brightens, press darkens, disabled dims. Works on flat colours.
    static void Style(Button b)
    {
        var cb = b.colors;
        cb.normalColor = Color.white; cb.selectedColor = Color.white;
        cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        cb.fadeDuration = 0.06f;
        b.colors = cb;
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
        Style(b);
        Txt(r, label, size, TextAlignmentOptions.Center);
        return b;
    }

    static void Tip(GameObject go, string text) => go.AddComponent<TooltipTrigger>().text = text;

    Image Icon(Transform parent, string id, float x, float y, float w, float h)
    {
        var r = Rt(parent, "Icon", x, y, w, h);
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = library.Icon(id); i.preserveAspect = true; i.raycastTarget = false;
        i.enabled = i.sprite != null;
        return i;
    }

    Transform ScrollList(Transform parent, string name, float x, float y, float w, float h, Color bg)
    {
        var root = Rt(parent, name, x, y, w, h);
        Img(root, bg);
        var vp = Rt(root, "Viewport", 0, 0, w, h); Stretch(vp, 1, 1, 1, 1);
        vp.gameObject.AddComponent<RectMask2D>();
        var content = Rt(vp, "Content", 0, 0, w - 2, 10);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 2; v.padding = new RectOffset(1, 1, 1, 1);
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = root.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = content; sr.horizontal = false; sr.scrollSensitivity = 14f; sr.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    TMP_Text ScrollText(Transform parent, string name, float x, float y, float w, float h)
    {
        var root = Rt(parent, name, x, y, w, h);
        Img(root, Well);
        var vp = Rt(root, "Viewport", 0, 0, w, h); Stretch(vp, 1, 1, 1, 1);
        vp.gameObject.AddComponent<RectMask2D>();
        var t = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        t.SetParent(vp, false);
        t.anchorMin = new Vector2(0, 1); t.anchorMax = new Vector2(1, 1); t.pivot = new Vector2(0.5f, 1f);
        t.offsetMin = new Vector2(3, 0); t.offsetMax = new Vector2(-3, 0);
        var tm = t.gameObject.AddComponent<TextMeshProUGUI>();
        tm.font = bodyFont; tm.fontSize = 7.5f; tm.color = Ink; tm.alignment = TextAlignmentOptions.TopLeft;
        tm.textWrappingMode = TextWrappingModes.Normal; tm.raycastTarget = false; tm.lineSpacing = -5f;
        t.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = root.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = t; sr.horizontal = false; sr.scrollSensitivity = 14f; sr.movementType = ScrollRect.MovementType.Clamped;
        return tm;
    }

    TMP_InputField Input(Transform parent, float x, float y, float w, float h, string hint)
    {
        var r = Rt(parent, "Name", x, y, w, h);
        var bg = Img(r, normalColor);
        var inp = r.gameObject.AddComponent<TMP_InputField>();
        var area = Rt(r, "Text Area", 0, 0, w, h); Stretch(area, 4, 0, 4, 0);
        area.gameObject.AddComponent<RectMask2D>();
        var txt = Txt(area, "", 8f, TextAlignmentOptions.MidlineLeft);
        var ph = Txt(area, hint, 8f, TextAlignmentOptions.MidlineLeft); ph.color = Dim; ph.fontStyle = FontStyles.Italic;
        inp.textViewport = area; inp.textComponent = txt; inp.placeholder = ph; inp.targetGraphic = bg;
        inp.characterLimit = 18; inp.lineType = TMP_InputField.LineType.SingleLine;
        return inp;
    }

    // =============================================================== layout
    void Build()
    {
        var root = (RectTransform)transform;
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        W = root.rect.width > 10 ? root.rect.width : 640f;
        H = root.rect.height > 10 ? root.rect.height : 360f;

        // ---- full-screen 3D view ----
        int rtW = Mathf.Clamp(Screen.width, 1280, 2560);
        preview = RocketPreview.Create(rtW, Mathf.RoundToInt(rtW * H / W));          // as many pixels as the screen: stays sharp when zoomed out
        var view = Rt(root, "View", 0, 0, W, H); Stretch(view);
        var raw = view.gameObject.AddComponent<RawImage>(); raw.texture = preview.Texture;
        var pin = view.gameObject.AddComponent<RocketPreviewInput>(); pin.preview = preview;
        pin.picked = pr => { sel = pr == null ? Sel.None : Sel.Of(pr.kind, pr.index); RefreshAll(); };
        var hint = Txt(Rt(root, "Hint", LB + 114, H - 14, W - LB - RB - 120, 12), "drag: rotate    right-drag: move view    scroll: zoom    click a part: select", 7f, TextAlignmentOptions.Center);
        hint.color = Dim;
        float cx = LB + 114 + (W - LB - RB - 120) / 2f;
        Image rotImg = null, panImg = null;
        var bRot = Btn(root, "Rotate", cx - 80, 30, 50, 14, () => { pin.panMode = false; rotImg.color = activeColor; panImg.color = normalColor; }, 7.5f);
        var bPan = Btn(root, "Move", cx - 28, 30, 50, 14, () => { pin.panMode = true; rotImg.color = normalColor; panImg.color = activeColor; }, 7.5f);
        Btn(root, "Reset view", cx + 24, 30, 58, 14, () => preview.ResetView(), 7.5f);
        rotImg = (Image)bRot.targetGraphic; panImg = (Image)bPan.targetGraphic; rotImg.color = activeColor;
        Tip(bRot.gameObject, "Drag the view to rotate around the rocket");
        Tip(bPan.gameObject, "Drag the view to move it anywhere.\nRight or middle mouse button does this too.");

        BuildTopBar(root);
        BuildLeftBar(root);
        BuildFlyout(root);
        BuildRightBar(root);
        BuildBottom(root);
        BuildDeployDialog(root);
        built = true;
    }

    void BuildTopBar(RectTransform root)
    {
        var top = Rt(root, "TopBar", 0, 0, W, 26);
        Img(top, Bar);
        var title = Txt(Rt(top, "Title", 38, 0, 110, 26), "Rocket Builder", 9f, TextAlignmentOptions.MidlineLeft, true);
        title.color = Ink;
        undoBtn = Btn(top, "Undo", 152, 5, 34, 16, Undo);
        redoBtn = Btn(top, "Redo", 188, 5, 34, 16, Redo);
        deleteBtn = Btn(top, "Delete", 226, 5, 40, 16, DeleteSelected);
        var auto = Btn(top, "Auto order", 270, 5, 58, 16, AutoOrderClicked);
        Tip(auto.gameObject, "Fix the part order automatically\n<color=" + Soft + ">stages, decouplers, adapters, head on top</color>");
        summaryText = Txt(Rt(top, "Summary", 334, 0, 118, 26), "", 7.5f, TextAlignmentOptions.MidlineLeft);
        nameInput = Input(top, W - 186, 5, 76, 16, "Rocket name");
        Btn(top, "Save", W - 108, 5, 36, 16, SaveRocket);
        deployBtn = Btn(top, "Deploy", W - 70, 5, 46, 16, OpenDeploy);
        Btn(top, "x", W - 22, 5, 18, 16, () => gameObject.SetActive(false));
    }

    void BuildLeftBar(RectTransform root)
    {
        var bar = Rt(root, "LeftBar", 0, 26, LB, H - 26);
        Img(bar, Bar);
        tabImages.Clear();
        for (int i = 0; i < Tabs.Length; i++)
        {
            int idx = i;
            var r = Rt(bar, Tabs[i].label, 5, 5 + i * 27, 24, 24);
            var img = Img(r, normalColor);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img; Style(b);
            b.onClick.AddListener(() => { openTab = openTab == idx ? -1 : idx; RefreshAll(); });
            Icon(r, Tabs[i].icon, 2, 2, 20, 20);
            Tip(r.gameObject, Tabs[i].label);
            tabImages.Add(img);
        }
    }

    void BuildFlyout(RectTransform root)
    {
        var f = Rt(root, "Flyout", LB + 2, 26, 108, H - 26);
        Img(f, Bar);
        flyTitle = Txt(Rt(f, "Title", 0, 0, 108, 16), "", 8f, TextAlignmentOptions.Center);
        flyTitle.color = new Color(0.5f, 0.86f, 1f, 1f);
        flyList = ScrollList(f, "Parts", 2, 17, 104, H - 26 - 20, Well);
        flyout = f.gameObject;
    }

    void BuildRightBar(RectTransform root)
    {
        float x0 = W - RB, vw = RB - 8, vh = H - 26 - 22 - 4;
        var bar = Rt(root, "RightBar", x0, 26, RB, H - 26);
        Img(bar, Bar);
        rightTabImages.Clear();
        float tw = (RB - 8 - 3 * 2) / 4f;
        for (int i = 0; i < RightTabs.Length; i++)
        {
            int idx = i;
            var b = Btn(bar, RightTabs[i], 4 + i * (tw + 2), 3, tw, 14, () => { rightTab = idx; RefreshAll(); }, 7f);
            rightTabImages.Add(b.targetGraphic as Image);
        }
        rightViews = new GameObject[4];
        var vBuild = Rt(bar, "BuildView", 4, 20, vw, vh); hierList = ScrollList(vBuild, "Hierarchy", 0, 0, vw, vh, Well);
        var vStats = Rt(bar, "StatsView", 4, 20, vw, vh); statsText = ScrollText(vStats, "Stats", 0, 0, vw, vh);
        var vMission = Rt(bar, "MissionView", 4, 20, vw, vh);
        Btn(vMission, "<", 0, 0, 14, 14, () => ChangeMission(-1));
        missionLabel = Txt(Rt(vMission, "Contract", 16, 0, vw - 32, 14), "", 7.5f, TextAlignmentOptions.Midline);
        Btn(vMission, ">", vw - 14, 0, 14, 14, () => ChangeMission(1));
        checksText = ScrollText(vMission, "Checks", 0, 17, vw, vh - 17);
        var vSaved = Rt(bar, "SavedView", 4, 20, vw, vh); savedList = ScrollList(vSaved, "Saved", 0, 0, vw, vh, Well);
        rightViews[0] = vBuild.gameObject; rightViews[1] = vStats.gameObject; rightViews[2] = vMission.gameObject; rightViews[3] = vSaved.gameObject;
    }

    void BuildBottom(RectTransform root)
    {
        // short message line + selected-part card
        toastText = Txt(Rt(root, "Toast", LB + 114, H - 52, W - LB - RB - 120, 14), "", 8f, TextAlignmentOptions.Center);
        var card = Rt(root, "SelectedCard", LB + 114, H - 38, W - LB - RB - 120, 32);
        Img(card, Bar);
        var info = Rt(card, "Info", 0, 0, 10, 10);
        Stretch(info, 6, 0, 52, 0);
        cardText = Txt(info, "", 7.5f, TextAlignmentOptions.MidlineLeft);
        cardText.textWrappingMode = TextWrappingModes.Normal; cardText.lineSpacing = -5f;
        var del = Btn(card, "Delete", 0, 8, 44, 16, DeleteSelected);
        var dr = (RectTransform)del.transform; dr.anchorMin = dr.anchorMax = new Vector2(1, 1); dr.pivot = new Vector2(1, 1); dr.anchoredPosition = new Vector2(-4, -8);
        selCard = card.gameObject;
    }

    void BuildDeployDialog(RectTransform root)
    {
        var dim = Rt(root, "DeployDialog", 0, 0, W, H); Stretch(dim);
        Img(dim, new Color(0, 0, 0, 0.62f));
        deployDialog = dim.gameObject;
        float bw = 232, bh = 214, bx = (W - bw) / 2, by = (H - bh) / 2;
        var box = Rt(dim, "Box", bx, by, bw, bh);
        Img(box, new Color(0.07f, 0.1f, 0.15f, 1f));
        var title = Txt(Rt(box, "Title", 0, 4, bw, 16), "Deploy to the launch pad", 9f, TextAlignmentOptions.Center, true);
        deployInfo = Txt(Rt(box, "Info", 10, 22, bw - 20, 24), "", 7.5f, TextAlignmentOptions.TopLeft);
        deployInfo.textWrappingMode = TextWrappingModes.Normal; deployInfo.lineSpacing = -5f;
        Btn(box, "<", 12, 50, 16, 14, () => ShiftMonth(-1));
        monthLabel = Txt(Rt(box, "Month", 30, 50, bw - 60, 14), "", 8f, TextAlignmentOptions.Midline);
        Btn(box, ">", bw - 28, 50, 16, 14, () => ShiftMonth(1));
        string[] wd = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        float gx = (bw - 7 * 28) / 2;
        for (int i = 0; i < 7; i++) { var t = Txt(Rt(box, "WD" + i, gx + i * 28, 67, 28, 11), wd[i], 7f, TextAlignmentOptions.Center); t.color = Dim; }
        dayButtons.Clear();
        for (int i = 0; i < 42; i++)
        {
            int idx = i;
            var b = Btn(box, "Day" + i, gx + (i % 7) * 28 + 1, 79 + (i / 7) * 15, 26, 13, () => PickDay(idx), 7.5f);
            dayButtons.Add(b);
        }
        deployDate = Txt(Rt(box, "Picked", 0, 172, bw, 12), "", 8f, TextAlignmentOptions.Center);
        Btn(box, "Confirm deploy", 12, 190, 104, 16, ConfirmDeploy);
        Btn(box, "Cancel", bw - 116, 190, 104, 16, () => deployDialog.SetActive(false));
    }

    // =============================================================== history (undo / redo)
    void Commit()
    {
        string j = design.ToJson();
        if (hIdx >= 0 && history[hIdx] == j) return;
        if (hIdx < history.Count - 1) history.RemoveRange(hIdx + 1, history.Count - hIdx - 1);
        history.Add(j);
        if (history.Count > 100) history.RemoveAt(0);
        hIdx = history.Count - 1;
    }

    void Changed() { Commit(); RefreshAll(); }

    void Undo()
    {
        if (hIdx <= 0) return;
        hIdx--; design = RocketDesign.FromJson(history[hIdx]); sel = Sel.None; RefreshAll(); Toast("Undo");
    }

    void Redo()
    {
        if (hIdx >= history.Count - 1) return;
        hIdx++; design = RocketDesign.FromJson(history[hIdx]); sel = Sel.None; RefreshAll(); Toast("Redo");
    }

    // =============================================================== actions
    void Toast(string msg, bool bad = false)
    {
        toastText.text = bad ? "<color=" + Bad + ">" + msg + "</color>" : msg;
        toastTimer = 3.5f;
    }

    void AddPart(PartInfo p)
    {
        RocketRules.CanAdd(design, p, library, out var note);
        sel = RocketRules.Add(design, p, library);
        Changed();
        if (!string.IsNullOrEmpty(note)) Toast(note, true);
    }

    void DeleteSelected()
    {
        if (sel.IsNone) return;
        switch (sel.kind)
        {
            case SelKind.Stack: if (sel.index >= 0 && sel.index < design.stack.Count) design.stack.RemoveAt(sel.index); break;
            case SelKind.Booster: design.boosterId = null; break;
            case SelKind.Fin: design.finId = null; break;
            case SelKind.Payload: design.payloadId = null; break;
            case SelKind.Equip: if (sel.index >= 0 && sel.index < design.equipment.Count) design.equipment.RemoveAt(sel.index); break;
        }
        sel = Sel.None;
        Changed();
        Toast("Part deleted");
    }

    void AutoOrderClicked()
    {
        string msg = RocketRules.AutoOrder(design, library);
        sel = Sel.None;
        Changed();
        Toast(msg);
    }

    public void MoveStack(int from, int toVisual)
    {
        int n = design.stack.Count;
        if (from < 0 || from >= n) return;
        string id = design.stack[from];
        design.stack.RemoveAt(from);
        int target = Mathf.Clamp(n - 1 - toVisual, 0, design.stack.Count);
        design.stack.Insert(target, id);
        sel = Sel.Of(SelKind.Stack, target);
        Changed();
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
        }
        missionIndex = Mathf.Max(0, missions.IndexOf(prev));
        if (prev == null && mm != null && mm.Tracked != null) missionIndex = Mathf.Max(0, missions.IndexOf(mm.Tracked.Definition));
    }

    void SaveRocket()
    {
        if (design.stack.Count == 0) { Toast("Nothing to save yet.", true); return; }
        string nm = (nameInput.text ?? "").Trim();
        if (nm.Length == 0) nm = "Rocket " + (RocketSaves.All().Count + 1);
        nameInput.text = nm;
        PlayerPrefs.SetString(NameKey, nm);
        RocketSaves.Upsert(new SavedRocket
        {
            name = nm, design = design.ToJson(), costM = report.costM, launchKg = report.launchKg, dv = report.dvTotal,
            valid = report.valid, savedAt = DateTime.Now.ToString("g", CultureInfo.InvariantCulture),
        });
        Toast("Saved \"" + nm + "\"");
        RefreshSaved();
    }

    // ---- deploy ----
    DateTime Today => GameClock.Instance != null ? GameClock.Instance.Now.Date : DateTime.Today;

    void OpenDeploy()
    {
        if (design.stack.Count == 0) { Toast("Build a rocket first.", true); return; }
        if (!report.valid)
        {
            var first = report.checks.FirstOrDefault(c => c.sev == Sev.Error);
            Toast("Not ready: " + (first != null ? first.text : "fix the rocket"), true);
            rightTab = 2; RefreshAll();
            return;
        }
        if (DeploymentManager.Instance == null) { Toast("No launch pad found in this scene.", true); return; }
        string nm = (nameInput.text ?? "").Trim();
        if (nm.Length == 0) nm = "Rocket " + (RocketSaves.All().Count + 1);
        nameInput.text = nm;
        minDate = Today;
        var cur = DeploymentManager.Instance.LaunchDate;
        pickDate = cur.HasValue && cur.Value >= minDate ? cur.Value : minDate.AddDays(1);
        viewMonth = new DateTime(pickDate.Value.Year, pickDate.Value.Month, 1);
        var m = Mission;
        deployInfo.text = "Rocket: <b>" + nm + "</b>   $" + report.costM.ToString("0.0") + "M\nContract: " + (m == null ? "<color=" + Soft + ">none (free launch)</color>" : "<color=" + Cyan + ">" + m.id + "</color> " + m.displayName)
                          + (DeploymentManager.Instance.Current != null ? "\n<color=" + Warn + ">Replaces the rocket now on the pad.</color>" : "");
        deployDialog.SetActive(true);
        RefreshCalendar();
    }

    void ShiftMonth(int d) { viewMonth = viewMonth.AddMonths(d); RefreshCalendar(); }

    DateTime DayOf(int i) => viewMonth.AddDays(i - (int)viewMonth.DayOfWeek);

    void PickDay(int i)
    {
        var d = DayOf(i);
        if (d.Month != viewMonth.Month || d < minDate) return;
        pickDate = d;
        RefreshCalendar();
    }

    void RefreshCalendar()
    {
        monthLabel.text = viewMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var scheduled = DeploymentManager.Instance != null ? DeploymentManager.Instance.LaunchDate : null;
        for (int i = 0; i < dayButtons.Count; i++)
        {
            var d = DayOf(i);
            bool inMonth = d.Month == viewMonth.Month;
            var b = dayButtons[i];
            b.gameObject.SetActive(inMonth);
            if (!inMonth) continue;
            bool ok = d >= minDate;
            bool picked = pickDate.HasValue && pickDate.Value.Date == d.Date;
            var img = (Image)b.targetGraphic;
            img.color = picked ? activeColor : (scheduled.HasValue && scheduled.Value.Date == d.Date) ? new Color(0.62f, 0.36f, 0.22f, 1f) : ok ? normalColor : Well;
            var t = b.GetComponentInChildren<TMP_Text>(); t.text = d.Day.ToString(); t.color = ok ? Ink : Dim;
            b.interactable = ok;
        }
        deployDate.text = pickDate.HasValue ? "Launch date: <b>" + pickDate.Value.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture) + "</b>" : "";
    }

    void ConfirmDeploy()
    {
        if (!pickDate.HasValue || DeploymentManager.Instance == null) return;
        SaveRocket();
        string nm = nameInput.text;
        DeploymentManager.Instance.Schedule(nm, design, Mission, pickDate.Value);
        deployDialog.SetActive(false);
        Toast("\"" + nm + "\" is on the launch pad. Launch: " + pickDate.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
    }

    // =============================================================== refresh
    void RefreshAll()
    {
        if (!built) return;
        ValidateSel();
        for (int i = 0; i < tabImages.Count; i++) tabImages[i].color = i == openTab ? activeColor : normalColor;
        for (int i = 0; i < rightTabImages.Count; i++) { rightTabImages[i].color = i == rightTab ? activeColor : normalColor; rightViews[i].SetActive(i == rightTab); }
        undoBtn.interactable = hIdx > 0;
        redoBtn.interactable = hIdx < history.Count - 1;
        deleteBtn.interactable = !sel.IsNone;

        RefreshFlyout();
        RefreshHierarchy();
        RefreshSaved();

        report = RocketStats.Evaluate(design, library, Mission);
        RefreshTexts();
        RefreshCard();
        ((Image)deployBtn.targetGraphic).color = report.valid ? Ready : normalColor;

        var rocket = RocketAssembler.Build(design, library, out var b);
        preview.Show(rocket, b);
        RocketAssembler.Highlight(rocket, sel, library.outlineMaterial, Mathf.Max(0.04f, preview.ViewDistance * 0.006f));
        PlayerPrefs.SetString(SaveKey, design.ToJson());
    }

    void ValidateSel()
    {
        bool ok = true;
        switch (sel.kind)
        {
            case SelKind.Stack: ok = sel.index >= 0 && sel.index < design.stack.Count; break;
            case SelKind.Booster: ok = design.boosterId != null; break;
            case SelKind.Fin: ok = design.finId != null; break;
            case SelKind.Payload: ok = design.payloadId != null; break;
            case SelKind.Equip: ok = sel.index >= 0 && sel.index < design.equipment.Count; break;
        }
        if (!ok) sel = Sel.None;
    }

    PartInfo SelPart()
    {
        switch (sel.kind)
        {
            case SelKind.Stack: return library.Get(design.stack[sel.index]);
            case SelKind.Booster: return library.Get(design.boosterId);
            case SelKind.Fin: return library.Get(design.finId);
            case SelKind.Payload: return library.Get(design.payloadId);
            case SelKind.Equip: return library.Get(design.equipment[sel.index]);
            default: return null;
        }
    }

    static string Describe(PartInfo p, bool rich)
    {
        var sb = new StringBuilder();
        sb.Append("<b>" + p.DisplayName + "</b>  <color=" + Soft + ">" + p.category + " " + p.size + "</color>");
        var parts = new List<string> { p.heightM.ToString("0.0") + " m", "$" + p.costM.ToString("0.##") + "M" };
        if (p.dryMassKg > 0) parts.Add("dry " + p.dryMassKg.ToString("N0") + " kg");
        if (p.massKg > 0) parts.Add(p.massKg.ToString("N0") + " kg");
        if (p.fuelKg > 0) parts.Add("fuel " + p.fuelKg.ToString("N0") + " kg");
        if (p.thrustKn > 0 || p.thrustVacKn > 0) parts.Add("thrust " + p.thrustKn.ToString("0.##") + "/" + p.thrustVacKn.ToString("0.##") + " kN");
        if (p.ispVac > 0) parts.Add("Isp " + p.ispSl.ToString("0") + "/" + p.ispVac.ToString("0") + " s");
        if (p.bells > 1) parts.Add(p.bells + " engines");
        if (p.powerW != 0) parts.Add((p.category == "Power" ? "makes " : p.powerW < 0 ? "needs " : "uses ") + Mathf.Abs(p.powerW).ToString("0") + " W");
        if (p.storageWh > 0) parts.Add("stores " + p.storageWh.ToString("0") + " Wh");
        if (p.dataMbps > 0) parts.Add(p.dataMbps.ToString("0.##") + " Mbps");
        if (p.commMbps > 0) parts.Add("comms " + p.commMbps.ToString("0.##") + " Mbps");
        if (p.payloadMaxKg > 0) parts.Add("carries " + p.payloadMaxKg.ToString("N0") + " kg");
        if (p.crew > 0) parts.Add("crew " + p.crew);
        sb.Append("\n" + string.Join("  ", parts));
        if (!string.IsNullOrEmpty(p.note)) sb.Append("\n<color=" + Soft + ">" + p.note + "</color>");
        return sb.ToString();
    }

    void RefreshFlyout()
    {
        flyout.SetActive(openTab >= 0);
        foreach (Transform c in flyList) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        if (openTab < 0) return;
        flyTitle.text = Tabs[openTab].label;
        var cats = Tabs[openTab].cats;
        foreach (var p in library.Parts.Where(x => cats.Contains(x.category)).OrderBy(x => x.category).ThenBy(x => PartInfo.SizeRank(x.size)).ThenBy(x => x.costM))
        {
            var part = p;
            RocketRules.CanAdd(design, part, library, out var advisory);
            bool ok = string.IsNullOrEmpty(advisory);
            var r = Rt(flyList, part.id, 0, 0, 100, 28);
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            var img = Img(r, normalColor);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            Style(b); b.onClick.AddListener(() => AddPart(part));
            Icon(r, part.id, 3, 3, 22, 22);
            var name = Txt(r, part.DisplayName, 7.5f, TextAlignmentOptions.MidlineLeft); Stretch((RectTransform)name.transform, 28, 2, 2, 14); name.color = Ink;
            var info = Txt(r, "$" + part.costM.ToString("0.#") + "M  " + part.size, 6.5f, TextAlignmentOptions.MidlineLeft); Stretch((RectTransform)info.transform, 28, 14, 2, 2); info.color = Dim;
            Tip(r.gameObject, Describe(part, true) + (ok ? "" : "\n<color=" + Warn + ">" + advisory + "</color>"));
        }
    }

    void RefreshHierarchy()
    {
        foreach (Transform c in hierList) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        int n = design.stack.Count;
        for (int r = 0; r < n; r++)
        {
            int si = n - 1 - r;
            var p = library.Get(design.stack[si]);
            if (p == null) continue;
            var row = Rt(hierList, "Row", 0, 0, 100, 17);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 17;
            bool isSel = sel.kind == SelKind.Stack && sel.index == si;
            var img = Img(row, isSel ? activeColor : RowColor);
            var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            int idx = si;
            b.onClick.AddListener(() => { sel = Sel.Of(SelKind.Stack, idx); RefreshAll(); });
            row.gameObject.AddComponent<RocketHierarchyRow>().Init(this, si);
            var grip = Txt(row, "=", 8f, TextAlignmentOptions.Center); Stretch((RectTransform)grip.transform, 0, 0, 0, 0); grip.rectTransform.sizeDelta = new Vector2(10, 17);
            grip.rectTransform.anchorMin = new Vector2(0, 0); grip.rectTransform.anchorMax = new Vector2(0, 1); grip.rectTransform.pivot = new Vector2(0, 0.5f); grip.rectTransform.anchoredPosition = Vector2.zero; grip.color = Dim;
            Icon(row, p.id, 11, 2, 13, 13);
            var t = Txt(row, p.DisplayName, 7.5f, TextAlignmentOptions.MidlineLeft); Stretch((RectTransform)t.transform, 27, 0, 3, 0);
        }
        if (n > 0 || design.boosterId != null || design.finId != null || design.payloadId != null || design.equipment.Count > 0)
        {
            AttachedRow("side boosters", design.boosterId, SelKind.Booster, 0, design.boosterCount, -1, 6);
            AttachedRow("fins", design.finId, SelKind.Fin, 0, design.finCount, 2, 6);
            AttachedRow("payload", design.payloadId, SelKind.Payload, 0, 0, 0, 0);
            for (int i = 0; i < design.equipment.Count; i++) AttachedRow("equipment", design.equipment[i], SelKind.Equip, i, 0, 0, 0);
        }
        if (hierList.childCount == 0)
        {
            var t = Txt(Rt(hierList, "Empty", 0, 0, 100, 60), "Empty rocket.\nPick a category on the left and click a part to add it.", 7.5f, TextAlignmentOptions.Center);
            t.color = Dim; t.textWrappingMode = TextWrappingModes.Normal;
            t.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
        }
    }

    void AttachedRow(string label, string id, SelKind kind, int index, int count, int min, int max)
    {
        var p = library.Get(id);
        if (p == null) return;
        var row = Rt(hierList, "Attached", 0, 0, 100, 17);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 17;
        bool isSel = sel.kind == kind && (kind == SelKind.Equip ? sel.index == index : true);
        var img = Img(row, isSel ? activeColor : AttachColor);
        var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        b.onClick.AddListener(() => { sel = Sel.Of(kind, index); RefreshAll(); });
        Icon(row, p.id, 3, 2, 13, 13);
        string cnt = count > 0 ? "  x" + count : "";
        bool stepper = kind == SelKind.Booster || kind == SelKind.Fin;
        var t = Txt(row, p.DisplayName + cnt, 7.5f, TextAlignmentOptions.MidlineLeft); Stretch((RectTransform)t.transform, 19, 0, stepper ? 34 : 3, 0);
        Tip(row.gameObject, char.ToUpper(label[0]) + label.Substring(1));
        if (stepper)
        {
            AddSmall(row, "-", -17, () => { if (kind == SelKind.Booster) design.boosterCount = Mathf.Max(1, design.boosterCount - 1); else design.finCount = Mathf.Max(2, design.finCount - 1); sel = Sel.Of(kind, 0); Changed(); });
            AddSmall(row, "+", -2, () => { if (kind == SelKind.Booster) design.boosterCount = Mathf.Min(6, design.boosterCount + 1); else design.finCount = Mathf.Min(6, design.finCount + 1); sel = Sel.Of(kind, 0); Changed(); });
        }
    }

    void AddSmall(RectTransform row, string label, float xFromRight, UnityAction act)
    {
        var s = Btn(row, label, 0, 2, 13, 13, act, 7.5f);
        var r = (RectTransform)s.transform;
        r.anchorMin = r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1); r.anchoredPosition = new Vector2(xFromRight, -2);
    }

    void Header(string text)
    {
        var h = Txt(Rt(savedList, "Header", 0, 0, 100, 12), text, 7f, TextAlignmentOptions.MidlineLeft);
        h.color = new Color(0.5f, 0.86f, 1f, 1f);
        h.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 12;
    }

    void LoadDemo(RocketDemos.Demo demo)
    {
        design = RocketDesign.FromJson(demo.design.ToJson());
        nameInput.text = demo.name;
        sel = Sel.None;
        string note = "";
        if (!string.IsNullOrEmpty(demo.missionId))
        {
            int i = missions.FindIndex(x => x != null && x.id == demo.missionId);
            if (i >= 0) missionIndex = i;
            else note = "  Start " + demo.missionId + " to check it against its contract.";
        }
        Changed();
        Toast("Loaded \"" + demo.name + "\"." + note);
    }

    void RefreshSaved()
    {
        foreach (Transform c in savedList) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        Header("DEMO ROCKETS");
        foreach (var dm in RocketDemos.All)
        {
            var demo = dm;
            var drow = Rt(savedList, "Demo", 0, 0, 100, 24);
            drow.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            var dimg = Img(drow, AttachColor);
            var db = drow.gameObject.AddComponent<Button>(); db.targetGraphic = dimg; Style(db);
            db.onClick.AddListener(() => LoadDemo(demo));
            var dt = Txt(drow, demo.name, 7.5f, TextAlignmentOptions.TopLeft); Stretch((RectTransform)dt.transform, 4, 2, 3, 11);
            var dd = Txt(drow, demo.description, 6.5f, TextAlignmentOptions.BottomLeft); Stretch((RectTransform)dd.transform, 4, 11, 3, 2); dd.color = Dim; dd.overflowMode = TextOverflowModes.Ellipsis; dd.textWrappingMode = TextWrappingModes.NoWrap;
            Tip(drow.gameObject, "<b>" + demo.name + "</b>\n" + demo.description);
        }
        Header("MY ROCKETS");
        var all = RocketSaves.All();
        foreach (var sr in all)
        {
            var s = sr;
            var row = Rt(savedList, "Saved", 0, 0, 100, 22);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            var img = Img(row, normalColor);
            var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = img; Style(b);
            b.onClick.AddListener(() => { design = RocketDesign.FromJson(s.design); nameInput.text = s.name; sel = Sel.None; Changed(); Toast("Loaded \"" + s.name + "\""); });
            var t = Txt(row, s.name, 7.5f, TextAlignmentOptions.TopLeft); Stretch((RectTransform)t.transform, 4, 2, 16, 10);
            var i = Txt(row, "$" + s.costM.ToString("0.#") + "M  " + s.launchKg.ToString("N0") + " kg  " + (s.valid ? "<color=" + Good + ">ready</color>" : "<color=" + Warn + ">draft</color>"), 6.5f, TextAlignmentOptions.BottomLeft);
            Stretch((RectTransform)i.transform, 4, 10, 16, 2); i.color = Dim;
            AddSmall(row, "x", -2, () => { RocketSaves.Delete(s.name); RefreshSaved(); });
        }
        if (all.Count == 0)
        {
            var t = Txt(Rt(savedList, "Empty", 0, 0, 100, 50), "No saved rockets yet.\nName your rocket and press Save.", 7.5f, TextAlignmentOptions.Center);
            t.color = Dim; t.textWrappingMode = TextWrappingModes.Normal;
            t.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
        }
    }

    void RefreshCard()
    {
        var p = sel.IsNone ? null : SelPart();
        selCard.SetActive(p != null);
        if (p == null) return;
        string extra = sel.kind == SelKind.Booster ? "  <color=" + Cyan + ">x" + design.boosterCount + "</color>" : sel.kind == SelKind.Fin ? "  <color=" + Cyan + ">x" + design.finCount + "</color>" : "";
        cardText.text = Describe(p, false).Replace("\n", "\n") + extra;
    }

    void RefreshTexts()
    {
        var m = Mission;
        missionLabel.text = m == null ? "<color=" + Soft + ">No mission</color>" : "<color=" + Cyan + ">" + m.id + "</color>";
        var r = report;

        var s = new StringBuilder();
        s.AppendLine("Height <b>" + r.heightM.ToString("0.0") + " m</b>  Width <b>" + r.widthM.ToString("0.0") + " m</b>");
        s.AppendLine("Launch mass <b>" + r.launchKg.ToString("N0") + " kg</b>");
        s.AppendLine("<color=" + Soft + ">dry " + r.dryKg.ToString("N0") + "  fuel " + r.fuelKg.ToString("N0") + "  payload " + r.payloadKg.ToString("N0") + "</color>");
        s.AppendLine("Thrust <b>" + r.thrustKn.ToString("N0") + " kN</b>");
        s.AppendLine("Liftoff TWR <b>" + r.liftoffTwr.ToString("0.00") + "</b>");
        s.AppendLine("Delta-v <b>" + r.dvTotal.ToString("N0") + " m/s</b>");
        for (int i = 0; i < r.stages.Count; i++)
            if (r.stages[i].parts.Count > 0) s.AppendLine("<color=" + Soft + ">stage " + (i + 1) + ": " + r.stages[i].dv.ToString("N0") + " m/s, TWR " + r.stages[i].twr.ToString("0.0") + "</color>");
        s.AppendLine("Cost <b>$" + r.costM.ToString("0.0") + "M</b>");
        s.AppendLine("Power " + r.powerSupplyW.ToString("0") + " W made, " + r.powerDemandW.ToString("0") + " W used");
        s.AppendLine("Comms <b>" + r.commMbps.ToString("0.##") + " Mbps</b>");
        statsText.text = s.ToString();

        var c = new StringBuilder();
        c.AppendLine(r.valid ? "<color=" + Good + "><b>READY TO DEPLOY</b></color>" : "<color=" + Bad + "><b>NOT READY</b></color>");
        if (m != null) c.AppendLine("<color=" + Soft + ">" + m.displayName + "</color>");
        else c.AppendLine("<color=" + Soft + ">No active mission. Start a contract in the Mission Manager to check your rocket against it.</color>");
        foreach (var ch in r.checks)
        {
            string col = ch.sev == Sev.Ok ? Good : ch.sev == Sev.Warn ? Warn : Bad;
            string tag = ch.sev == Sev.Ok ? "OK" : ch.sev == Sev.Warn ? "!" : "X";
            c.AppendLine("<color=" + col + ">[" + tag + "]</color> " + ch.text);
        }
        if (r.checks.Count == 0) c.AppendLine("<color=" + Soft + ">Add parts to see checks.</color>");
        checksText.text = c.ToString();

        string status = r.valid ? "<color=" + Good + ">ready</color>" : "<color=" + Warn + ">not ready</color>";
        summaryText.text = "$" + r.costM.ToString("0.0") + "M  " + (r.launchKg / 1000f).ToString("0.0") + " t  dv " + (r.dvTotal / 1000f).ToString("0.0") + " km/s  " + status;
    }
}

/// <summary>A row in the build hierarchy that can be dragged up or down to re-order the stack.</summary>
class RocketHierarchyRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RocketBuilderUI owner; int stackIndex;
    RectTransform rt, content; LayoutElement le; CanvasGroup cg; float lastY;

    public void Init(RocketBuilderUI o, int si) { owner = o; stackIndex = si; rt = (RectTransform)transform; }

    public void OnBeginDrag(PointerEventData e)
    {
        content = (RectTransform)rt.parent;
        le = GetComponent<LayoutElement>(); le.ignoreLayout = true;
        cg = gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0.75f; cg.blocksRaycasts = false;
        rt.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (content == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, e.position, e.pressEventCamera, out var lp)) return;
        lastY = lp.y;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, lp.y + rt.rect.height * 0.5f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        int visual = 0;
        foreach (Transform sib in content)
        {
            if (sib == transform || sib.GetComponent<RocketHierarchyRow>() == null) continue;
            var sr = (RectTransform)sib;
            if (sr.anchoredPosition.y - sr.rect.height * 0.5f > lastY) visual++;
        }
        owner.MoveStack(stackIndex, visual);
    }
}
