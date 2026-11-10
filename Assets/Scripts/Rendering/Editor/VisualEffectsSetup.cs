using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Switches on the look of the game: post-processing (bloom, tonemapping, vignette), god rays and strong side sunlight.</summary>
public static class VisualEffectsSetup
{
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string ProfilePath = "Assets/Settings/GameVolumeProfile.asset";

    [MenuItem("Tools/Space Agency/Setup Visual Effects")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
        EditorSceneManager.OpenScene("Assets/Scenes/2. Game.unity", OpenSceneMode.Single);
        AddGodRays();
        var profile = MakeProfile();

        var go = GameObject.Find("Post Processing Volume");
        if (go == null) go = new GameObject("Post Processing Volume");
        var vol = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 1f; vol.sharedProfile = profile; vol.weight = 1f;

        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            cam.allowHDR = true;
            EditorUtility.SetDirty(cam); EditorUtility.SetDirty(data);
        }

        // strong sun from the side
        var dn = Object.FindAnyObjectByType<DayNightCycle>();
        if (dn != null)
        {
            dn.maxIntensity = 1.7f; dn.sunMaxElevation = 58f; dn.sunAzimuth = 90f;
            EditorUtility.SetDirty(dn);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Visual effects set up.");
    }

    static void AddGodRays()
    {
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rd == null) { Debug.LogError("Renderer not found: " + RendererPath); return; }
        foreach (var f in rd.rendererFeatures) if (f is GodRaysFeature) return;
        var feature = ScriptableObject.CreateInstance<GodRaysFeature>();
        feature.name = "God Rays";
        feature.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PostFX/SpaceAgencyGodRays.shader");
        AssetDatabase.AddObjectToAsset(feature, rd);
        rd.rendererFeatures.Add(feature);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
        var so = new SerializedObject(rd);
        var map = so.FindProperty("m_RendererFeatureMap");
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rd);
        AssetDatabase.SaveAssets();
    }

    static VolumeProfile MakeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        // remove old (possibly orphaned) components, then add fresh ones as real sub-assets so they survive a reload
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ProfilePath)) if (o != null && o != profile) Object.DestroyImmediate(o, true);
        profile.components.Clear();

        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(0.85f); bloom.intensity.Override(2.0f); bloom.scatter.Override(0.8f);
        bloom.tint.Override(new Color(1f, 0.95f, 0.85f)); bloom.highQualityFiltering.Override(true);

        var tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);

        var vig = profile.Add<Vignette>();
        vig.intensity.Override(0.2f); vig.smoothness.Override(0.55f);

        var col = profile.Add<ColorAdjustments>();
        col.contrast.Override(8f); col.saturation.Override(6f);

        foreach (var c in profile.components) { c.name = c.GetType().Name; if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile); }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
    }
}
