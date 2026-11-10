using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click setup for the reference UI layouts: creates Resources/UITheme (pointing at the existing glass materials,
/// which are not touched), adds the HUD controller to the Game scene and the Main Menu screens to the Main Menu scene.
/// Safe to run again. Menu: Tools / Space Agency / Setup Game UI.
/// </summary>
public static class GameUISetup
{
    const string ThemePath = "Assets/Resources/UITheme.asset";
    const string GlassMat = "Assets/Assets/Graphics/UI/UI-Glass.mat";
    const string FlatMat = "Assets/Assets/Graphics/UI/UI-GlassFlat.mat";
    const string RoundSprite = "Assets/Assets/Graphics/ui-glass-round.png";
    const string BodyFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string IconDir = "Assets/Assets/Graphics/modern-icons/";
    const string GameScene = "Assets/Scenes/2. Game.unity";
    const string MenuScene = "Assets/Scenes/1. Main Menu.unity";

    [MenuItem("Tools/Space Agency/Setup Game UI")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var theme = BuildTheme();

        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        SetupMenu();
        EditorSceneManager.SaveOpenScenes();

        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        SetupGame();
        EditorSceneManager.SaveOpenScenes();

        EnsureBuildScenes();
        Debug.Log("Game UI set up (theme " + (theme != null) + ").");
    }

    static UITheme BuildTheme()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var t = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (t == null) { t = ScriptableObject.CreateInstance<UITheme>(); AssetDatabase.CreateAsset(t, ThemePath); }
        t.glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMat);
        t.flat = AssetDatabase.LoadAssetAtPath<Material>(FlatMat);
        t.round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSprite);
        t.body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
        t.head = t.body;          // one clean sans-serif; headings are set in bold
        var d = ScriptableObject.CreateInstance<UITheme>();      // the palette lives in UITheme's field defaults
        t.dark = d.dark; t.ink = d.ink; t.dim = d.dim; t.well = d.well; t.chip = d.chip; t.track = d.track;
        t.teal = d.teal; t.orange = d.orange; t.purple = d.purple; t.green = d.green; t.red = d.red; t.blue = d.blue; t.priceText = d.priceText;
        Object.DestroyImmediate(d);
        t.closeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "close.png");
        EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();
        return t;
    }

    /// <summary>Apple-inspired restyle of the existing glass: tweaks the two glass materials (values only) and re-fonts the scene UI.</summary>
    [MenuItem("Tools/Space Agency/Apply Apple Glass Style")]
    public static void ApplyAppleStyle()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var theme = BuildTheme();
        var glass = theme.glass; var flat = theme.flat;
        glass.SetFloat("_BlurRadius", 7f); glass.SetFloat("_TintStrength", 0.6f);
        glass.SetFloat("_BackdropBrightness", 0.85f); glass.SetFloat("_BackdropSaturation", 1.7f);
        glass.SetColor("_BorderColor", new Color(1f, 1f, 1f, 0.55f)); glass.SetFloat("_RimBottom", 0.3f);
        flat.SetColor("_BorderColor", new Color(1f, 1f, 1f, 0.2f)); flat.SetFloat("_RimBottom", 0.25f);
        EditorUtility.SetDirty(glass); EditorUtility.SetDirty(flat);
        AssetDatabase.SaveAssets();

        foreach (var path in new[] { MenuScene, GameScene })
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var txt in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (!txt.gameObject.scene.IsValid()) continue;
                bool wasHead = txt.font != null && txt.font.name.StartsWith("Orbitron");
                txt.font = theme.body;
                txt.fontStyle = wasHead ? FontStyles.Bold : FontStyles.Normal;
                EditorUtility.SetDirty(txt);
            }
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
                if (img.gameObject.scene.IsValid() && img.material != null && img.material.name == "UI-Glass") { img.color = theme.dark; EditorUtility.SetDirty(img); }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        Debug.Log("Apple glass style applied.");
    }

    static Canvas RootCanvas() => Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).First(c => c.isRootCanvas);

    static void SetupMenu()
    {
        var canvas = RootCanvas();
        if (canvas.GetComponent<MainMenuUI>() == null) canvas.gameObject.AddComponent<MainMenuUI>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void SetupGame()
    {
        var canvas = RootCanvas();
        var hud = canvas.GetComponent<HudController>() ?? canvas.gameObject.AddComponent<HudController>();
        hud.builder = Object.FindAnyObjectByType<RocketBuilderUI>(FindObjectsInactive.Include);
        hud.playIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "play_arrow.png");
        hud.pauseIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "pause.png");
        hud.navIcons = new[] { NavIcon("assignment"), NavIcon("nav_training"), NavIcon("nav_building"), NavIcon("nav_testing") };

        // procedural sky with moving clouds, driven by DayNightCycle
        var dn = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        if (dn != null)
        {
            const string skyDir = "Assets/Assets/Graphics/Sky", skyPath = skyDir + "/Sky.mat";
            if (!AssetDatabase.IsValidFolder(skyDir)) AssetDatabase.CreateFolder("Assets/Assets/Graphics", "Sky");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            var shader = Shader.Find("SpaceAgency/Sky/Procedural");
            if (sky == null && shader != null) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, skyPath); }
            if (sky != null) { sky.shader = shader; dn.skyMaterial = sky; RenderSettings.skybox = sky; EditorUtility.SetDirty(sky); EditorUtility.SetDirty(dn); }
        }

        // game time: one real minute is one game hour at normal speed; controls are pause / 3x / 6x; a fixed start date (not real time)
        var clock = Object.FindAnyObjectByType<GameClock>(FindObjectsInactive.Include);
        if (clock != null)
        {
            clock.gameMinutesPerSecond = 24f;
            clock.speedLevels = new[] { 0f, 1f, 2f, 6f };
            clock.startSpeedIndex = 1;
            clock.startFromRealDhakaTime = false;
            clock.startYear = 2026; clock.startMonth = 1; clock.startDay = 1; clock.startHour = 6;
            EditorUtility.SetDirty(clock);
        }
        EditorUtility.SetDirty(hud);

        // the designers are now cards over the game view: no full-screen dark backdrop
        if (hud.builder != null)
        {
            var img = hud.builder.GetComponent<Image>();
            if (img != null) { img.color = Color.clear; img.raycastTarget = false; EditorUtility.SetDirty(img); }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static Sprite NavIcon(string name)
    {
        var path = IconDir + name + ".png";
        if (AssetImporter.GetAtPath(path) is TextureImporter ti && (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureBuildScenes()
    {
        var list = EditorBuildSettings.scenes.ToList();
        foreach (var p in new[] { MenuScene, GameScene })
            if (!list.Any(s => s.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = list.OrderBy(s => s.path.Contains("Main Menu") ? 0 : 1).ToArray();
    }
}
