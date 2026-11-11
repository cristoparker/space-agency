using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu (TV-1) and "Register Your Agency" (TV-2) from the reference layouts.
/// Title: Play / Load Game / Settings / Exit.  Register: agency name, type, stats and the layer based logo creator.
/// The finished agency is stored in <see cref="Agency"/> and the Game scene is loaded.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public string gameScene = "2. Game";

    RectTransform stage;
    GameObject titleScreen, registerScreen, settingsScreen;
    TMP_Text hint, pointsLeft, typeNote;
    TMP_InputField nameInput;
    readonly TMP_Text[] statValue = new TMP_Text[4];
    readonly Image[] typeChips = new Image[4];
    RectTransform logoPreview, layerList;
    Button loadBtn;

    string agencyName = "";
    int type = 1;
    readonly int[] stats = { 1, 1, 1, 1 };
    LogoDesign logo;
    float hintTimer;

    int PointsLeft => Agency.StatPoints - (stats[0] + stats[1] + stats[2] + stats[3] - 4);

    void Start()
    {
        if (UITheme.Current == null) { Debug.LogError("MainMenuUI: Resources/UITheme is missing. Run Tools / Space Agency / Setup Game UI."); return; }
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.06f, 0.11f, 1f); }   // the glass blurs this, so keep it calm

        MenuBackdrop.Create(cam);                                       // the game's own scenery, drifting slowly behind the menu (drawn by the camera so the glass can blur it)

        var st = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
        st.SetParent(transform, false);
        st.anchorMin = st.anchorMax = new Vector2(0.5f, 0.5f); st.pivot = new Vector2(0.5f, 0.5f);
        st.sizeDelta = new Vector2(640f, 360f); st.anchoredPosition = Vector2.zero;
        stage = st;

        BuildTitle();
        BuildRegister();
        BuildSettings();
        hint = UIKit.TextAt(stage, "Hint", 0, 336, 640, 14, "", 8f, TextAlignmentOptions.Center, false, new Color(1f, 0.7f, 0.5f, 1f));
        Show(titleScreen);
    }

    static void Glow(RectTransform parent, Vector2 pos, float size, Color c)
    {
        var r = new GameObject("Glow", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(size, size); r.anchoredPosition = pos;
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = UIKit.RadialSprite(); i.color = c; i.raycastTarget = false;
    }

    void Update()
    {
        if (hintTimer > 0f) { hintTimer -= Time.unscaledDeltaTime; if (hintTimer <= 0f && hint != null) hint.text = ""; }
    }

    void Say(string s) { hint.text = s; hintTimer = 3f; }

    void Show(GameObject screen)
    {
        titleScreen.SetActive(screen == titleScreen);
        registerScreen.SetActive(screen == registerScreen);
        settingsScreen.SetActive(screen == settingsScreen);
        if (screen == titleScreen && loadBtn != null) loadBtn.interactable = Agency.Registered;
    }

    GameObject Screen_(string name)
    {
        var r = UIKit.At(stage, name, 0, 0, 640, 360);
        return r.gameObject;
    }

    // =============================================================== game logo
    /// <summary>
    /// The game's wordmark, "SPACE MISSION TYCOON": Audiowide (SIL Open Font License, Resources/Fonts) with a vertical colour gradient,
    /// a soft glow behind it and a thin accent rule. Falls back to the theme's heading font if the font asset is missing.
    /// </summary>
    void BuildLogo(Transform s)
    {
        var display = Resources.Load<TMP_FontAsset>("Fonts/Audiowide SDF");
        if (display != null) display.TryAddCharacters("SPACEMISONTYCO ");     // load the glyphs up front so the first layout has every advance (no uneven gaps)
        TMP_Text Line(string name, float y, string text, float size, float spacing, Color top, Color bottom)
        {
            var t = UIKit.TextAt(s, name, 44, y, 520, size * 1.25f, text, size, TextAlignmentOptions.MidlineLeft, true);
            if (display != null) { t.font = display; t.fontStyle = FontStyles.Normal; }
            t.characterSpacing = spacing;
            t.fontFeatures = new System.Collections.Generic.List<UnityEngine.TextCore.OTL_FeatureTag>();   // no kerning: with wide tracking it made the gaps uneven
            t.ForceMeshUpdate();
            t.enableVertexGradient = true; t.colorGradient = new VertexGradient(top, top, bottom, bottom);
            return t;
        }
        Line("Logo Space Mission", 30f, "SPACE MISSION", 33f, 2f, Color.white, new Color(0.62f, 0.8f, 1f, 1f));
        Line("Logo Tycoon", 66f, "TYCOON", 33f, 36f, new Color(1f, 0.82f, 0.4f, 1f), new Color(1f, 0.5f, 0.12f, 1f));
        // a thin rule and the tagline under the wordmark
        var rule = UIKit.At(s, "Logo Rule", 46f, 108f, 150f, 1.5f);
        UIKit.Tint(rule, new Color(1f, 0.62f, 0.2f, 0.9f)).raycastTarget = false;
        var tag = UIKit.TextAt(s, "Sub", 46, 112, 460, 14, "Build the agency. Design the rockets. Reach orbit.", 8f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
        tag.characterSpacing = 1.5f;
    }

    // =============================================================== title screen
    void BuildTitle()
    {
        titleScreen = Screen_("Title Screen");
        var s = titleScreen.transform;
        BuildLogo(s);

        // each menu button is one glass capsule (blurred scenery behind it, fully rounded ends) with its label on it: no card, no inner chip
        string[] labels = { "Play", "Load Game", "Settings", "Exit" };
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var r = UIKit.At(s, "Menu " + labels[i], 44, 134 + i * 34, 150, 26);
            var glass = UIKit.Glass(r);                                   // the game's own blurred glass
            var round = r.GetComponent<UIRound>(); round.radius = 13f; round.Force();     // exactly half the height: ends are full half-circles
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = glass; b.onClick.AddListener(() => OnTitle(idx));
            UIKit.StyleButton(b);
            UIHover.Attach(r.gameObject);
            UIKit.Text(r, labels[i], 9f, TextAlignmentOptions.Center, true).raycastTarget = false;
            if (i == 1) loadBtn = b;
        }
        UIKit.TextAt(s, "Version", 40, 336, 200, 12, Agency.Registered ? "Agency: " + Agency.Name : "No agency registered yet", 7f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
    }

    void OnTitle(int i)
    {
        switch (i)
        {
            case 0: Show(registerScreen); break;
            case 1:
                if (!Agency.Registered) { Say("No saved agency yet. Press Play to register one."); return; }
                SceneManager.LoadScene(gameScene);
                break;
            case 2: Show(settingsScreen); break;
            default:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    // =============================================================== settings
    void BuildSettings()
    {
        settingsScreen = Screen_("Settings Screen");
        var card = UIKit.Card(settingsScreen.transform, "Card", 40, 56, 240, 110);
        UIKit.TitleBar(card, "Settings", UIKit.T.teal, 240, () => Show(titleScreen));
        UIKit.TextAt(card, "Vol", 12, 32, 100, 14, "Master volume", 8f, TextAlignmentOptions.MidlineLeft);

        var sr = UIKit.At(card, "Slider", 12, 50, 216, 12);
        UIKit.Tint(sr, UIKit.T.chip);
        var fillArea = UIKit.At(sr, "Fill Area", 0, 0, 216, 12); UIKit.Fill(fillArea, 2, 3, 2, 3);
        var fill = UIKit.At(fillArea, "Fill", 0, 0, 10, 6); UIKit.Fill(fill);
        var fi = UIKit.Tint(fill, UIKit.T.teal);
        var slider = sr.gameObject.AddComponent<Slider>();
        slider.fillRect = fill; slider.targetGraphic = fi; slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = AudioListener.volume;
        slider.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat("SpaceAgency.Volume", v); });
        if (PlayerPrefs.HasKey("SpaceAgency.Volume")) { AudioListener.volume = PlayerPrefs.GetFloat("SpaceAgency.Volume"); slider.SetValueWithoutNotify(AudioListener.volume); }
        UIKit.Btn(card, "Back", 12, 82, 70, 18, UIKit.T.chip, () => Show(titleScreen), 7.5f);
    }

    // =============================================================== register your agency
    void BuildRegister()
    {
        registerScreen = Screen_("Register Screen");
        var s = registerScreen.transform;
        UIKit.TextAt(s, "Heading", 40, 18, 400, 26, "Register Your Agency", 16f, TextAlignmentOptions.MidlineLeft, true);

        // ---------- left card
        var card = UIKit.Card(s, "Agency Card", 40, 50, 290, 272);
        UIKit.TextAt(card, "NameLabel", 14, 10, 200, 12, "Agency name", 8f, TextAlignmentOptions.MidlineLeft, true);
        nameInput = UIKit.Input(card, 14, 24, 262, 18, "e.g. Aurora Space Agency", 22, 8f);
        nameInput.onValueChanged.AddListener(v => agencyName = v);

        UIKit.TextAt(card, "TypeLabel", 14, 50, 200, 12, "Agency type", 8f, TextAlignmentOptions.MidlineLeft, true);
        for (int i = 0; i < Agency.Types.Length; i++)
        {
            int idx = i;
            var b = UIKit.Btn(card, Agency.Types[i], 14 + (i % 2) * 133, 64 + (i / 2) * 20, 129, 16, UIKit.T.chip, () => { type = idx; RefreshRegister(); }, 7f);
            typeChips[i] = (Image)b.targetGraphic;
        }
        typeNote = UIKit.TextAt(card, "TypeNote", 14, 106, 262, 12, "", 6.8f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);

        UIKit.TextAt(card, "StatsLabel", 14, 126, 150, 12, "Choose stats", 8f, TextAlignmentOptions.MidlineLeft, true);
        pointsLeft = UIKit.TextAt(card, "Points", 150, 126, 126, 12, "", 7f, TextAlignmentOptions.MidlineRight, true);
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float y = 142 + i * 22;
            var row = UIKit.At(card, "Stat" + i, 14, y, 262, 19);
            UIKit.Tint(row, UIKit.T.well);
            UIKit.TextAt(row, "Name", 6, 0, 80, 19, Agency.StatNames[i], 7.5f, TextAlignmentOptions.MidlineLeft, true);
            UIKit.TextAt(row, "Note", 82, 0, 100, 19, Agency.StatNotes[i], 6f, TextAlignmentOptions.MidlineLeft, false, UIKit.T.dim);
            UIKit.Btn(row, "-", 190, 3, 14, 13, UIKit.T.chip, () => ChangeStat(idx, -1), 8f);
            statValue[i] = UIKit.TextAt(row, "Val", 206, 0, 22, 19, "1", 8f, TextAlignmentOptions.Center, true);
            UIKit.Btn(row, "+", 230, 3, 14, 13, UIKit.T.chip, () => ChangeStat(idx, 1), 8f);
        }

        UIKit.Btn(card, "Back", 14, 242, 70, 20, UIKit.T.chip, () => Show(titleScreen), 7.5f);
        UIKit.Btn(card, "Create", 196, 242, 80, 20, UIKit.T.teal, Create, 8f);

        // ---------- right card: layer based logo creator
        var lc = UIKit.Card(s, "Logo Card", 340, 50, 260, 272);
        UIKit.TitleBar(lc, "Logo Creator", UIKit.T.purple, 260, null);
        var pv = UIKit.At(lc, "Preview", 85, 28, 90, 90);
        UIKit.Tint(pv, new Color(1f, 1f, 1f, 0.10f));
        logoPreview = UIKit.At(pv, "Layers", 0, 0, 90, 90);
        UIKit.TextAt(lc, "LayersLabel", 14, 124, 150, 12, "Layers (bottom to top)", 7.5f, TextAlignmentOptions.MidlineLeft, true);
        layerList = UIKit.ScrollList(lc, "Layer List", 10, 138, 240, 100, null, 2f);
        UIKit.Btn(lc, "Add layer", 10, 244, 90, 18, UIKit.T.purple, AddLayer, 7f);
        UIKit.Btn(lc, "Randomize", 106, 244, 60, 18, UIKit.T.chip, Randomize, 7f);

        logo = LogoDesign.Default();
        RefreshRegister();
    }

    void ChangeStat(int i, int d)
    {
        if (d > 0 && (PointsLeft <= 0 || stats[i] >= Agency.StatMax)) return;
        if (d < 0 && stats[i] <= 1) return;
        stats[i] += d;
        RefreshRegister();
    }

    void AddLayer()
    {
        if (logo.layers.Count >= 4) { Say("A logo can have up to 4 layers."); return; }
        logo.layers.Add(new LogoLayer { shape = Random.Range(0, LogoArt.ShapeNames.Length), color = Random.Range(0, LogoArt.Palette.Length), size = 2 });
        RefreshRegister();
    }

    void Randomize()
    {
        logo = new LogoDesign();
        int n = Random.Range(2, 5);
        for (int i = 0; i < n; i++) logo.layers.Add(new LogoLayer { shape = Random.Range(0, LogoArt.ShapeNames.Length), color = Random.Range(0, LogoArt.Palette.Length), size = Mathf.Max(1, 4 - i) });
        RefreshRegister();
    }

    void RefreshRegister()
    {
        for (int i = 0; i < typeChips.Length; i++) typeChips[i].color = i == type ? UIKit.T.teal : UIKit.T.chip;
        typeNote.text = Agency.TypeNotes[type];
        pointsLeft.text = "points left: " + PointsLeft + " / " + Agency.StatPoints;
        for (int i = 0; i < 4; i++) statValue[i].text = stats[i].ToString();

        LogoArt.Draw(logoPreview, logo, 90f);
        UIKit.Clear(layerList);
        for (int li = 0; li < logo.layers.Count; li++)
        {
            int idx = li;
            var l = logo.layers[li];
            var row = UIKit.At(layerList, "Layer" + li, 0, 0, 100, 24);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            UIKit.Tint(row, UIKit.T.well);
            UIKit.TextAt(row, "N", 4, 0, 12, 24, (li + 1).ToString(), 7f, TextAlignmentOptions.Center, true, UIKit.T.dim);
            UIKit.Btn(row, "<", 17, 6, 12, 12, UIKit.T.chip, () => { l.shape = (l.shape + LogoArt.ShapeNames.Length - 1) % LogoArt.ShapeNames.Length; RefreshRegister(); }, 7f);
            UIKit.TextAt(row, "Shape", 30, 0, 44, 24, LogoArt.ShapeNames[l.shape], 6.8f, TextAlignmentOptions.Center);
            UIKit.Btn(row, ">", 75, 6, 12, 12, UIKit.T.chip, () => { l.shape = (l.shape + 1) % LogoArt.ShapeNames.Length; RefreshRegister(); }, 7f);
            UIKit.Btn(row, "<", 94, 6, 12, 12, UIKit.T.chip, () => { l.color = (l.color + LogoArt.Palette.Length - 1) % LogoArt.Palette.Length; RefreshRegister(); }, 7f);
            var sw = UIKit.At(row, "Swatch", 108, 6, 22, 12);
            var swi = UIKit.Tint(sw, LogoArt.Palette[l.color]);
            UIKit.Btn(row, ">", 132, 6, 12, 12, UIKit.T.chip, () => { l.color = (l.color + 1) % LogoArt.Palette.Length; RefreshRegister(); }, 7f);
            UIKit.Btn(row, "-", 152, 6, 12, 12, UIKit.T.chip, () => { l.size = Mathf.Max(1, l.size - 1); RefreshRegister(); }, 7f);
            UIKit.TextAt(row, "Size", 165, 0, 14, 24, l.size.ToString(), 7f, TextAlignmentOptions.Center);
            UIKit.Btn(row, "+", 180, 6, 12, 12, UIKit.T.chip, () => { l.size = Mathf.Min(4, l.size + 1); RefreshRegister(); }, 7f);
            UIKit.Btn(row, "x", 202, 6, 14, 12, UIKit.T.red, () => { if (logo.layers.Count > 1) { logo.layers.RemoveAt(idx); RefreshRegister(); } else Say("Keep at least one layer."); }, 7f);
            UIKit.MakeButton(UIKit.At(row, "up", 218, 6, 12, 12), "^", UIKit.T.chip, () => { if (idx < logo.layers.Count - 1) { (logo.layers[idx], logo.layers[idx + 1]) = (logo.layers[idx + 1], logo.layers[idx]); RefreshRegister(); } }, 7f);
        }
    }

    void Create()
    {
        if (string.IsNullOrWhiteSpace(agencyName)) { Say("Give your agency a name first."); return; }
        if (PointsLeft > 0) { Say("Spend all " + Agency.StatPoints + " stat points first (" + PointsLeft + " left)."); return; }
        Agency.Create(agencyName, type, stats, logo);
        SceneManager.LoadScene(gameScene);
    }
}
