using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Off-screen 3D stage that shows the rocket being built: a camera rendering into a RenderTexture, two lamps
/// (the toon shader only needs point lights), and orbit / zoom controls. Lives far from the game world.
/// </summary>
public class RocketPreview : MonoBehaviour
{
    public RenderTexture Texture { get; private set; }

    Camera cam;
    Light key, fill;
    Transform stage;
    GameObject current;
    Bounds bounds = new Bounds(Vector3.up * 3f, Vector3.one * 6f);
    float yaw = 35f, pitch = 12f, dist = 20f, targetDist = 20f;
    Vector3 panOffset;

    public static RocketPreview Create(int width, int height)
    {
        var go = new GameObject("Rocket Preview Stage");
        go.transform.position = new Vector3(4000f, 0f, 4000f);
        var p = go.AddComponent<RocketPreview>();
        p.Init(width, height);
        return p;
    }

    void Init(int w, int h)
    {
        Texture = new RenderTexture(w, h, 24) { antiAliasing = 4, name = "RocketPreviewRT", filterMode = FilterMode.Bilinear, useMipMap = false };
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);

        var cg = new GameObject("Preview Camera");
        cg.transform.SetParent(transform, false);
        cam = cg.AddComponent<Camera>();
        cam.targetTexture = Texture;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.055f, 0.08f, 0.125f, 1f);
        cam.fieldOfView = 28f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 600f;
        cam.enabled = false;

        key = NewLight("Key", new Color(1f, 0.95f, 0.88f));
        fill = NewLight("Fill", new Color(0.7f, 0.8f, 1f));
    }

    Light NewLight(string n, Color c)
    {
        var g = new GameObject(n);
        g.transform.SetParent(transform, false);
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.shadows = LightShadows.None; l.enabled = false;
        return l;
    }

    void OnDestroy()
    {
        if (Texture != null) Texture.Release();
    }

    public void SetActive(bool on)
    {
        if (cam == null) return;
        cam.enabled = on; key.enabled = on; fill.enabled = on;
    }

    /// <summary>Replace what is shown. The rocket should be built at the origin; it is moved onto the stage.</summary>
    public void Show(GameObject rocket, Bounds worldBoundsAtOrigin)
    {
        if (current != null) Destroy(current);
        current = rocket;
        rocket.transform.SetParent(stage, false);
        rocket.transform.localPosition = Vector3.zero;
        bounds = worldBoundsAtOrigin;
        float radius = Mathf.Max(bounds.extents.magnitude, 1.5f);
        targetDist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.28f;
    }

    public void Orbit(Vector2 delta)
    {
        yaw += delta.x * 0.45f;
        pitch = Mathf.Clamp(pitch - delta.y * 0.3f, -15f, 70f);
    }

    public float ViewDistance => dist;
    public GameObject Current => current;

    /// <summary>Which part is under this point of the preview image (0..1 on both axes)? Uses each part's bounding box.</summary>
    public PartRef Pick(Vector2 normalized)
    {
        if (cam == null || current == null) return null;
        var ray = cam.ViewportPointToRay(new Vector3(normalized.x, normalized.y, 0f));
        PartRef best = null; float bestT = float.MaxValue;
        foreach (var pr in current.GetComponentsInChildren<PartRef>())
        {
            var rends = pr.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) continue;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            if (b.IntersectRay(ray, out float t) && t < bestT) { bestT = t; best = pr; }
        }
        return best;
    }

    public void Zoom(float scroll) => targetDist = Mathf.Clamp(targetDist * (1f - scroll * 0.1f), 3f, 150f);

    /// <summary>Slide the view sideways / up and down (screen pixels), so any part of the rocket can be brought to the middle.</summary>
    public void Pan(Vector2 pixels)
    {
        float k = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, Screen.height);
        panOffset -= (cam.transform.right * pixels.x + cam.transform.up * pixels.y) * k;
        panOffset = Vector3.ClampMagnitude(panOffset, 200f);
    }

    public void ResetView() { yaw = 35f; pitch = 12f; panOffset = Vector3.zero; }

    void LateUpdate()
    {
        if (cam == null || !cam.enabled) return;
        dist = Mathf.Lerp(dist, targetDist, 0.2f);
        Vector3 center = stage.position + bounds.center + panOffset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.position = center - rot * Vector3.forward * dist;
        cam.transform.rotation = rot;

        // lamps ride with the camera so the visible side is always lit
        float i = dist * dist;
        key.transform.position = cam.transform.position + cam.transform.right * dist * 0.5f + cam.transform.up * dist * 0.45f;
        fill.transform.position = cam.transform.position - cam.transform.right * dist * 0.6f - cam.transform.up * dist * 0.1f;
        key.range = fill.range = dist * 4f;
        key.intensity = i * 1.5f; fill.intensity = i * 0.5f;
    }
}

/// <summary>On the RawImage that shows the preview: drag to orbit, scroll to zoom.</summary>
public class RocketPreviewInput : MonoBehaviour, IDragHandler, IScrollHandler, IPointerClickHandler
{
    public RocketPreview preview;
    /// <summary>Left-drag pans instead of rotating (right or middle mouse button always pans).</summary>
    public bool panMode;
    /// <summary>Called with the part that was clicked (null = empty space).</summary>
    public System.Action<PartRef> picked;

    public void OnDrag(PointerEventData e)
    {
        if (preview == null) return;
        if (panMode || e.button == PointerEventData.InputButton.Right || e.button == PointerEventData.InputButton.Middle) preview.Pan(e.delta);
        else preview.Orbit(e.delta);
    }
    public void OnScroll(PointerEventData e) => preview?.Zoom(e.scrollDelta.y);

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        var rt = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var lp)) return;
        var n = new Vector2((lp.x - rt.rect.xMin) / rt.rect.width, (lp.y - rt.rect.yMin) / rt.rect.height);
        picked?.Invoke(preview?.Pick(n));
    }
}
