using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click re-skin of the UI to the glass look: switches the canvas to Screen Space - Camera,
/// swaps old pixel-art sliced backgrounds for the rounded glass sprite + material
/// (blur glass for top-level panels, flat glass for nested elements) and applies the modern fonts.
/// Safe to run repeatedly. Menu: Tools / Space Agency / Apply Glass UI Skin.
/// </summary>
public static class ApplyGlassSkin
{
    const string GlassMat = "Assets/Assets/Graphics/UI/UI-Glass.mat";
    const string FlatMat = "Assets/Assets/Graphics/UI/UI-GlassFlat.mat";
    const string RoundSprite = "Assets/Assets/Graphics/ui-glass-round.png";
    const string BodyFont = "Assets/Assets/Font/Modern/Rajdhani Medium SDF.asset";
    const string HeadFont = "Assets/Assets/Font/Modern/Orbitron SDF.asset";
    const string CardPrefab = "Assets/Prefabs/UI/ContractCard.prefab";
    const string RowPrefab = "Assets/Prefabs/UI/RunningMissionRow.prefab";

    static readonly Color Dark = new Color(0.04f, 0.07f, 0.12f, 1f);

    [MenuItem("Tools/Space Agency/Apply Glass UI Skin")]
    public static void Apply()
    {
        var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMat);
        var flat = AssetDatabase.LoadAssetAtPath<Material>(FlatMat);
        var round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSprite);
        var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
        var head = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadFont);
        if (glass == null || flat == null || round == null || body == null)
        {
            Debug.LogError("ApplyGlassSkin: missing glass materials, sprite or font assets.");
            return;
        }

        // Scene canvases: glass blur reads the camera's opaque texture, so overlay mode won't work.
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (!canvas.isRootCanvas) continue;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
            }
            Skin(canvas.transform, glass, flat, round, body, head, topLevel: true);
            EditorUtility.SetDirty(canvas);
        }

        foreach (var path in new[] { CardPrefab, RowPrefab })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            Skin(root.transform, glass, flat, round, body, head, topLevel: false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        Debug.Log("Glass UI skin applied. Save the scene (Ctrl+S) and enable 'Opaque Texture' on the URP asset if blur looks black.");
    }

    static void Skin(Transform root, Material glass, Material flat, Sprite round, TMP_FontAsset body, TMP_FontAsset head, bool topLevel)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            // Only old pixel-art sliced backgrounds (have 9-slice borders) are replaced; icons are left alone.
            var s = img.sprite;
            bool isBackground = s != null && s.border != Vector4.zero && s != round;
            bool isGlassAlready = img.sprite == round;
            if (!isBackground && !isGlassAlready) continue;
            // Skip scrollbars/sliders handles etc. that are driven by other components.
            if (img.GetComponent<Scrollbar>() != null || img.GetComponent<Slider>() != null) continue;

            bool nestedInGlass = HasGlassAncestor(img.transform, root);
            img.sprite = round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.material = (topLevel && !nestedInGlass) ? glass : flat;
            img.color = (topLevel && !nestedInGlass) ? new Color(Dark.r, Dark.g, Dark.b, 1f) : new Color(1f, 1f, 1f, 0.14f);
            EditorUtility.SetDirty(img);
        }

        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            bool heading = head != null && (t.name == "Title" || t.name == "Header" || t.fontSize >= 20f);
            t.font = heading ? head : body;
            t.color = Color.white;
            EditorUtility.SetDirty(t);
        }
    }

    static bool HasGlassAncestor(Transform t, Transform stopAt)
    {
        for (var p = t.parent; p != null && p != stopAt; p = p.parent)
        {
            var i = p.GetComponent<Image>();
            if (i != null && i.sprite != null && i.material != null && i.material.name == "UI-Glass") return true;
            if (i != null && i.sprite != null && i.sprite.border != Vector4.zero) return true;
        }
        return false;
    }
}
