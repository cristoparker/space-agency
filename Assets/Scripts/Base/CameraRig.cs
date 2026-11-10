using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Strategy-style camera for the big site: it keeps its viewing angle and moves over the ground.
/// WASD / arrow keys or middle / right mouse drag pan, the mouse wheel zooms, Home returns to the overview.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Vector3 focus = new Vector3(0f, 6f, 130f);
    public float distance = 520f, minDistance = 120f, maxDistance = 900f;
    public float yaw = 0f, pitch = 28f;
    [Tooltip("Viewing angle when fully zoomed in: the camera rises so the plots and buildings are seen from above.")]
    public float zoomedPitch = 60f;
    [Tooltip("Half width of the map (metres) the camera may show; panning stops before the view reaches the broken terrain beyond it.")]
    public float visibleHalfWidth = 540f;
    public Vector2 zoomedZLimits = new Vector2(-20f, 340f);
    public Vector2 xLimits = new Vector2(-480f, 480f), zLimits = new Vector2(-120f, 420f);
    public float panSpeed = 0.9f, zoomSpeed = 0.05f, smoothing = 10f;

    Vector3 targetFocus, homeFocus;
    float targetDistance, homeDistance, homePitch;
    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetFocus = homeFocus = focus;
        targetDistance = homeDistance = distance;
        homePitch = pitch;
        maxDistance = homeDistance;                          // zooming out brings the camera back to the overview
        Apply(true);
    }

    static bool OverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void Update()
    {
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb != null)
        {
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += fwd;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move -= fwd;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += right;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= right;
            if (move != Vector3.zero) targetFocus += move.normalized * distance * 0.7f * Time.unscaledDeltaTime;
            if (kb.homeKey.wasPressedThisFrame) { targetFocus = homeFocus; targetDistance = homeDistance; }
        }
        if (mouse != null)
        {
            if ((mouse.middleButton.isPressed || mouse.rightButton.isPressed) && !OverUi())
            {
                Vector2 d = mouse.delta.ReadValue();
                Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                targetFocus -= (right * d.x + fwd * d.y) * distance * 0.0016f * panSpeed;
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !OverUi()) targetDistance = Mathf.Clamp(targetDistance * (1f - Mathf.Sign(scroll) * zoomSpeed), minDistance, maxDistance);
        }
        // the closer the view, the further it may pan; fully zoomed out it sits on the overview
        float zoom = ZoomOut01(targetDistance);
        float aspect = cam != null ? cam.aspect : 16f / 9f, tanHalf = Mathf.Tan((cam != null ? cam.fieldOfView : 40f) * 0.5f * Mathf.Deg2Rad);
        float halfW = targetDistance * tanHalf * aspect * 0.95f;
        float xr = Mathf.Max(0f, visibleHalfWidth - halfW);
        targetFocus.x = Mathf.Clamp(targetFocus.x, Mathf.Max(xLimits.x, -xr), Mathf.Min(xLimits.y, xr));
        float zMin = Mathf.Lerp(zoomedZLimits.x, homeFocus.z - 30f, zoom), zMax = Mathf.Lerp(zoomedZLimits.y, homeFocus.z + 30f, zoom);
        targetFocus.z = Mathf.Clamp(targetFocus.z, zMin, zMax);
        Apply(false);
    }

    void Apply(bool snap)
    {
        float k = snap ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        focus = Vector3.Lerp(focus, targetFocus, k);
        distance = Mathf.Lerp(distance, targetDistance, k);
        // zooming in lifts the camera: the view tilts from the overview angle to a high top-down angle early (the curve is steep near the overview
        // distance), so the plots can be studied from above while the zoom itself is still wide
        float curPitch = Mathf.Lerp(zoomedPitch, homePitch, Mathf.SmoothStep(0f, 1f, Mathf.Pow(ZoomOut01(distance), 2.4f)));
        var rot = Quaternion.Euler(curPitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = focus - rot * Vector3.forward * distance;
    }

    // 0 = fully zoomed in, 1 = at the overview distance (logarithmic, so every wheel step feels the same)
    float ZoomOut01(float d)
    {
        float lo = Mathf.Log(Mathf.Max(1f, minDistance)), hi = Mathf.Log(Mathf.Max(minDistance + 1f, homeDistance));
        return Mathf.Clamp01((Mathf.Log(Mathf.Max(1f, d)) - lo) / (hi - lo));
    }
}
