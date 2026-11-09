using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click setup for the Rocket Builder: builds the PartLibrary asset (catalog + Blender models + livery materials + icons + outline),
/// adds the full-screen builder window to the Game scene's canvas wired to the sidebar "Rocket Builder" button,
/// and adds the Deployment Manager that puts the deployed rocket on the launch pad. Safe to run again.
/// Menu: Tools / Space Agency / Setup Rocket Builder.
/// </summary>
public static class RocketBuilderSetup
{
    const string PartsDir = "Assets/Assets/Models/Parts/";
    const string IconDir = "Assets/Assets/Graphics/PartIcons/";
    const string LibraryPath = "Assets/Data/Parts/PartLibrary.asset";
    const string CatalogPath = "Assets/Data/Parts/parts-catalog.json";
    const string FlatMat = "Assets/Assets/Graphics/UI/UI-GlassFlat.mat";
    const string RoundSprite = "Assets/Assets/Graphics/ui-glass-round.png";
    const string BodyFont = "Assets/Assets/Font/Modern/Rajdhani Medium SDF.asset";
    const string HeadFont = "Assets/Assets/Font/Modern/Orbitron SDF.asset";

    [MenuItem("Tools/Space Agency/Setup Rocket Builder")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        var lib = BuildLibrary();
        var msg = AddPanel(lib) + " " + AddDeploymentManager(lib);
        Debug.Log("Rocket Builder set up. " + msg);
    }

    static Material OutlineMaterial()
    {
        var path = PartsDir + "Outline.mat";
        var shader = Shader.Find("SpaceAgency/Toon/Outline");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_OutlineColor", new Color(0.3f, 0.85f, 1f, 1f));
        EditorUtility.SetDirty(m);
        return m;
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
        lib.outlineMaterial = OutlineMaterial();

        // icons: make every png a sprite first
        var icons = new List<PartLibrary.IconEntry>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconDir.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) icons.Add(new PartLibrary.IconEntry { id = System.IO.Path.GetFileNameWithoutExtension(path), sprite = sp });
        }
        lib.icons = icons.OrderBy(i => i.id).ToArray();

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    static string AddPanel(PartLibrary lib)
    {
        foreach (var e in Object.FindObjectsByType<RocketBuilderUI>(FindObjectsInactive.Include)) Object.DestroyImmediate(e.gameObject);

        Transform parent = null;
        var mm = Object.FindAnyObjectByType<MissionManagerUI>(FindObjectsInactive.Include);
        if (mm != null) parent = mm.transform.parent;
        if (parent == null) parent = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).First(c => c.isRootCanvas).transform;

        var go = new GameObject("Rocket Builder Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;          // full-screen editor

        var img = go.AddComponent<Image>();
        img.color = new Color(0.03f, 0.05f, 0.09f, 1f);                    // plain dark backdrop behind the 3D view

        var ui = go.AddComponent<RocketBuilderUI>();
        ui.library = lib;
        ui.flatMat = AssetDatabase.LoadAssetAtPath<Material>(FlatMat);
        ui.roundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSprite);
        ui.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
        ui.headFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadFont);
        go.SetActive(false);

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
        return lib.models.Length + " models, " + lib.icons.Length + " icons, " + wired + ".";
    }

    static string AddDeploymentManager(PartLibrary lib)
    {
        var dm = Object.FindAnyObjectByType<DeploymentManager>(FindObjectsInactive.Include);
        if (dm == null) dm = new GameObject("Deployment Manager").AddComponent<DeploymentManager>();
        dm.library = lib;
        var falcon = GameObject.Find("Falcon (Blender)");
        if (falcon != null) { dm.placeholder = falcon; dm.rocketPosition = new Vector3(falcon.transform.position.x, falcon.transform.position.y, falcon.transform.position.z); }
        EditorUtility.SetDirty(dm);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return "Deployment Manager ready" + (falcon != null ? " (placeholder rocket linked)." : ".");
    }
}
