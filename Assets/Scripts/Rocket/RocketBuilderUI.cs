using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum DesignerMode { Rocket, Payload }

/// <summary>
/// The Rocket Designer and the Satellite (Payload) Designer, laid out like the reference screens:
/// a left card with the 3D preview, a stats area and the Blueprints / Buy Blueprints buttons,
/// and a right card with a coloured title, category chips and a 3-column grid of parts with a price and an EQUIP button.
/// Missing from the reference and added here: undo / redo / delete / auto order, the build order list, contract checks,
/// naming + saving, and Deploy with a launch-date calendar. Built in code the first time it opens.
/// Uses the existing glass materials through <see cref="UIKit"/>; nothing about the glass look is changed.
/// </summary>
public class RocketBuilderUI : MonoBehaviour
{
    [Header("Data")]
    public PartLibrary library;
    public DesignerMode mode = DesignerMode.Rocket;

    // Kept so the existing scene (and the setup tool) still serialise: the look now comes from Resources/UITheme.
    [HideInInspector] public Material flatMat;
    [HideInInspector] public Sprite roundSprite;
    [HideInInspector] public TMP_FontAsset bodyFont, headFont;

    // the simple rocket builder: five categories (Payload = head + cargo, Fuel tank, Engine, Outer = boosters + fins, Extras = no visual)
    static readonly (string label, string cat)[] RocketCats =
    {
        ("Payload", "Payload"), ("Fuel tank", "Tank"), ("Engine", "Engine"), ("Outer", "Outer"), ("Extras", "Extras"),
    };
    static readonly Dictionary<string, string> CatIcons = new Dictionary<string, string>
    { { "Payload", "cat_payload" }, { "Tank", "cat_tank" }, { "Engine", "cat_engine" }, { "Outer", "cat_outer" }, { "Extras", "cat_extras" } };
    static readonly string[][] SubTabs =
    {
        new[] { "Head", "Cargo" }, new[] { "Main stage", "Upper stage" }, new[] { "Main stage", "Upper stage" },
        new[] { "Boosters", "Fins", "Combos" }, new[] { "Recovery", "Power", "Comms", "Thrusters" },
    };
    static readonly (string name, string booster, string fin)[] Combos =
    {
        ("Booster S + Fins S", "Booster_S", "Fin_S"), ("Booster M + Fins S", "Booster_M", "Fin_S"), ("Booster L + Fins L", "Booster_L", "Fin_L"),
    };
    static readonly string[] LiveryNames = { "White", "Black", "Copper", "Blue", "Red", "Grey", "Green", "Gold" };
    static readonly Color32[] LiveryColors =
    {
        new Color32(232, 232, 228, 255), new Color32(44, 46, 52, 255), new Color32(205, 135, 80, 255), new Color32(86, 112, 150, 255),
        new Color32(170, 82, 72, 255), new Color32(130, 134, 140, 255), new Color32(96, 128, 100, 255), new Color32(196, 168, 88, 255),
    };
    static readonly (string label, string cat)[] PayloadCats =
    {
        ("Payloads", "Payload"), ("Communication", "Comms"), ("Power", "Power"),
    };

    const string SaveKey = "SpaceAgency.RocketDesign", NameKey = "SpaceAgency.RocketName";
    const string Good = "#8FE3B0", Warn = "#FFB878", Bad = "#FF8C8C", Soft = "#9FB4C8", Cyan = "#7FDBFF";
    const float LW = 190f, RW = 196f, CardH = 302f, CardY = 32f, Gap = 4f;

    // ---------------- state ----------------
    RocketDesign design = new RocketDesign();
    RocketPreview preview;
    RocketReport report;
    Sel sel = Sel.None;
    int catIdx, subTab, infoView;                 // infoView: 0 stats, 1 order, 2 checks, 3 blueprints, 4 buy blueprints
    bool built;
    float W = 640f, H = 360f;
    readonly List<string> history = new List<string>();
    int hIdx = -1;
    readonly List<MissionDefinition> missions = new List<MissionDefinition> { null };
    int missionIndex;

    // ---------------- ui refs ----------------
    readonly List<Image> chipImages = new List<Image>();
    Segmented infoSeg;
    GameObject selCard, deployDialog;
    GameObject[] infoViews;
    RectTransform chipRow, gridContent, subRow, statsGrid, paintCard;
    TMP_Text countLabel, orbitText;
    GameObject countBox;
    Image[] orbitPips, paintRings;
    TMP_Text[] statValue = new TMP_Text[8];
    Image[] statIcon = new Image[8];
    Image orbitIcon;
    Transform hierList, bpList, buyList;
    TMP_Text titleText, statsText, checksText, missionLabel, summaryText, cardText, deployInfo, deployDate, monthLabel;
    Image titleBar, rotImg, panImg, bpBtnImg, buyBtnImg;
    TMP_InputField nameInput;
    Button undoBtn, redoBtn, deleteBtn, deployBtn;
    RocketPreviewInput previewInput;
    readonly List<Button> dayButtons = new List<Button>();
    DateTime viewMonth, minDate;
    DateTime? pickDate;

    MissionDefinition Mission => missionIndex > 0 && missionIndex < missions.Count ? missions[missionIndex] : null;
    (string label, string cat)[] Cats => mode == DesignerMode.Rocket ? RocketCats : PayloadCats;
    Color Accent => mode == DesignerMode.Rocket ? UIKit.T.orange : UIKit.T.purple;

    // =============================================================== lifecycle
    void OnEnable()
    {
        if (library == null) { Debug.LogWarning("RocketBuilderUI: no PartLibrary assigned."); return; }
        if (UITheme.Current == null) { Debug.LogError("RocketBuilderUI: Resources/UITheme is missing. Run Tools / Space Agency / Setup Game UI."); return; }
        Canvas.ForceUpdateCanvases();
        if (!built) Build();
        transform.SetAsLastSibling();
        design = RocketDesign.FromJson(PlayerPrefs.GetString(SaveKey, ""));
        design.stack.RemoveAll(id => library.Get(id) == null);
        EnsureSimple();
        history.Clear(); hIdx = -1; Commit();
        sel = Sel.None; catIdx = 0; subTab = 0; infoView = 0;
        nameInput.text = PlayerPrefs.GetString(NameKey, "");
        deployDialog.SetActive(false);
        RefreshMissions();
        ApplyMode();
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
        var kb = Keyboard.current;
        if (kb == null || nameInput.isFocused || deployDialog.activeSelf) return;
        bool ctrl = kb.ctrlKey.isPressed;
        if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) DeleteSelected();
        if (ctrl && kb.zKey.wasPressedThisFrame) Undo();
        if (ctrl && kb.yKey.wasPressedThisFrame) Redo();
    }

    void Toast(string msg, bool bad = false) => Hud.Toast(msg);

    static void Tip(GameObject go, string text) => go.AddComponent<TooltipTrigger>().text = text;

    Image PartIcon(Transform parent, string id, float x, float y, float w, float h)
        => UIKit.IconAt(parent, library.Icon(id), x, y, w, h);

    // =============================================================== layout
    void Build()
    {
        var root = (RectTransform)transform;
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        W = root.rect.width > 10 ? root.rect.width : 640f;
        H = root.rect.height > 10 ? root.rect.height : 360f;

        // the old full-screen dark backdrop is gone: the game stays visible behind the two cards
        var bg = GetComponent<Image>();
        if (bg != null) { bg.color = Color.clear; bg.raycastTarget = false; }

        float leftX = W - 8f - (LW + Gap + RW), rightX = leftX + LW + Gap;
        BuildLeftCard(root, leftX);
        BuildPaintCard(root, leftX - 4f - 30f);
        BuildRightCard(root, rightX);
        BuildDeployDialog(root);
        built = true;
    }

    void BuildLeftCard(RectTransform root, float x)
    {
        var card = UIKit.Card(root, "Left Card", x, CardY, LW, CardH);

        // ---- 3D preview ----
        int rtW = 1024, rtH = Mathf.RoundToInt(rtW * 150f / 180f);
        preview = RocketPreview.Create(rtW, rtH);
        var frame = UIKit.At(card, "Preview", 5, 5, 180, 150);
        UIKit.Tint(frame, new Color(0.02f, 0.04f, 0.08f, 1f));
        var clip = UIKit.At(frame, "Clip", 0, 0, 180, 150); UIKit.Fill(clip, 1, 1, 1, 1);
        clip.gameObject.AddComponent<RectMask2D>();
        var viewRt = UIKit.At(clip, "View", 0, 0, 178, 148); UIKit.Fill(viewRt);
        var raw = viewRt.gameObject.AddComponent<RawImage>(); raw.texture = preview.Texture;
        previewInput = viewRt.gameObject.AddComponent<RocketPreviewInput>(); previewInput.preview = preview;
        previewInput.picked = pr => { sel = pr == null ? Sel.None : Sel.Of(pr.kind, pr.index); RefreshAll(); };

        // top row: undo / redo / delete / auto
        var T = UIKit.T;
        undoBtn = UIKit.Btn(frame, "Undo", 4, 4, 26, 12, T.chip, Undo, 6f);
        redoBtn = UIKit.Btn(frame, "Redo", 32, 4, 26, 12, T.chip, Redo, 6f);
        deleteBtn = UIKit.Btn(frame, "Delete", 60, 4, 30, 12, T.chip, DeleteSelected, 6f, true, new Color(1f, 0.45f, 0.42f, 1f));

        // bottom row: view controls
        var bRot = UIKit.Btn(frame, "Rotate", 4, 134, 26, 12, T.teal, () => SetPan(false), 6f);
        var bPan = UIKit.Btn(frame, "Move", 32, 134, 26, 12, T.chip, () => SetPan(true), 6f);
        UIKit.Btn(frame, "Reset", 60, 134, 26, 12, T.chip, () => preview.ResetView(), 6f);
        rotImg = (Image)bRot.targetGraphic; panImg = (Image)bPan.targetGraphic;
        Tip(bRot.gameObject, "Drag the view to rotate around it\nScroll to zoom, click a part to select it");
        Tip(bPan.gameObject, "Drag the view to move it anywhere.\nRight or middle mouse button does this too.");

        // selected part caption
        var sc = UIKit.At(frame, "Selected", 4, 100, 172, 30);
        UIKit.Tint(sc, new Color(0.02f, 0.05f, 0.1f, 0.82f));
        cardText = UIKit.Text(sc, "", 6.2f, TextAlignmentOptions.MidlineLeft);
        UIKit.Fill((RectTransform)cardText.transform, 4, 1, 4, 1);
        cardText.textWrappingMode = TextWrappingModes.Normal; cardText.lineSpacing = -8f;
        selCard = sc.gameObject;

        // ---- info area: stats / order / checks (and the blueprint lists) ----
        infoSeg = new Segmented(card, 5, 158, 90, 13, new[] { "Stats", "Checks" }, 0, idx => { infoView = idx == 0 ? 0 : 2; RefreshAll(); });
        summaryText = UIKit.TextAt(card, "Summary", 98, 158, 87, 13, "", 6f, TextAlignmentOptions.MidlineRight);

        const float ay = 174f, ah = 82f;
        infoViews = new GameObject[5];
        var vStats = UIKit.At(card, "StatsView", 5, ay, 180, ah);
        statsText = ScrollText(vStats, 180, ah);
        BuildStatsGrid(vStats, 180, ah);
        var vOrder = UIKit.At(card, "OrderView", 5, ay, 180, ah);
        hierList = UIKit.ScrollList(vOrder, "Order", 0, 0, 180, ah, T.well, 2f);
        var vChecks = UIKit.At(card, "ChecksView", 5, ay, 180, ah);
        UIKit.Btn(vChecks, "<", 0, 0, 14, 12, T.chip, () => ChangeMission(-1), 7f);
        missionLabel = UIKit.TextAt(vChecks, "Contract", 16, 0, 148, 12, "", 6.5f, TextAlignmentOptions.Center);
        UIKit.Btn(vChecks, ">", 166, 0, 14, 12, T.chip, () => ChangeMission(1), 7f);
        var chk = UIKit.At(vChecks, "Checks", 0, 14, 180, ah - 14);
        checksText = ScrollText(chk, 180, ah - 14);
        var vBp = UIKit.At(card, "BlueprintView", 5, ay, 180, ah);
        bpList = UIKit.ScrollList(vBp, "Blueprints", 0, 0, 180, ah, T.well, 2f);
        var vBuy = UIKit.At(card, "BuyView", 5, ay, 180, ah);
        buyList = UIKit.ScrollList(vBuy, "Buy", 0, 0, 180, ah, T.well, 2f);
        infoViews[0] = vStats.gameObject; infoViews[1] = vOrder.gameObject; infoViews[2] = vChecks.gameObject; infoViews[3] = vBp.gameObject; infoViews[4] = vBuy.gameObject;

        // ---- name / save / deploy ----
        nameInput = UIKit.Input(card, 5, 261, 88, 14, "Rocket name", 18, 7f);
        UIKit.Btn(card, "Save", 96, 261, 40, 14, T.chip, SaveRocket, 6.5f);
        deployBtn = UIKit.Btn(card, "Deploy", 139, 261, 46, 14, T.chip, OpenDeploy, 6.5f);

        // ---- blueprints (as in the reference) ----
        var bp = UIKit.Btn(card, "Blueprints", 5, 280, 89, 18, UIKit.Wash(T.blue, 0.35f), () => { infoView = infoView == 3 ? 0 : 3; RefreshAll(); }, 7f, true, new Color(0.7f, 0.69f, 1f, 1f));
        var buy = UIKit.Btn(card, "Buy blueprints", 96, 280, 89, 18, UIKit.Wash(T.purple, 0.35f), () => { infoView = infoView == 4 ? 0 : 4; RefreshAll(); }, 7f, true, new Color(0.9f, 0.7f, 1f, 1f));
        bpBtnImg = (Image)bp.targetGraphic; buyBtnImg = (Image)buy.targetGraphic;
    }

    TMP_Text ScrollText(RectTransform parent, float w, float h)
    {
        var root = UIKit.At(parent, "Scroll", 0, 0, w, h);
        UIKit.Tint(root, UIKit.T.well);
        var vp = UIKit.At(root, "Viewport", 0, 0, w, h); UIKit.Fill(vp, 2, 2, 2, 2);
        vp.gameObject.AddComponent<RectMask2D>();
        var t = UIKit.At(vp, "Text", 0, 0, w - 4, 10);
        t.anchorMin = new Vector2(0, 1); t.anchorMax = new Vector2(1, 1); t.pivot = new Vector2(0.5f, 1f);
        t.offsetMin = new Vector2(2, 0); t.offsetMax = new Vector2(-2, 0);
        var tm = t.gameObject.AddComponent<TextMeshProUGUI>();
        tm.font = UIKit.T.body; tm.fontSize = 6.8f; tm.color = UIKit.T.ink; tm.alignment = TextAlignmentOptions.TopLeft;
        tm.textWrappingMode = TextWrappingModes.Normal; tm.raycastTarget = false; tm.lineSpacing = -6f;
        t.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = root.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = t; sr.horizontal = false; sr.scrollSensitivity = 14f; sr.movementType = ScrollRect.MovementType.Clamped;
        return tm;
    }

    void SetPan(bool pan)
    {
        previewInput.panMode = pan;
        rotImg.color = pan ? UIKit.T.chip : UIKit.T.teal;
        panImg.color = pan ? UIKit.T.teal : UIKit.T.chip;
    }

    void BuildRightCard(RectTransform root, float x)
    {
        var card = UIKit.Card(root, "Right Card", x, CardY, RW, CardH);
        titleText = UIKit.TitleBar(card, "Rocket Designer", UIKit.T.orange, RW, () => gameObject.SetActive(false));
        titleBar = UIKit.TitleAccent(titleText);
        chipRow = UIKit.At(card, "Categories", 6, 26, RW - 12, 24);
        subRow = UIKit.At(card, "SubTabs", 6, 53, RW - 12, 13);
        gridContent = UIKit.ScrollList(card, "Parts", 6, 69, RW - 12, CardH - 75, UIKit.T.well, 3f, true, new Vector2(56, 86), 3);
        ((GridLayoutGroup)gridContent.GetComponent<GridLayoutGroup>()).padding = new RectOffset(3, 3, 3, 3);
    }

    void ApplyMode()
    {
        bool rocket = mode == DesignerMode.Rocket;
        titleText.text = rocket ? "Rocket Designer" : "Satellite Designer";
        titleBar.color = Accent;
        previewInput.panMode = false; SetPan(false);
        statsText.transform.parent.parent.gameObject.SetActive(!rocket);          // the old text stats are for the satellite designer
        statsGrid.gameObject.SetActive(rocket);
        subRow.gameObject.SetActive(rocket);
        if (paintCard != null) paintCard.gameObject.SetActive(true);
        nameInput.placeholder.GetComponent<TMP_Text>().text = rocket ? "Rocket name" : "Rocket name";
        // category chips
        UIKit.Clear(chipRow);
        chipImages.Clear();
        var cats = Cats;
        float cw = rocket ? (RW - 12 - 2f * (cats.Length - 1)) / cats.Length : Mathf.Min(26f, (RW - 12 - 2f * (cats.Length - 1)) / cats.Length);
        for (int i = 0; i < cats.Length; i++)
        {
            int idx = i;
            string cat = cats[i].cat;
            var r = UIKit.At(chipRow, cats[i].label, i * (cw + 2f), 0, cw, rocket ? 24 : 20);
            var b = UIKit.MakeButton(r, "", UIKit.T.chip, () => { catIdx = idx; subTab = 0; RefreshAll(); });
            if (rocket && CatIcons.TryGetValue(cat, out var iconName))
            {
                UIKit.IconAt(r, Resources.Load<Sprite>("UIIcons/" + iconName), cw / 2f - 6.5f, 2f, 13, 13, UIKit.T.ink);
                var lt = UIKit.TextAt(r, "L", 0, 15, cw, 8, cats[i].label, 5.2f, TextAlignmentOptions.Center, true);
                lt.raycastTarget = false;
            }
            else
            {
                var first = library.Parts.FirstOrDefault(p => p.category == cat);
                if (first != null) PartIcon(r, first.id, 2, 2, cw - 4, 16);
                else UIKit.Text(r, cats[i].label.Substring(0, 1), 8f);
            }
            Tip(r.gameObject, cats[i].label);
            chipImages.Add((Image)b.targetGraphic);
        }
    }

    // =============================================================== deploy dialog
    void BuildDeployDialog(RectTransform root)
    {
        var dim = UIKit.At(root, "DeployDialog", 0, 0, W, H); UIKit.Fill(dim);
        var dimImg = dim.gameObject.AddComponent<Image>(); dimImg.color = new Color(0, 0, 0, 0.55f);
        deployDialog = dim.gameObject;
        float bw = 232, bh = 222, bx = (W - bw) / 2, by = (H - bh) / 2;
        var box = UIKit.Card(dim, "Box", bx, by, bw, bh);
        UIKit.TitleBar(box, "Deploy to the launch pad", UIKit.T.green, bw, () => deployDialog.SetActive(false));
        deployInfo = UIKit.TextAt(box, "Info", 10, 26, bw - 20, 30, "", 6.8f, TextAlignmentOptions.TopLeft);
        deployInfo.textWrappingMode = TextWrappingModes.Normal; deployInfo.lineSpacing = -6f;
        UIKit.Btn(box, "<", 12, 60, 16, 13, UIKit.T.chip, () => ShiftMonth(-1), 8f);
        monthLabel = UIKit.TextAt(box, "Month", 30, 60, bw - 60, 13, "", 8f, TextAlignmentOptions.Center, true);
        UIKit.Btn(box, ">", bw - 28, 60, 16, 13, UIKit.T.chip, () => ShiftMonth(1), 8f);
        string[] wd = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        float gx = (bw - 7 * 28) / 2;
        for (int i = 0; i < 7; i++) UIKit.TextAt(box, "WD" + i, gx + i * 28, 76, 28, 11, wd[i], 6.5f, TextAlignmentOptions.Center, false, UIKit.T.dim);
        dayButtons.Clear();
        for (int i = 0; i < 42; i++)
        {
            int idx = i;
            var b = UIKit.Btn(box, "Day" + i, gx + (i % 7) * 28 + 1, 88 + (i / 7) * 15, 26, 13, UIKit.T.chip, () => PickDay(idx), 7f, false);
            dayButtons.Add(b);
        }
        deployDate = UIKit.TextAt(box, "Picked", 0, 182, bw, 12, "", 7.5f, TextAlignmentOptions.Center);
        UIKit.Btn(box, "Confirm deploy", 12, 200, 104, 16, UIKit.T.green, ConfirmDeploy, 6.8f);
        UIKit.Btn(box, "Cancel", bw - 116, 200, 104, 16, UIKit.T.chip, () => deployDialog.SetActive(false), 6.8f);
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
    void AddPart(PartInfo p)
    {
        if (mode == DesignerMode.Rocket) { EquipSimple(p); return; }
        RocketRules.CanAdd(design, p, library, out var note);
        sel = RocketRules.Add(design, p, library);
        Changed();
        Toast(string.IsNullOrEmpty(note) ? p.DisplayName + " equipped" : note, !string.IsNullOrEmpty(note));
    }

    void DeleteSelected()
    {
        if (sel.IsNone) return;
        if (mode == DesignerMode.Rocket) { DeleteSimple(); return; }
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
        if (mode == DesignerMode.Rocket) return;
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
            foreach (var m in mm.Running) if (m.Definition != null && !missions.Contains(m.Definition)) missions.Add(m.Definition);
        missionIndex = Mathf.Max(0, missions.IndexOf(prev));
        if (prev == null && mm != null && mm.Tracked != null) missionIndex = Mathf.Max(0, missions.IndexOf(mm.Tracked.Definition));
    }

    void SaveRocket()
    {
        if (design.stack.Count == 0 && design.payloadId == null) { Toast("Nothing to save yet.", true); return; }
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
        RefreshLists();
    }

    // ---- deploy ----
    DateTime Today => GameClock.Instance != null ? GameClock.Instance.Now.Date : DateTime.Today;
    long DeployCost => (long)(report.costM * 1_000_000.0 * Agency.CostFactor);

    void OpenDeploy()
    {
        if (design.stack.Count == 0) { Toast("Build a rocket first.", true); return; }
        if (!report.valid)
        {
            var first = report.checks.FirstOrDefault(c => c.sev == Sev.Error);
            Toast("Not ready: " + (first != null ? first.text : "fix the rocket"), true);
            infoView = 2; RefreshAll();
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
        bool afford = Agency.CanAfford(DeployCost);
        deployInfo.text = "Rocket: <b>" + nm + "</b>   Build cost <b>$" + UIKit.Money(DeployCost) + "</b>"
                          + (afford ? "" : "  <color=" + Bad + ">(not enough money)</color>")
                          + "\nContract: " + (m == null ? "<color=" + Soft + ">none (free launch)</color>" : "<color=" + Cyan + ">" + m.id + "</color> " + m.displayName)
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
            img.color = picked ? UIKit.T.teal : (scheduled.HasValue && scheduled.Value.Date == d.Date) ? UIKit.T.orange : ok ? UIKit.T.chip : new Color(1, 1, 1, 0.04f);
            var t = b.GetComponentInChildren<TMP_Text>(); t.text = d.Day.ToString(); t.color = ok ? UIKit.T.ink : UIKit.T.dim;
            b.interactable = ok;
        }
        deployDate.text = pickDate.HasValue ? "Launch date: <b>" + pickDate.Value.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture) + "</b>" : "";
    }

    void ConfirmDeploy()
    {
        if (!pickDate.HasValue || DeploymentManager.Instance == null) return;
        long cost = DeployCost;
        if (!Agency.TrySpend(cost)) { Toast("Not enough money to build this rocket.", true); return; }
        SaveRocket();
        string nm = nameInput.text;
        DeploymentManager.Instance.Schedule(nm, design, Mission, pickDate.Value);
        deployDialog.SetActive(false);
        Toast("\"" + nm + "\" is on the launch pad. Launch " + pickDate.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + "   (-$" + UIKit.Money(cost) + ")");
    }

    // =============================================================== refresh
    void RefreshAll()
    {
        if (!built) return;
        ValidateSel();
        for (int i = 0; i < chipImages.Count; i++) chipImages[i].color = i == catIdx ? Accent : UIKit.T.chip;
        infoSeg.Select(infoView == 0 ? 0 : infoView == 2 ? 1 : -1);
        bpBtnImg.color = infoView == 3 ? UIKit.Wash(UIKit.T.blue, 0.65f) : UIKit.Wash(UIKit.T.blue, 0.35f);
        buyBtnImg.color = infoView == 4 ? UIKit.Wash(UIKit.T.purple, 0.65f) : UIKit.Wash(UIKit.T.purple, 0.35f);
        for (int i = 0; i < infoViews.Length; i++) infoViews[i].SetActive(i == infoView);
        undoBtn.interactable = hIdx > 0;
        redoBtn.interactable = hIdx < history.Count - 1;
        deleteBtn.interactable = !sel.IsNone;

        report = RocketStats.Evaluate(design, library, Mission);
        RefreshGrid();
        RefreshHierarchy();
        RefreshLists();
        RefreshTexts();
        RefreshCard();
        RefreshPaint();
        ((Image)deployBtn.targetGraphic).color = report.valid ? UIKit.T.green : UIKit.T.chip;

        Bounds b;
        GameObject rocket = mode == DesignerMode.Rocket ? RocketAssembler.Build(design, library, out b, true) : BuildSatellite(out b);
        preview.Show(rocket, b);
        RocketAssembler.Highlight(rocket, sel, library.outlineMaterial, Mathf.Max(0.04f, preview.ViewDistance * 0.006f));
        PlayerPrefs.SetString(SaveKey, design.ToJson());
    }

    // The satellite designer shows just the payload and the equipment around it (the rocket hides the payload in its fairing).
    GameObject BuildSatellite(out Bounds bounds)
    {
        var root = new GameObject("Satellite");
        var mat = library.Livery(design.livery);
        GameObject Spawn(PartInfo p, Vector3 pos, Quaternion rot, SelKind kind, int index)
        {
            var prefab = library.Prefab(p.id);
            if (prefab == null) return null;
            var go = Instantiate(prefab, root.transform);
            go.transform.localPosition = pos; go.transform.localRotation = rot;
            var pr = go.AddComponent<PartRef>(); pr.kind = kind; pr.index = index;
            if (mat != null) foreach (var rd in go.GetComponentsInChildren<MeshRenderer>()) rd.sharedMaterial = mat;
            return go;
        }
        float r = 0.6f;
        var pay = library.Get(design.payloadId);
        if (pay != null) { Spawn(pay, Vector3.zero, Quaternion.identity, SelKind.Payload, 0); r = Mathf.Max(r, pay.Radius); }
        for (int i = 0; i < design.equipment.Count; i++)
        {
            var e = library.Get(design.equipment[i]); if (e == null) continue;
            float a = 360f * i / Mathf.Max(1, design.equipment.Count) + 20f;
            Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            Spawn(e, dir * (r + 0.04f) + Vector3.up * (pay != null ? Mathf.Max(0f, pay.heightM * 0.3f) : 0f), Quaternion.FromToRotation(Vector3.forward, dir), SelKind.Equip, i);
        }
        var rends = root.GetComponentsInChildren<Renderer>();
        bounds = rends.Length > 0 ? rends[0].bounds : new Bounds(Vector3.up, Vector3.one * 2f);
        foreach (var rd in rends) bounds.Encapsulate(rd.bounds);
        return root;
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

    static string Describe(PartInfo p)
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

    bool IsEquipped(PartInfo p)
    {
        switch (RocketRules.SlotOf(p))
        {
            case PartSlot.Booster: return design.boosterId == p.id;
            case PartSlot.Fin: return design.finId == p.id;
            case PartSlot.Payload: return design.payloadId == p.id;
            default: return false;
        }
    }

    // ---- the parts grid: icon, name, price, EQUIP ----
    void RefreshGrid()
    {
        if (mode == DesignerMode.Rocket) { RefreshGridRocket(); return; }
        UIKit.Clear(gridContent);
        var cats = Cats;
        if (catIdx >= cats.Length) catIdx = 0;
        string cat = cats[catIdx].cat;
        foreach (var p in library.Parts.Where(x => x.category == cat).OrderBy(x => PartInfo.SizeRank(x.size)).ThenBy(x => x.costM))
        {
            var part = p;
            RocketRules.CanAdd(design, part, library, out var advisory);
            bool equipped = IsEquipped(part);
            var cell = UIKit.At(gridContent, part.id, 0, 0, 56, 78);
            UIKit.Tint(cell, new Color(1f, 1f, 1f, 0.12f));
            PartIcon(cell, part.id, 5, 3, 46, 36);
            UIKit.TextAt(cell, "Name", 2, 40, 52, 9, part.DisplayName, 5.5f, TextAlignmentOptions.Center, false, UIKit.T.dim).overflowMode = TextOverflowModes.Ellipsis;
            UIKit.TextAt(cell, "Price", 2, 49, 52, 11, "$" + part.costM.ToString("0.#") + " M", 7f, TextAlignmentOptions.Center, true, UIKit.T.priceText);
            var b = UIKit.Btn(cell, equipped ? "Equipped" : "Equip", 4, 62, 48, 13, equipped ? UIKit.T.chip : UIKit.Wash(UIKit.T.green, 0.6f), () => AddPart(part), 6.8f, true, equipped ? UIKit.T.dim : UIKit.T.ink);
            b.interactable = !equipped;
            Tip(cell.gameObject, Describe(part) + (string.IsNullOrEmpty(advisory) ? "" : "\n<color=" + Warn + ">" + advisory + "</color>"));
        }
    }

    // ---- build order (drag rows to reorder) ----
    void RefreshHierarchy()
    {
        UIKit.Clear(hierList);
        var T = UIKit.T;
        int n = design.stack.Count;
        bool rocket = mode == DesignerMode.Rocket;
        if (rocket)
            for (int r = 0; r < n; r++)
            {
                int si = n - 1 - r;
                var p = library.Get(design.stack[si]);
                if (p == null) continue;
                var row = UIKit.At(hierList, "Row", 0, 0, 100, 17);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 17;
                bool isSel = sel.kind == SelKind.Stack && sel.index == si;
                var img = UIKit.Tint(row, isSel ? T.teal : T.chip);
                var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = img; UIKit.StyleButton(b);
                int idx = si;
                b.onClick.AddListener(() => { sel = Sel.Of(SelKind.Stack, idx); RefreshAll(); });
                row.gameObject.AddComponent<RocketHierarchyRow>().Init(this, si);
                UIKit.TextAt(row, "Grip", 0, 0, 10, 17, "=", 7f, TextAlignmentOptions.Center, false, T.dim);
                PartIcon(row, p.id, 11, 2, 13, 13);
                var t = UIKit.TextAt(row, "Name", 27, 0, 140, 17, p.DisplayName, 7f);
                t.raycastTarget = false;
            }
        if (n > 0 || design.boosterId != null || design.finId != null || design.payloadId != null || design.equipment.Count > 0)
        {
            if (rocket) { AttachedRow("side boosters", design.boosterId, SelKind.Booster, 0, design.boosterCount, -1, 6); AttachedRow("fins", design.finId, SelKind.Fin, 0, design.finCount, 2, 6); }
            AttachedRow("payload", design.payloadId, SelKind.Payload, 0, 0, 0, 0);
            for (int i = 0; i < design.equipment.Count; i++) AttachedRow("equipment", design.equipment[i], SelKind.Equip, i, 0, 0, 0);
        }
        if (hierList.childCount == 0)
        {
            var r = UIKit.At(hierList, "Empty", 0, 0, 100, 50);
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
            var t = UIKit.Text(r, mode == DesignerMode.Rocket ? "Empty rocket.\nPick a category on the right and press EQUIP." : "Empty satellite.\nPick a payload, then add power and comms.", 7f, TextAlignmentOptions.Center, false, T.dim);
            t.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    void AttachedRow(string label, string id, SelKind kind, int index, int count, int min, int max)
    {
        var p = library.Get(id);
        if (p == null) return;
        var row = UIKit.At(hierList, "Attached", 0, 0, 100, 17);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 17;
        bool isSel = sel.kind == kind && (kind == SelKind.Equip ? sel.index == index : true);
        var img = UIKit.Tint(row, isSel ? UIKit.T.teal : new Color(0.3f, 0.7f, 0.95f, 0.18f));
        var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = img; UIKit.StyleButton(b);
        b.onClick.AddListener(() => { sel = Sel.Of(kind, index); RefreshAll(); });
        PartIcon(row, p.id, 3, 2, 13, 13);
        string cnt = count > 0 ? "  x" + count : "";
        bool stepper = kind == SelKind.Booster || kind == SelKind.Fin;
        var t = UIKit.TextAt(row, "Name", 19, 0, 110, 17, p.DisplayName + cnt, 7f); t.raycastTarget = false;
        Tip(row.gameObject, char.ToUpper(label[0]) + label.Substring(1));
        if (stepper)
        {
            AddSmall(row, "-", 31, () => { if (kind == SelKind.Booster) design.boosterCount = Mathf.Max(1, design.boosterCount - 1); else design.finCount = Mathf.Max(2, design.finCount - 1); sel = Sel.Of(kind, 0); Changed(); });
            AddSmall(row, "+", 16, () => { if (kind == SelKind.Booster) design.boosterCount = Mathf.Min(6, design.boosterCount + 1); else design.finCount = Mathf.Min(6, design.finCount + 1); sel = Sel.Of(kind, 0); Changed(); });
        }
    }

    void AddSmall(RectTransform row, string label, float xFromRight, UnityEngine.Events.UnityAction act)
    {
        var s = UIKit.Btn(row, label, 0, 2, 13, 13, UIKit.T.chip, act, 7.5f);
        UIKit.AnchorRight((RectTransform)s.transform, xFromRight, 2f);
    }

    // ---- blueprint lists ----
    float BlueprintPriceM(RocketDemos.Demo d)
    {
        var rep = RocketStats.Evaluate(d.Design(library), library, null);
        return Mathf.Max(1f, Mathf.Round(rep.costM * 0.15f * 10f) / 10f);
    }

    bool DemoOwned(RocketDemos.Demo d) => d == RocketDemos.All[0] || Agency.OwnsBlueprint(d.name);

    void LoadDesign(RocketDesign d, string nm, string note)
    {
        design = RocketDesign.FromJson(d.ToJson());
        EnsureSimple();
        nameInput.text = nm;
        sel = Sel.None;
        Changed();
        Toast(note);
    }

    void ListHeader(Transform list, string text)
    {
        var r = UIKit.At(list, "Header", 0, 0, 100, 11);
        r.gameObject.AddComponent<LayoutElement>().preferredHeight = 11;
        UIKit.Text(r, text, 6.2f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.teal);
    }

    void RefreshLists()
    {
        var T = UIKit.T;
        UIKit.Clear(bpList); UIKit.Clear(buyList);

        // owned blueprints: bought demo designs, then my own saved rockets
        ListHeader(bpList, "Owned blueprints");
        foreach (var dm in RocketDemos.All)
        {
            if (!DemoOwned(dm)) continue;
            var demo = dm;
            var row = UIKit.At(bpList, "Demo", 0, 0, 100, 24);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            var b = UIKit.MakeButton(row, "", new Color(0.3f, 0.7f, 0.95f, 0.18f), () => { LoadDesign(demo.Design(library), demo.name, "Loaded \"" + demo.name + "\"" + (string.IsNullOrEmpty(demo.missionId) ? "" : "  (made for " + demo.missionId + ")")); });
            UIKit.TextAt(row, "T", 4, 2, 150, 10, demo.name, 7f, TextAlignmentOptions.TopLeft);
            var dd = UIKit.TextAt(row, "D", 4, 12, 170, 10, demo.description, 6f, TextAlignmentOptions.TopLeft, false, T.dim); dd.overflowMode = TextOverflowModes.Ellipsis;
            Tip(row.gameObject, "<b>" + demo.name + "</b>\n" + demo.description);
        }
        ListHeader(bpList, "My rockets");
        var all = RocketSaves.All();
        foreach (var sr in all)
        {
            var s = sr;
            var row = UIKit.At(bpList, "Saved", 0, 0, 100, 22);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            UIKit.MakeButton(row, "", T.chip, () => LoadDesign(RocketDesign.FromJson(s.design), s.name, "Loaded \"" + s.name + "\""));
            UIKit.TextAt(row, "T", 4, 2, 150, 10, s.name, 7f, TextAlignmentOptions.TopLeft);
            UIKit.TextAt(row, "I", 4, 12, 150, 10, "$" + s.costM.ToString("0.#") + "M  " + s.launchKg.ToString("N0") + " kg  " + (s.valid ? "<color=" + Good + ">ready</color>" : "<color=" + Warn + ">draft</color>"), 6f, TextAlignmentOptions.TopLeft, false, T.dim);
            AddSmall(row, "x", 3, () => { RocketSaves.Delete(s.name); RefreshLists(); });
        }
        if (all.Count == 0)
        {
            var r = UIKit.At(bpList, "Empty", 0, 0, 100, 30);
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            UIKit.Text(r, "No saved rockets yet. Name it and press SAVE.", 6.5f, TextAlignmentOptions.Center, false, T.dim).textWrappingMode = TextWrappingModes.Normal;
        }

        // shop
        ListHeader(buyList, "Buy blueprints  ·  you have $" + UIKit.Money(Agency.Money));
        bool any = false;
        foreach (var dm in RocketDemos.All)
        {
            if (DemoOwned(dm)) continue;
            any = true;
            var demo = dm;
            float price = BlueprintPriceM(demo);
            var row = UIKit.At(buyList, "Shop", 0, 0, 100, 26);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            UIKit.Tint(row, T.chip);
            UIKit.TextAt(row, "T", 4, 2, 112, 10, demo.name, 7f, TextAlignmentOptions.TopLeft);
            var dd = UIKit.TextAt(row, "D", 4, 13, 112, 10, demo.description, 6f, TextAlignmentOptions.TopLeft, false, T.dim); dd.overflowMode = TextOverflowModes.Ellipsis;
            var buy = UIKit.Btn(row, "Buy  $" + price.ToString("0.#") + " M", 0, 4, 58, 18, UIKit.Wash(T.purple, 0.4f), () =>
            {
                if (!Agency.TrySpend((long)(price * 1_000_000f))) { Toast("Not enough money for this blueprint.", true); return; }
                Agency.AddBlueprint(demo.name);
                Toast("Blueprint bought: " + demo.name);
                RefreshLists();
            }, 6.5f, true, new Color(0.9f, 0.7f, 1f, 1f));
            UIKit.AnchorRight((RectTransform)buy.transform, 3, 4);
            Tip(row.gameObject, "<b>" + demo.name + "</b>\n" + demo.description);
        }
        if (!any)
        {
            var r = UIKit.At(buyList, "Empty", 0, 0, 100, 30);
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            UIKit.Text(r, "You own every blueprint.", 6.5f, TextAlignmentOptions.Center, false, T.dim);
        }
    }

    void RefreshCard()
    {
        var p = sel.IsNone ? null : SelPart();
        selCard.SetActive(p != null);
        if (p == null) return;
        if (mode == DesignerMode.Rocket) { cardText.text = Compact(p) + (sel.kind == SelKind.Booster ? "\n<color=" + Cyan + ">x" + design.boosterCount + "</color>" : sel.kind == SelKind.Fin ? "\n<color=" + Cyan + ">x" + design.finCount + "</color>" : ""); return; }
        string extra = sel.kind == SelKind.Booster ? "  <color=" + Cyan + ">x" + design.boosterCount + "</color>" : sel.kind == SelKind.Fin ? "  <color=" + Cyan + ">x" + design.finCount + "</color>" : "";
        cardText.text = Describe(p) + extra;
    }

    void RefreshTexts()
    {
        var m = Mission;
        missionLabel.text = m == null ? "<color=" + Soft + ">No mission</color>" : "<color=" + Cyan + ">" + m.id + "</color> " + m.displayName;
        var r = report;

        var s = new StringBuilder();
        s.AppendLine("Height <b>" + r.heightM.ToString("0.0") + " m</b>  Width <b>" + r.widthM.ToString("0.0") + " m</b>");
        s.AppendLine("Launch mass <b>" + r.launchKg.ToString("N0") + " kg</b>");
        s.AppendLine("<color=" + Soft + ">dry " + r.dryKg.ToString("N0") + "  fuel " + r.fuelKg.ToString("N0") + "  payload " + r.payloadKg.ToString("N0") + "</color>");
        s.AppendLine("Thrust <b>" + r.thrustKn.ToString("N0") + " kN</b>   Liftoff TWR <b>" + r.liftoffTwr.ToString("0.00") + "</b>");
        s.AppendLine("Delta-v <b>" + r.dvTotal.ToString("N0") + " m/s</b>");
        for (int i = 0; i < r.stages.Count; i++)
            if (r.stages[i].parts.Count > 0) s.AppendLine("<color=" + Soft + ">stage " + (i + 1) + ": " + r.stages[i].dv.ToString("N0") + " m/s, TWR " + r.stages[i].twr.ToString("0.0") + "</color>");
        s.AppendLine("Cost <b>$" + r.costM.ToString("0.0") + "M</b>");
        s.AppendLine("Power " + r.powerSupplyW.ToString("0") + " W made, " + r.powerDemandW.ToString("0") + " W used");
        s.AppendLine("Comms <b>" + r.commMbps.ToString("0.##") + " Mbps</b>");
        statsText.text = s.ToString();
        if (mode == DesignerMode.Rocket) UpdateStatsGrid();

        var c = new StringBuilder();
        c.AppendLine(r.valid ? "<color=" + Good + "><b>READY TO DEPLOY</b></color>" : "<color=" + Bad + "><b>NOT READY</b></color>");
        if (m == null) c.AppendLine("<color=" + Soft + ">No active contract. Accept one in CONTRACTS to check your rocket against it.</color>");
        foreach (var ch in r.checks)
        {
            string col = ch.sev == Sev.Ok ? Good : ch.sev == Sev.Warn ? Warn : Bad;
            string tag = ch.sev == Sev.Ok ? "OK" : ch.sev == Sev.Warn ? "!" : "X";
            c.AppendLine("<color=" + col + ">[" + tag + "]</color> " + ch.text);
        }
        if (r.checks.Count == 0) c.AppendLine("<color=" + Soft + ">Add parts to see checks.</color>");
        checksText.text = c.ToString();

        summaryText.text = "$" + r.costM.ToString("0.0") + "M  " + (r.launchKg / 1000f).ToString("0.0") + " t  " + (r.valid ? "<color=" + Good + ">ready</color>" : "<color=" + Warn + ">not ready</color>");
    }

    // =============================================================== simple builder (rocket mode)
    SimpleBuild Simple => design.simple ?? (design.simple = new SimpleBuild());

    /// <summary>Older designs only have a part stack: work out the simple picks from it (a rocket made here is always simple).</summary>
    void EnsureSimple()
    {
        if (mode != DesignerMode.Rocket) return;
        if (design.useSimple) return;
        if (design.stack.Count > 0) SimpleBuilder.Infer(design, library);
        else { design.useSimple = true; design.simple = design.simple ?? new SimpleBuild(); }
    }

    void SetSimple(System.Action<SimpleBuild> edit, string toast)
    {
        EnsureSimple();
        edit(Simple);
        SimpleBuilder.Apply(design, library);
        sel = Sel.None;
        Changed();
        var mismatch = report != null ? report.checks.FirstOrDefault(c => c.sev == Sev.Error && c.text.StartsWith("Size mismatch")) : null;
        if (mismatch != null) Toast("These parts are different sizes and cannot be joined: pick tanks and engines of the same size.", true);
        else Toast(toast);
    }

    void EquipSimple(PartInfo p)
    {
        switch (p.category)
        {
            case "Head": SetSimple(s => s.headId = p.id, p.DisplayName + " fitted"); break;
            case "Payload": SetSimple(s => s.payloadId = p.id, p.DisplayName + " loaded"); break;
            case "Tank": if (subTab == 0) SetSimple(s => s.tankId = p.id, p.DisplayName + " fitted"); else SetSimple(s => s.upperTankId = p.id, p.DisplayName + " fitted to the upper stage"); break;
            case "Engine": if (subTab == 0) SetSimple(s => s.engineId = p.id, p.DisplayName + " fitted"); else SetSimple(s => s.upperEngineId = p.id, p.DisplayName + " fitted to the upper stage"); break;
            case "Booster": SetSimple(s => s.boosterId = p.id, p.DisplayName + "s fitted"); break;
            case "Structure": if (p.id.StartsWith("Fin_")) SetSimple(s => s.finId = p.id, p.DisplayName + "s fitted"); break;
            default:                                                    // recovery, power, comms, thrusters: equip / remove, no visual change
                bool has = Simple.extras.Contains(p.id);
                if (!has && Simple.extras.Count >= SimpleBuilder.MaxExtras) { Toast("Only " + SimpleBuilder.MaxExtras + " extras fit.", true); return; }
                SetSimple(s => { if (has) s.extras.Remove(p.id); else s.extras.Add(p.id); }, p.DisplayName + (has ? " removed" : " equipped"));
                break;
        }
    }

    void DeleteSimple()
    {
        var p = SelPart();
        if (p == null) { sel = Sel.None; return; }
        int stage = 0;
        if (sel.kind == SelKind.Stack) for (int i = 0; i < sel.index && i < design.stack.Count; i++) if (design.stack[i].StartsWith("Dec_")) stage++;
        switch (sel.kind)
        {
            case SelKind.Booster: SetSimple(s => s.boosterId = null, "Boosters removed"); return;
            case SelKind.Fin: SetSimple(s => s.finId = null, "Fins removed"); return;
            case SelKind.Payload: SetSimple(s => s.payloadId = null, "Cargo removed"); return;
            case SelKind.Equip: { string id = p.id; SetSimple(s => s.extras.Remove(id), "Extra removed"); return; }
        }
        if (RocketRules.IsHead(p)) SetSimple(s => s.headId = null, "Head removed");
        else if (RocketRules.IsEngine(p)) { if (stage == 0) SetSimple(s => s.engineId = null, "Engine removed"); else SetSimple(s => s.upperEngineId = null, "Upper engine removed"); }
        else if (p.category == "Tank") { if (stage == 0) SetSimple(s => s.tankId = null, "Tanks removed"); else SetSimple(s => s.upperTankId = null, "Upper tanks removed"); }
        else { sel = Sel.None; Toast(p.DisplayName + " is added automatically."); RefreshAll(); }
    }

    bool SimpleHas(PartInfo p)
    {
        var s = Simple;
        switch (p.category)
        {
            case "Head": return s.headId == p.id;
            case "Payload": return s.payloadId == p.id;
            case "Tank": return (subTab == 0 ? s.tankId : s.upperTankId) == p.id;
            case "Engine": return (subTab == 0 ? s.engineId : s.upperEngineId) == p.id;
            case "Booster": return s.boosterId == p.id;
            case "Structure": return s.finId == p.id;
            default: return s.extras.Contains(p.id);
        }
    }

    // ---- compact text: rating line + a few facts (used by the cell tooltips and the selected-part caption) ----
    static string RatingColor(int r) => r >= 95 ? Good : r >= 85 ? Cyan : r >= 78 ? Warn : Bad;

    static string KeyStat(PartInfo p)
    {
        switch (p.category)
        {
            case "Engine": return (p.thrustKn > 0 ? p.thrustKn : p.thrustVacKn).ToString("N0") + " kN";
            case "Tank": return (p.fuelKg / 1000f).ToString("0.#") + " t fuel";
            case "Head": return p.payloadMaxKg > 0 ? p.payloadMaxKg.ToString("N0") + " kg" : p.crew > 0 ? "crew " + p.crew : "no cargo";
            case "Payload": return p.massKg.ToString("N0") + " kg";
            case "Booster": return "+" + p.thrustKn.ToString("N0") + " kN";
            case "Power": return p.powerW.ToString("0") + " W";
            case "Comms": return p.dataMbps >= 1f ? p.dataMbps.ToString("0.#") + " Mbps" : "low data";
            case "Recovery": return p.id.Contains("Parachute") ? "soft landing" : p.id.Contains("Leg") ? "legs" : "steering";
            case "Thruster": return p.id == "Thr_Ion" ? "efficient" : "steering";
            default: return p.id.StartsWith("Fin_") ? "steadier" : "";
        }
    }

    string Compact(PartInfo p)
    {
        int rt = PartRatings.Rating(p);
        var sb = new StringBuilder();
        sb.Append("<b>" + p.DisplayName + "</b>");
        sb.Append("\n<color=" + RatingColor(rt) + ">Rated " + rt + "/100 · " + PartRatings.Tier(rt) + "</color>");
        switch (p.category)
        {
            case "Engine":
                float burn = SimpleMetrics.BurnKgS(p);
                sb.Append("\nPower " + (p.thrustKn > 0 ? p.thrustKn.ToString("N0") + " kN" : p.thrustVacKn.ToString("N0") + " kN (vacuum only)"));
                sb.Append("\nBurns " + burn.ToString("N0") + " kg/s");
                var ab = PartRatings.AbilityOf(p);
                if (ab != null) sb.Append("\n<color=" + Cyan + ">" + ab + "</color>");
                break;
            case "Tank": sb.Append("\nHolds " + (p.fuelKg / 1000f).ToString("0.#") + " t of fuel"); break;
            case "Head": sb.Append(p.payloadMaxKg > 0 ? "\nCarries up to " + p.payloadMaxKg.ToString("N0") + " kg" : p.crew > 0 ? "\nCarries a crew of " + p.crew : "\nStreamlined, no cargo bay"); break;
            case "Payload": sb.Append("\nWeighs " + p.massKg.ToString("N0") + " kg"); break;
            case "Booster": sb.Append("\nAdds " + p.thrustKn.ToString("N0") + " kN of push\nSaves fuel and steadies flight"); break;
            case "Power": sb.Append("\nMakes " + p.powerW.ToString("0") + " W"); break;
            case "Comms": sb.Append("\nSends " + p.dataMbps.ToString("0.##") + " Mbps"); break;
            case "Recovery": sb.Append(p.id.Contains("Parachute") ? "\nSlows the landing" : "\nHelps a safe touchdown"); break;
            case "Thruster": sb.Append(p.id == "Thr_Ion" ? "\nTiny push, very efficient" : "\nSmall steering jets"); break;
            default: if (p.id.StartsWith("Fin_")) sb.Append("\nKeeps the rocket flying straight"); break;
        }
        sb.Append("\n<color=" + Soft + ">$" + p.costM.ToString("0.##") + " M</color>");
        return sb.ToString();
    }

    // ---- the parts grid of the simple builder ----
    void RefreshGridRocket()
    {
        UIKit.Clear(gridContent); UIKit.Clear(subRow);
        var cats = Cats;
        if (catIdx >= cats.Length) catIdx = 0;
        string cat = cats[catIdx].cat;
        var names = SubTabs[catIdx];
        if (subTab >= names.Length) subTab = 0;

        float segW = names.Length >= 4 ? RW - 12 : names.Length == 3 ? 126f : 112f;
        BuildSubTabs(names, segW);
        BuildStepper();

        var list = new List<(PartInfo part, string label, PartInfo second)>();
        bool noneCell = false; string noneName = "None"; bool noneOn = false;
        var s = Simple;
        System.Func<string, IEnumerable<PartInfo>> byCat = c => library.Parts.Where(x => x.category == c).OrderBy(x => PartInfo.SizeRank(x.size)).ThenBy(x => x.costM);
        switch (cat)
        {
            case "Payload":
                if (subTab == 0) foreach (var x in byCat("Head")) list.Add((x, null, null));
                else { foreach (var x in byCat("Payload")) list.Add((x, null, null)); noneCell = true; noneName = "No cargo"; noneOn = s.payloadId == null; }
                break;
            case "Tank": foreach (var x in byCat("Tank")) list.Add((x, null, null)); break;
            case "Engine":
                foreach (var x in byCat("Engine")) list.Add((x, null, null));
                if (subTab == 1) { noneCell = true; noneName = "No upper stage"; noneOn = s.upperEngineId == null; }
                break;
            case "Outer":
                if (subTab == 0) { foreach (var x in byCat("Booster")) list.Add((x, null, null)); noneCell = true; noneName = "No boosters"; noneOn = s.boosterId == null; }
                else if (subTab == 1) { foreach (var x in library.Parts.Where(f => f.id.StartsWith("Fin_")).OrderBy(f => PartInfo.SizeRank(f.size))) list.Add((x, null, null)); noneCell = true; noneName = "No fins"; noneOn = s.finId == null; }
                else foreach (var c in Combos) { var b = library.Get(c.booster); var f = library.Get(c.fin); if (b != null && f != null) list.Add((b, c.name, f)); }
                break;
            default:
                string[] extraCats = { "Recovery", "Power", "Comms", "Thruster" };
                foreach (var x in byCat(extraCats[subTab])) list.Add((x, null, null));
                break;
        }

        if (noneCell) NoneCell(noneName, noneOn, cat, subTab);
        foreach (var entry in list)
        {
            var part = entry.part;
            bool combo = entry.second != null;
            bool equipped = combo ? s.boosterId == part.id && s.finId == entry.second.id : SimpleHas(part);
            bool toggle = cat == "Extras";
            var cell = UIKit.At(gridContent, part.id, 0, 0, 56, 86);
            UIKit.Tint(cell, equipped ? UIKit.Wash(UIKit.T.green, 0.22f) : new Color(1f, 1f, 1f, 0.12f));
            PartIcon(cell, part.id, 5, 3, 46, 34);
            int rt = PartRatings.Rating(part);
            var shield = UIKit.IconAt(cell, Resources.Load<Sprite>("UIIcons/stat_rating"), 33, 3, 7, 7, ColorFromHex(RatingColor(rt)));
            UIKit.TextAt(cell, "Rt", 40, 2.5f, 15, 8, rt.ToString(), 5.4f, TextAlignmentOptions.MidlineLeft, true, ColorFromHex(RatingColor(rt))).raycastTarget = false;
            UIKit.TextAt(cell, "Name", 2, 37, 52, 8, combo ? entry.label : part.DisplayName, 5.2f, TextAlignmentOptions.Center, false, UIKit.T.dim).overflowMode = TextOverflowModes.Ellipsis;
            UIKit.TextAt(cell, "Stat", 2, 45, 52, 9, combo ? "boost + steady" : KeyStat(part), 5.8f, TextAlignmentOptions.Center, false, UIKit.T.ink).overflowMode = TextOverflowModes.Ellipsis;
            float price = part.costM + (combo ? entry.second.costM * 4f : 0f);
            UIKit.TextAt(cell, "Price", 2, 54, 52, 10, "$" + price.ToString("0.#") + " M", 6.8f, TextAlignmentOptions.Center, true, UIKit.T.priceText);
            string label = toggle ? (equipped ? "Remove" : "Equip") : equipped ? "Equipped" : "Equip";
            var b2 = UIKit.Btn(cell, label, 4, 68, 48, 13, equipped && !toggle ? UIKit.T.chip : UIKit.Wash(equipped ? UIKit.T.red : UIKit.T.green, 0.6f), () =>
            {
                if (combo) SetSimple(sb => { sb.boosterId = part.id; sb.finId = entry.second.id; }, entry.label + " fitted");
                else AddPart(part);
            }, 6.4f, true, equipped && !toggle ? UIKit.T.dim : UIKit.T.ink);
            b2.interactable = toggle || !equipped;
            Tip(cell.gameObject, combo ? "<b>" + entry.label + "</b>\n<color=" + Soft + ">Boosters and fins fitted together.</color>\n" + Compact(part).Split('\n').Skip(1).FirstOrDefault() : Compact(part));
        }
    }

    static readonly Dictionary<string, string> SubIcons = new Dictionary<string, string>
    {
        { "Head", "sub_head" }, { "Cargo", "sub_cargo" }, { "Main stage", "sub_main" }, { "Upper stage", "sub_upper" },
        { "Boosters", "sub_boosters" }, { "Fins", "sub_fins" }, { "Combos", "sub_combo" },
        { "Recovery", "sub_recovery" }, { "Power", "sub_power" }, { "Comms", "sub_comms" }, { "Thrusters", "sub_thrusters" },
    };

    // sub-tabs with a glyph of their own: icon + name, the chosen one lit
    void BuildSubTabs(string[] names, float width)
    {
        var T = UIKit.T;
        var track = UIKit.At(subRow, "Tabs", 0, 0, width, 13);
        UIKit.Tint(track, T.track);
        float seg = (width - 4f) / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var r = UIKit.At(track, names[i], 2 + i * seg, 2, seg, 9);
            var b = UIKit.MakeButton(r, "", i == subTab ? new Color(1f, 1f, 1f, 0.26f) : Color.clear, () => { subTab = idx; RefreshGrid(); RefreshStepper(); });
            string label = names[i];
            bool narrow = seg < 40f;
            float iw = 7f;
            if (SubIcons.TryGetValue(label, out var ic)) UIKit.IconAt(r, Resources.Load<Sprite>("UIIcons/" + ic), narrow ? 2f : 4f, 1f, iw, iw, i == subTab ? T.ink : T.dim);
            var t = UIKit.TextAt(r, "L", (narrow ? 2f : 4f) + iw + 1.5f, 0, seg - iw - 6f, 9, label, 5.4f, TextAlignmentOptions.MidlineLeft, true, i == subTab ? T.ink : T.dim);
            t.raycastTarget = false; t.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    void NoneCell(string name, bool on, string cat, int sub)
    {
        var cell = UIKit.At(gridContent, "None", 0, 0, 56, 86);
        UIKit.Tint(cell, on ? UIKit.Wash(UIKit.T.green, 0.22f) : new Color(1f, 1f, 1f, 0.06f));
        UIKit.TextAt(cell, "X", 2, 8, 52, 26, "-", 18f, TextAlignmentOptions.Center, true, UIKit.T.dim);
        UIKit.TextAt(cell, "Name", 2, 40, 52, 10, name, 5.6f, TextAlignmentOptions.Center, false, UIKit.T.dim);
        var b = UIKit.Btn(cell, on ? "Chosen" : "Select", 4, 68, 48, 13, on ? UIKit.T.chip : UIKit.Wash(UIKit.T.green, 0.6f), () =>
        {
            if (cat == "Payload") SetSimple(s => s.payloadId = null, "No cargo");
            else if (cat == "Engine") SetSimple(s => s.upperEngineId = null, "Single stage");
            else if (cat == "Outer" && sub == 0) SetSimple(s => s.boosterId = null, "No boosters");
            else if (cat == "Outer") SetSimple(s => s.finId = null, "No fins");
        }, 6.4f, true, on ? UIKit.T.dim : UIKit.T.ink);
        b.interactable = !on;
    }

    static Color ColorFromHex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    // count stepper (tanks, boosters, fins), shown at the right end of the sub-tab row
    void BuildStepper()
    {
        string cat = Cats[catIdx].cat;
        bool show = cat == "Tank" || cat == "Outer";
        if (!show) { countBox = null; countLabel = null; return; }
        var box = UIKit.At(subRow, "Count", RW - 12 - 62, 0, 62, 13);
        countBox = box.gameObject;
        UIKit.Btn(box, "-", 0, 0, 14, 13, UIKit.T.chip, () => ChangeCount(-1), 7.5f);
        countLabel = UIKit.TextAt(box, "N", 15, 0, 32, 13, "", 6.4f, TextAlignmentOptions.Center, true);
        UIKit.Btn(box, "+", 48, 0, 14, 13, UIKit.T.chip, () => ChangeCount(1), 7.5f);
        Tip(box.gameObject, "How many\n<color=" + Soft + ">more tanks hold more fuel, more boosters push harder</color>");
        RefreshStepper();
    }

    int CurrentCount()
    {
        var s = Simple; string cat = Cats[catIdx].cat;
        if (cat == "Tank") return subTab == 0 ? s.tankCount : s.upperTankCount;
        return subTab == 1 ? s.finCount : s.boosterCount;
    }

    void ChangeCount(int dlt)
    {
        string cat = Cats[catIdx].cat;
        SetSimple(s =>
        {
            if (cat == "Tank") { if (subTab == 0) s.tankCount = Mathf.Clamp(s.tankCount + dlt, 1, SimpleBuilder.MaxTanks); else s.upperTankCount = Mathf.Clamp(s.upperTankCount + dlt, 1, SimpleBuilder.MaxTanks); }
            else if (subTab == 1) s.finCount = Mathf.Clamp(s.finCount + dlt, 2, SimpleBuilder.MaxOuter);
            else s.boosterCount = Mathf.Clamp(s.boosterCount + dlt, 1, SimpleBuilder.MaxOuter);
        }, "Count " + (CurrentCount() + dlt));
    }

    void RefreshStepper()
    {
        if (countLabel == null) return;
        string cat = Cats[catIdx].cat;
        countLabel.text = "x " + CurrentCount() + (cat == "Tank" ? (subTab == 0 ? " tanks" : " tanks") : subTab == 1 ? " fins" : " boosters");
    }

    // ---- paint menu beside the rocket ----
    void BuildPaintCard(RectTransform root, float x)
    {
        paintCard = UIKit.Card(root, "Paint Card", x, CardY, 30f, 24f + 8 * 17f);
        UIKit.TextAt(paintCard, "Title", 0, 3, 30, 9, "Paint", 5.8f, TextAlignmentOptions.Center, true, UIKit.T.dim);
        paintRings = new Image[8];
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            float y = 15f + i * 17f;
            var ring = UIKit.At(paintCard, "Ring" + i, 3, y - 1.5f, 24, 18);
            paintRings[i] = UIKit.Tint(ring, Color.clear);
            var sw = UIKit.At(paintCard, LiveryNames[i], 5, y, 20, 15);
            var b = UIKit.MakeButton(sw, "", LiveryColors[i], () => { design.livery = idx; Changed(); Toast("Paint: " + LiveryNames[idx]); });
            Tip(sw.gameObject, "<b>" + LiveryNames[i] + "</b>\n<color=" + Soft + ">Paint the rocket</color>");
        }
    }

    void RefreshPaint()
    {
        if (paintRings == null) return;
        for (int i = 0; i < paintRings.Length; i++) paintRings[i].color = i == design.livery ? new Color(1f, 1f, 1f, 0.85f) : Color.clear;
    }

    // ---- the stats grid: orbit reach banner and eight icon cells ----
    readonly TooltipTrigger[] statTips = new TooltipTrigger[8];
    TooltipTrigger orbitTip;

    void BuildStatsGrid(RectTransform parent, float w, float h)
    {
        var T = UIKit.T;
        statsGrid = UIKit.At(parent, "Stats Grid", 0, 0, w, h);
        // banner: where can this rocket go?
        var banner = UIKit.At(statsGrid, "Orbit", 0, 0, w, 27);
        UIKit.Tint(banner, T.well);
        orbitIcon = UIKit.IconAt(banner, Resources.Load<Sprite>("UIIcons/stat_orbit"), 3, 2, 11, 11, T.ink);
        orbitText = UIKit.TextAt(banner, "Reach", 17, 1.5f, w - 20, 12, "", 7.2f, TextAlignmentOptions.MidlineLeft, true);
        orbitPips = new Image[4];
        string[] pn = { "Hop", "Earth", "Moon", "Mars" };
        float sw = (w - 8f) / 4f;
        for (int i = 0; i < 4; i++)
        {
            orbitPips[i] = UIKit.Tint(UIKit.At(banner, "Pip" + i, 4 + i * sw, 15.5f, sw - 2f, 3f), T.chip);
            UIKit.TextAt(banner, "PipL" + i, 4 + i * sw, 19f, sw - 2f, 7f, pn[i], 4.6f, TextAlignmentOptions.Center, false, T.dim);
        }
        orbitTip = banner.gameObject.AddComponent<TooltipTrigger>();

        string[] labels = { "Total mass", "Can lift", "Fuel", "Burn rate", "Payload", "Stability", "Cost", "Rated" };
        string[] icons = { "stat_mass", "stat_lift", "stat_fuel", "icon_engine", "stat_payload", "stat_stability", "stat_cost", "stat_rating" };
        float cw = (w - 6f) / 4f, ch = 25f;
        for (int i = 0; i < 8; i++)
        {
            float cx = (i % 4) * (cw + 2f), cy = 29f + (i / 4) * (ch + 2f);
            var cell = UIKit.At(statsGrid, labels[i], cx, cy, cw, ch);
            UIKit.Tint(cell, new Color(1f, 1f, 1f, 0.10f));
            statIcon[i] = UIKit.IconAt(cell, Resources.Load<Sprite>("UIIcons/" + icons[i]), 2.5f, 2.5f, 8, 8, T.ink);
            UIKit.TextAt(cell, "L", 12, 1.5f, cw - 13, 10, labels[i], 4.8f, TextAlignmentOptions.MidlineLeft, false, T.dim).raycastTarget = false;
            statValue[i] = UIKit.TextAt(cell, "V", 2, 11.5f, cw - 4, 12, "", 7f, TextAlignmentOptions.MidlineLeft, true);
            statValue[i].raycastTarget = false; statValue[i].overflowMode = TextOverflowModes.Ellipsis;
            statTips[i] = cell.gameObject.AddComponent<TooltipTrigger>();
        }
    }

    void UpdateStatsGrid()
    {
        var r = report;
        if (r == null || statValue[0] == null) return;
        var pay = library.Get(design.payloadId);
        System.Action<int, string, string, string> set = (i, val, col, tip) =>
        {
            statValue[i].text = val;
            statValue[i].color = col == null ? UIKit.T.ink : ColorFromHex(col);
            statTips[i].text = tip;
        };
        set(0, (r.launchKg / 1000f).ToString("0.0") + " t", null, "<b>Total mass</b>\nThe whole rocket with fuel and cargo.");
        bool canLift = r.liftKg >= r.launchKg;
        set(1, (r.liftKg / 1000f).ToString("0.0") + " t", canLift ? Good : Bad, "<b>Can lift</b>\nThe heaviest rocket these engines can raise.\n<color=" + (canLift ? Good : Bad) + ">" + (canLift ? "Enough for this rocket." : "Too weak: add thrust or cut weight.") + "</color>");
        set(2, (r.fuelKg / 1000f).ToString("0.#") + " t", null, "<b>Fuel capacity</b>\nAll the fuel the tanks and boosters hold.");
        set(3, r.burnKgS.ToString("N0") + " kg/s", null, "<b>Burn rate</b>\nFuel the first stage burns each second." + (r.fuelSavePct > 0.05f ? "\n<color=" + Good + ">Fuel use -" + r.fuelSavePct.ToString("0.#") + "% from boosters and fins</color>" : ""));
        bool over = pay != null && r.payloadCapKg > 0 && pay.massKg > r.payloadCapKg;
        set(4, r.payloadCapKg > 0 ? r.payloadCapKg.ToString("N0") + " kg" : "none", over ? Bad : null, "<b>Payload capacity</b>\nWhat the head can carry." + (pay != null ? "\nCargo: " + pay.massKg.ToString("N0") + " kg" : ""));
        string sw = SimpleMetrics.StabilityWord(r.stability);
        set(5, sw, r.stability < 40f ? Bad : r.stability < 70f ? Warn : Good, "<b>Stability</b>\nFins and an even set of boosters keep it flying straight.");
        set(6, "$" + r.costM.ToString("0.#") + "M", null, "<b>Cost</b>\nPrice of every part chosen.");
        int rt = Mathf.RoundToInt(r.reliability);
        set(7, rt > 0 ? rt + "/100" : "-", rt > 0 ? RatingColor(rt) : null, "<b>Rated</b>\nHow well tested the parts are.\n<color=" + RatingColor(rt) + ">" + PartRatings.Tier(rt) + "</color>");

        // orbit reach
        int lvl = r.orbitLevel;
        bool nothing = design.stack.Count == 0;
        orbitText.text = nothing ? "Add parts to see how far it goes" : r.liftoffTwr < 1f && r.thrustKn > 0 ? "Too heavy to lift off" : lvl == 0 ? "Cannot reach space yet" : "Reaches  " + SimpleMetrics.OrbitNames[lvl];
        orbitText.color = nothing ? UIKit.T.dim : lvl == 0 ? ColorFromHex(Bad) : lvl >= 3 ? ColorFromHex(Cyan) : ColorFromHex(Good);
        for (int i = 0; i < 4; i++) orbitPips[i].color = i < lvl ? (lvl >= 3 ? UIKit.T.teal : UIKit.T.green) : UIKit.T.chip;
        string next = lvl >= 4 ? "Nothing further to reach." : "To reach " + SimpleMetrics.OrbitNames[lvl + 1] + " it needs more fuel or a stronger upper stage.";
        orbitTip.text = "<b>Orbit reach</b>\nSuborbital hop, Earth orbit, the Moon, Mars.\n<color=" + Soft + ">" + next + "</color>";
    }
}

/// <summary>A row in the build order list that can be dragged up or down to re-order the stack.</summary>
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
