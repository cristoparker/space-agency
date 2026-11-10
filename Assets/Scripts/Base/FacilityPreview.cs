using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pictures of the buildings for the Facility menu: the real game model is placed on a hidden stage far below the map, lit and
/// photographed once by a small camera, and the picture is kept (one per facility and level). It is a screenshot of the building only.
/// </summary>
public static class FacilityPreview
{
    const int W = 320, H = 200;
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    static GameObject stage;
    static Camera cam;
    static Light light;
    static RenderTexture rt;

    public static void ResetStatics() { cache.Clear(); stage = null; cam = null; light = null; rt = null; }

    public static void ClearCache()
    {
        foreach (var t in cache.Values) if (t != null) Object.Destroy(t);
        cache.Clear();
    }

    public static Texture2D Get(string id, int level)
    {
        string key = id + ":" + level;
        if (cache.TryGetValue(key, out var t) && t != null) return t;
        t = Render(id, level);
        if (t != null) cache[key] = t;
        return t;
    }

    static GameObject CreateModel(string id, int level, Transform parent)
    {
        var def = BaseData.Building(id);
        if (def != null && def.needsPlot)
        {
            var cat = Resources.Load<FacilityCatalog>("FacilityCatalog");
            return cat != null ? cat.CreateModel(id, level, parent, parent.position) : null;
        }
        // a fixed building (Office, Power Grid): copy the model of that level from the map
        var root = GameObject.Find("Base Buildings");
        var b = root != null ? root.transform.Find(id) : null;
        var lv = b != null ? b.Find("L" + level) : null;
        if (lv == null) return null;
        var go = Object.Instantiate(lv.gameObject, parent.position, Quaternion.identity, parent);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        go.SetActive(true);
        return go;
    }

    static void EnsureStage()
    {
        // objects can outlive Play mode (domain reload off) while the render texture does not: rebuild whatever is gone
        if (stage != null && cam != null && light != null && rt != null) return;
        if (stage != null) Object.Destroy(stage);
        if (rt != null) rt.Release();
        stage = new GameObject("Facility Preview Stage") { hideFlags = HideFlags.HideAndDontSave };
        stage.transform.position = new Vector3(0f, -4000f, 0f);
        var cg = new GameObject("FacilityPreviewCam");
        cg.transform.SetParent(stage.transform, false);
        cam = cg.AddComponent<Camera>();
        cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.fieldOfView = 24f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 1200f; cam.allowHDR = false; cam.allowMSAA = true;
        var data = cg.AddComponent<UniversalAdditionalCameraData>();
        data.renderShadows = false; data.renderPostProcessing = false;
        var lg = new GameObject("Stage Light");
        lg.transform.SetParent(stage.transform, false);
        lg.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        light = lg.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 2.1f; light.color = new Color(1f, 0.97f, 0.9f); light.shadows = LightShadows.None;
        light.enabled = false;
        rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Facility Preview", hideFlags = HideFlags.HideAndDontSave };
        rt.Create();
    }

    static Texture2D Render(string id, int level)
    {
        if (!Application.isPlaying) return null;
        EnsureStage();
        var holder = new GameObject("Model Holder");
        holder.transform.SetParent(stage.transform, false);
        holder.transform.position = stage.transform.position;
        var model = CreateModel(id, level, holder.transform);
        if (model == null) { Object.Destroy(holder); return null; }

        var rends = model.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { Object.Destroy(holder); return null; }
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);

        // three-quarter view from the front-right, looking at the middle of the building
        float radius = b.extents.magnitude;
        Vector3 dir = Quaternion.Euler(24f, -38f, 0f) * Vector3.back;                    // from behind the stage origin, raised
        float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.18f;
        cam.transform.position = b.center - Quaternion.Euler(24f, -38f, 0f) * Vector3.forward * dist;
        cam.transform.LookAt(b.center);
        cam.nearClipPlane = Mathf.Max(0.5f, dist - radius * 1.5f); cam.farClipPlane = dist + radius * 3f;

        light.enabled = true;
        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        light.enabled = false;

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Preview " + id + level, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply(false);
        RenderTexture.active = prev;
        Object.Destroy(holder);
        return tex;
    }
}
