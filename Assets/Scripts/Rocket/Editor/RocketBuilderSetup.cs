using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click setup for the Rocket Builder: builds the PartLibrary asset (catalog + Blender models + livery materials)
/// and adds the builder window to the Game scene's canvas, wired to the sidebar "Rocket Builder" button.
/// Safe to run again. Menu: Tools / Space Agency / Setup Rocket Builder.
/// </summary>
public static class RocketBuilderSetup
{
    const string PartsDir = "Assets/Assets/Models/Parts/";
    const string LibraryPath = "Assets/Data/Parts/PartLibrary.asset";
    const string CatalogPath = "Assets/Data/Parts/parts-catalog.json";
    const string FlatMat = "Assets/Assets/Graphics/UI/UI-GlassFlat.mat";
    const string GlassMat = "Assets/Assets/Graphics/UI/UI-Glass.mat";
    const string RoundSprite = "Assets/Assets/Graphics/ui-glass-round.png";
    const string BodyFont = "Assets/Assets/Font/Modern/Rajdhani Medium SDF.asset";
    const string HeadFont = "Assets/Assets/Font/Modern/Orbitron SDF.asset";

    [MenuItem("Tools/Space Agency/Setup Rocket Builder")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        var lib = BuildLibrary();
        var msg = AddPanel(lib);
        Debug.Log("Rocket Builder set up. " + msg);
    }

    static PartLibrary BuildLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<PartLibrary>(LibraryPath);
        if (lib == null) { lib = ScriptableObject.CreateInstance<PartLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
        lib.catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
        var models = new List<PartLibrary.ModelEntry>();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PartsDir.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) models.Add(new PartLibrary.ModelEntry { id = System.IO.Path.GetFileNameWithoutExtension(path), prefab = go });
        }
        lib.models = models.OrderBy(m => m.id).ToArray();
        lib.liveryMaterials = Enumerable.Range(0, 8).Select(i => AssetDatabase.LoadAssetAtPath<Material>(PartsDir + "Part_" + i + ".mat")).ToArray();
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    static string AddPanel(PartLibrary lib)
    {
        var existing = Object.FindObjectsByType<RocketBuilderUI>(FindObjectsInactive.Include);
        foreach (var e in existing) Object.DestroyImmediate(e.gameObject);

        // put it next to the Mission Manager window on the same canvas
        Transform parent = null;
        var mm = Object.FindAnyObjectByType<MissionManagerUI>(FindObjectsInactive.Include);
        if (mm != null) parent = mm.transform.parent;
        if (parent == null) parent = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).First(c => c.isRootCanvas).transform;

        var go = new GameObject("Rocket Builder Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(600f, 340f);

        var img = go.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSprite);
        img.type = Image.Type.Sliced;
        img.material = AssetDatabase.LoadAssetAtPath<Material>(GlassMat);
        img.color = new Color(0.04f, 0.07f, 0.12f, 1f);

        var ui = go.AddComponent<RocketBuilderUI>();
        ui.library = lib;
        ui.flatMat = AssetDatabase.LoadAssetAtPath<Material>(FlatMat);
        ui.roundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSprite);
        ui.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
        ui.headFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadFont);
        go.SetActive(false);

        // sidebar button opens it
        var btn = GameObject.Find("Rocket Builder Button");
        string wired = "no sidebar button found";
        if (btn != null)
        {
            var toggle = btn.GetComponent<PanelToggleButton>() ?? btn.AddComponent<PanelToggleButton>();
            toggle.panel = go;
            EditorUtility.SetDirty(toggle);
            wired = "sidebar button wired";
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return lib.models.Length + " models, " + lib.liveryMaterials.Length + " liveries, " + wired + ".";
    }
}
