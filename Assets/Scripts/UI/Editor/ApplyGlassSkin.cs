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

    const string IconDir = "Assets/Assets/Graphics/modern-icons/";
    static readonly string[] Scenes = { "Assets/Scenes/1. Main Menu.unity", "Assets/Scenes/2. Game.unity" };

    // GameObject name -> white icon file (put on the button, replacing its old glyph/pixel icon).
    static readonly System.Collections.Generic.Dictionary<string, string> Icons = new()
    {
        { "Money Icon", "paid" }, { "Previous Month Button", "chevron_left" }, { "Next Month Button", "chevron_right" },
        { "Slow Down Button", "fast_rewind" }, { "Speed Up Button", "fast_forward" }, { "Close Button", "close" },
        { "Mission Manager Button", "assignment" }, { "Rocket Builder Button", "rocket_launch" },
        { "Date Button", "calendar_today" },
        { "Discard Mission Button", "close" },
    };

    [MenuItem("Tools/Space Agency/Apply Glass UI Skin")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var path in Scenes)
        {
            if (!System.IO.File.Exists(path)) continue;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Apply();
            EditorSceneManager.SaveOpenScenes();
        }
    }

    static void Apply()
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
            // Sprite-less panel containers (e.g. Main Menu Panel, Mission Bar) become glass too.
            if (s == null && (img.name.Contains("Panel") || img.name == "Mission Bar")) isBackground = true;
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

        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (!Icons.TryGetValue(tr.name, out var file)) continue;
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + file + ".png");
            if (icon == null) continue;
            Image target = tr.name == "Money Icon" ? tr.GetComponent<Image>() : EnsureIconChild(tr);
            if (target == null) continue;
            target.sprite = icon; target.type = Image.Type.Simple; target.preserveAspect = true;
            target.material = null; target.color = Color.white; target.raycastTarget = false;
            EditorUtility.SetDirty(target);
        }

        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            bool heading = head != null && (t.name == "Title" || t.name == "Header" || t.fontSize >= 20f);
            t.font = heading ? head : body;
            t.color = Color.white;
            EditorUtility.SetDirty(t);
        }
    }

    // Finds/creates a white icon child on a button and hides the button's old text glyph.
    static Image EnsureIconChild(Transform button)
    {
        var existing = button.Find("Icon");
        if (existing == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(button, false);
            var rt = (RectTransform)go.transform;
            if (button.name == "Date Button")
            {
                // Button also shows the date text: small icon on the left, text keeps the rest.
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(14, 14); rt.anchoredPosition = new Vector2(8, 0);
            }
            else
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(3, 3); rt.offsetMax = new Vector2(-3, -3);
            }
            existing = go.transform;
        }
        if (button.name != "Date Button")
            foreach (var txt in button.GetComponentsInChildren<TMP_Text>(true)) txt.gameObject.SetActive(false);
        return existing.GetComponent<Image>();
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
