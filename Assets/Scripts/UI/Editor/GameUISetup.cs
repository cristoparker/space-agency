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
    const string BodyFont = "Assets/Assets/Font/Modern/Rajdhani Medium SDF.asset";
    const string HeadFont = "Assets/Assets/Font/Modern/Orbitron SDF.asset";
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
        t.head = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadFont);
        t.closeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "close.png");
        EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();
        return t;
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
        EditorUtility.SetDirty(hud);

        // the designers are now cards over the game view: no full-screen dark backdrop
        if (hud.builder != null)
        {
            var img = hud.builder.GetComponent<Image>();
            if (img != null) { img.color = Color.clear; img.raycastTarget = false; EditorUtility.SetDirty(img); }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void EnsureBuildScenes()
    {
        var list = EditorBuildSettings.scenes.ToList();
        foreach (var p in new[] { MenuScene, GameScene })
            if (!list.Any(s => s.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = list.OrderBy(s => s.path.Contains("Main Menu") ? 0 : 1).ToArray();
    }
}
