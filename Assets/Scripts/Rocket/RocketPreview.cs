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
        Texture = new RenderTexture(w, h, 24) { antiAliasing = 4, name = "RocketPreviewRT" };
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
        targetDist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
    }

    public void Orbit(Vector2 delta)
    {
        yaw += delta.x * 0.45f;
        pitch = Mathf.Clamp(pitch - delta.y * 0.3f, -15f, 70f);
    }

    public void Zoom(float scroll) => targetDist = Mathf.Clamp(targetDist * (1f - scroll * 0.1f), 3f, 150f);

    public void ResetView() { yaw = 35f; pitch = 12f; }

    void LateUpdate()
    {
        if (cam == null || !cam.enabled) return;
        dist = Mathf.Lerp(dist, targetDist, 0.2f);
        Vector3 center = stage.position + bounds.center;
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
public class RocketPreviewInput : MonoBehaviour, IDragHandler, IScrollHandler
{
    public RocketPreview preview;
    public void OnDrag(PointerEventData e) => preview?.Orbit(e.delta);
    public void OnScroll(PointerEventData e) => preview?.Zoom(e.scrollDelta.y);
}
