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

    static readonly (string label, string cat)[] RocketCats =
    {
        ("Engines", "Engine"), ("Fuel tanks", "Tank"), ("Rocket heads", "Head"), ("Side boosters", "Booster"),
        ("Thrusters", "Thruster"), ("Structure & control", "Structure"), ("Landing & recovery", "Recovery"),
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
    int catIdx, infoView;                 // infoView: 0 stats, 1 order, 2 checks, 3 blueprints, 4 buy blueprints
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
    RectTransform chipRow, gridContent;
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
        history.Clear(); hIdx = -1; Commit();
        sel = Sel.None; catIdx = 0; infoView = 0;
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
        var auto = UIKit.Btn(frame, "Auto order", 92, 4, 40, 12, T.chip, AutoOrderClicked, 6f);
        Tip(auto.gameObject, "Fix the part order automatically\n<color=" + Soft + ">stages, decouplers, adapters, head on top</color>");

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
        infoSeg = new Segmented(card, 5, 158, 112, 13, new[] { "Stats", "Order", "Checks" }, 0, idx => { infoView = idx; RefreshAll(); });
        summaryText = UIKit.TextAt(card, "Summary", 120, 158, 65, 13, "", 6f, TextAlignmentOptions.MidlineRight);

        const float ay = 174f, ah = 82f;
        infoViews = new GameObject[5];
        var vStats = UIKit.At(card, "StatsView", 5, ay, 180, ah);
        statsText = ScrollText(vStats, 180, ah);
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
        chipRow = UIKit.At(card, "Categories", 6, 26, RW - 12, 20);
        gridContent = UIKit.ScrollList(card, "Parts", 6, 50, RW - 12, CardH - 56, UIKit.T.well, 3f, true, new Vector2(56, 78), 3);
        ((GridLayoutGroup)gridContent.GetComponent<GridLayoutGroup>()).padding = new RectOffset(3, 3, 3, 3);
    }

    void ApplyMode()
    {
        bool rocket = mode == DesignerMode.Rocket;
        titleText.text = rocket ? "Rocket Designer" : "Satellite Designer";
        titleBar.color = Accent;
        previewInput.panMode = false; SetPan(false);
        nameInput.placeholder.GetComponent<TMP_Text>().text = rocket ? "Rocket name" : "Rocket name";
        // category chips
        UIKit.Clear(chipRow);
        chipImages.Clear();
        var cats = Cats;
        float cw = Mathf.Min(26f, (RW - 12 - 2f * (cats.Length - 1)) / cats.Length);
        for (int i = 0; i < cats.Length; i++)
        {
            int idx = i;
            string cat = cats[i].cat;
            var r = UIKit.At(chipRow, cats[i].label, i * (cw + 2f), 0, cw, 20);
            var b = UIKit.MakeButton(r, "", UIKit.T.chip, () => { catIdx = idx; RefreshAll(); });
            var first = library.Parts.FirstOrDefault(p => p.category == cat);
            if (first != null) PartIcon(r, first.id, 2, 2, cw - 4, 16);
            else UIKit.Text(r, cats[i].label.Substring(0, 1), 8f);
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
        RocketRules.CanAdd(design, p, library, out var note);
        sel = RocketRules.Add(design, p, library);
        Changed();
        Toast(string.IsNullOrEmpty(note) ? p.DisplayName + " equipped" : note, !string.IsNullOrEmpty(note));
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
        infoSeg.Select(infoView < 3 ? infoView : -1);
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
        var rep = RocketStats.Evaluate(d.design, library, null);
        return Mathf.Max(1f, Mathf.Round(rep.costM * 0.15f * 10f) / 10f);
    }

    bool DemoOwned(RocketDemos.Demo d) => d == RocketDemos.All[0] || Agency.OwnsBlueprint(d.name);

    void LoadDesign(RocketDesign d, string nm, string note)
    {
        design = RocketDesign.FromJson(d.ToJson());
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
            var b = UIKit.MakeButton(row, "", new Color(0.3f, 0.7f, 0.95f, 0.18f), () => { LoadDesign(demo.design, demo.name, "Loaded \"" + demo.name + "\"" + (string.IsNullOrEmpty(demo.missionId) ? "" : "  (made for " + demo.missionId + ")")); });
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
