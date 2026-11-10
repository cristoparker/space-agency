using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The cinematic camera. The interface hides and the camera tours the base on its own: it always starts low over the water to catch the
/// reflections, then cuts between random shots: following a car, panning over a building, circling the launch complex, walking along the
/// road at eye level, looking up at the rocket from the side, or gliding over the whole site. Every shot stays inside the map so the
/// edge of the terrain is never in the picture. Click or Esc leaves.
/// </summary>
public class CinematicCamera : MonoBehaviour
{
    public static bool Active { get; private set; }
    static CinematicCamera inst;
    public static void ResetStatics() { Active = false; inst = null; }

    static readonly Vector3 PadCenter = new Vector3(-187f, 6f, 161f);
    // the area the camera may be in (metres): inside it every view is backed by the base, the water or the mountains
    const float MinX = -470f, MaxX = 470f, MinZ = -150f, MaxZ = 400f, WaterY = 3.5f;

    struct Pose { public Vector3 pos, look; public float fov; }

    Camera cam;
    CameraRig rig;
    HudController hud;
    RectTransform overlay;
    Image fade;
    TMP_Text hint;
    Vector3 camPos; Quaternion camRot; float camFov;
    bool stop, labelsWere;
    float fadeAlpha = 1f, hintUntil;
    Terrain terrain;
    int lastShot = -1;

    public static void Begin(HudController hud, RectTransform canvas)
    {
        if (Active) return;
        if (inst == null) inst = new GameObject("Cinematic Camera").AddComponent<CinematicCamera>();
        inst.hud = hud;
        inst.StartCoroutine(inst.Run(canvas));
    }

    float Ground(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : 6f;

    IEnumerator Run(RectTransform canvas)
    {
        Active = true; stop = false;
        cam = Camera.main;
        if (cam == null) { Active = false; yield break; }
        terrain = Terrain.activeTerrain;
        rig = cam.GetComponent<CameraRig>();
        camPos = cam.transform.position; camRot = cam.transform.rotation; camFov = cam.fieldOfView;
        if (rig != null) rig.enabled = false;
        labelsWere = BuildingTags.Visible;
        hud.HideInterface();
        BuildingTags.Visible = false;
        BuildOverlay(canvas);
        fadeAlpha = 1f; fade.color = Color.black;
        hintUntil = Time.unscaledTime + 4f;

        yield return Shot(0);                                      // always: low over the water, looking at the reflection
        while (!stop)
        {
            int n;
            do { n = Random.Range(1, 7); } while (n == lastShot);
            lastShot = n;
            yield return Shot(n);
        }
        // ---- leave
        yield return null;
        cam.transform.position = camPos; cam.transform.rotation = camRot; cam.fieldOfView = camFov;
        if (rig != null) rig.enabled = true;
        BuildingTags.Visible = labelsWere;
        if (overlay != null) Destroy(overlay.gameObject);
        hud.ShowInterface();
        Active = false;
    }

    void BuildOverlay(RectTransform canvas)
    {
        overlay = new GameObject("Cinematic Overlay", typeof(RectTransform)).GetComponent<RectTransform>();
        overlay.SetParent(canvas, false); UIKit.Fill(overlay); overlay.SetAsLastSibling();
        var click = new GameObject("Catcher", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        click.SetParent(overlay, false); UIKit.Fill(click);
        var ci = click.GetComponent<Image>(); ci.color = Color.clear;
        var b = click.GetComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = ci; b.onClick.AddListener(() => stop = true);
        var f = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        f.SetParent(overlay, false); UIKit.Fill(f);
        fade = f.GetComponent<Image>(); fade.color = Color.black; fade.raycastTarget = false;
        hint = UIKit.TextAt(overlay, "Hint", 0, 0, 220, 12, "Cinematic camera  ·  click or press Esc to leave", 6f, TextAlignmentOptions.Center, true, new Color(1f, 1f, 1f, 0.85f));
        var hr = (RectTransform)hint.transform.parent; hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f); hr.pivot = new Vector2(0.5f, 0f); hr.anchoredPosition = new Vector2(0f, 10f);
        hint.raycastTarget = false;
    }

    void Update()
    {
        if (!Active) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) stop = true;
        if (hint != null) hint.color = new Color(1f, 1f, 1f, Mathf.Clamp01((hintUntil - Time.unscaledTime) / 0.8f) * 0.85f);
    }

    // ---- plays one shot: fade in, camera poses over `dur` seconds, fade out
    IEnumerator Play(float dur, System.Func<float, Pose> pose, float smooth = 0f)
    {
        float t = 0f; bool first = true;
        Vector3 pos = default, look = default;
        while (t < dur && !stop)
        {
            float dt = Time.unscaledDeltaTime; t += dt;
            var p = pose(Mathf.Clamp01(t / dur));
            if (p.fov < 0f) break;                                              // the shot says it is over (e.g. the car it follows is gone)
            Vector3 targetPos = p.pos, targetLook = p.look;
            targetPos.x = Mathf.Clamp(targetPos.x, MinX, MaxX); targetPos.z = Mathf.Clamp(targetPos.z, MinZ, MaxZ);
            targetPos.y = Mathf.Max(targetPos.y, Mathf.Max(WaterY + 1.2f, Ground(targetPos) + 1.4f));
            if (first || smooth <= 0f) { pos = targetPos; look = targetLook; }
            else { float k = 1f - Mathf.Exp(-smooth * dt); pos = Vector3.Lerp(pos, targetPos, k); look = Vector3.Lerp(look, targetLook, k); }
            first = false;
            cam.transform.position = pos;
            var dir = look - pos; if (dir.sqrMagnitude > 0.01f) cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, p.fov, first ? 1f : 0.2f);
            float fin = Mathf.Clamp01(t / 0.8f), fout = Mathf.Clamp01((dur - t) / 0.8f);
            fadeAlpha = 1f - Mathf.Min(fin, fout);
            fade.color = new Color(0f, 0f, 0f, fadeAlpha);
            yield return null;
        }
        // quick fade out when ended early
        float a = fadeAlpha;
        while (a < 1f) { a += Time.unscaledDeltaTime * 3f; fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a)); yield return null; }
    }

    IEnumerator Shot(int n)
    {
        float s = Random.value < 0.5f ? 1f : -1f;
        switch (n)
        {
            case 0:     // low over the water, gliding along the shore: the reflections of the base lie in front of the lens
            {
                float x0 = s > 0 ? -330f : 330f, x1 = -x0 * 0.35f, z = -125f;
                yield return Play(11f, u => new Pose
                {
                    pos = new Vector3(Mathf.Lerp(x0, x1, u), WaterY + 3.2f + Mathf.Sin(u * 6f) * 0.25f, z),
                    look = new Vector3(Mathf.Lerp(x0, x1, u) + 60f * s * -1f, 16f, 70f), fov = 58f,
                });
                break;
            }
            case 1:     // high orbit around the whole site
            {
                Vector3 mid = new Vector3(-20f, 6f, 150f);
                float a0 = Random.Range(150f, 210f);
                yield return Play(14f, u =>
                {
                    float a = (a0 + 70f * s * u) * Mathf.Deg2Rad;
                    return new Pose { pos = mid + new Vector3(Mathf.Sin(a) * 215f, 150f, Mathf.Cos(a) * 215f), look = mid + Vector3.up * 8f, fov = 52f };
                });
                break;
            }
            case 2:     // pan over a building
            {
                var b = PickBuilding();
                Vector3 c = b.center; float r = Mathf.Max(55f, Mathf.Max(b.extents.x, b.extents.z) * 2.2f + 20f);
                float a0 = Random.Range(0f, 360f), sweep = 55f * s;
                yield return Play(10f, u =>
                {
                    float a = (a0 + sweep * u) * Mathf.Deg2Rad;
                    return new Pose { pos = c + new Vector3(Mathf.Sin(a) * r, 24f + b.extents.y, Mathf.Cos(a) * r), look = c + Vector3.up * b.extents.y * 0.6f, fov = 48f };
                });
                break;
            }
            case 3:     // circle the launch complex
            {
                float a0 = Random.Range(-60f, 60f) + 180f;      // mostly from the south side (the camera never leaves the map)
                yield return Play(13f, u =>
                {
                    float a = (a0 + 105f * s * u) * Mathf.Deg2Rad;
                    return new Pose { pos = PadCenter + new Vector3(Mathf.Sin(a) * 165f, 52f + 12f * Mathf.Sin(u * Mathf.PI), Mathf.Cos(a) * 165f), look = PadCenter + Vector3.up * 34f, fov = 44f };
                });
                break;
            }
            case 4:     // a person walking beside the road, looking across at the launch complex
            {
                float z0 = Random.Range(70f, 110f);
                yield return Play(12f, u => new Pose
                {
                    pos = new Vector3(-80f, Ground(new Vector3(-80f, 0f, z0 + 4f * 12f * u)) + 1.7f, z0 + 4f * 12f * u),
                    look = PadCenter + new Vector3(0f, 28f - 12f * u, 0f) + Vector3.forward * (-10f + 20f * u), fov = 68f,
                });
                break;
            }
            case 5:     // low, close to the ground, looking up the side of the rocket
            {
                float a0 = Random.Range(-25f, 25f) + 150f;
                yield return Play(10f, u =>
                {
                    float a = (a0 + 28f * s * u) * Mathf.Deg2Rad;
                    Vector3 p = PadCenter + new Vector3(Mathf.Sin(a) * 85f, 0f, Mathf.Cos(a) * 85f);
                    p.y = Ground(p) + 2.2f;
                    return new Pose { pos = p, look = PadCenter + Vector3.up * Mathf.Lerp(14f, 52f, u), fov = 52f };
                });
                break;
            }
            default:    // glide over the whole site
            {
                float x0 = s > 0 ? -380f : 380f, x1 = -x0 * 0.6f;
                yield return Play(14f, u => new Pose
                {
                    pos = new Vector3(Mathf.Lerp(x0, x1, u), 105f, Mathf.Lerp(-30f, 40f, u)),
                    look = new Vector3(Mathf.Lerp(x0, x1, u) + 140f * s, 6f, 170f), fov = 55f,
                });
                break;
            }
        }
    }

    Bounds PickBuilding()
    {
        var root = GameObject.Find("Base Buildings");
        var list = new List<Bounds>();
        if (root != null)
            foreach (Transform t in root.transform)
            {
                if (!t.gameObject.activeInHierarchy || t.name.Contains("LaunchPad") || t.name.Contains("CarPark")) continue;
                var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer)).ToList();
                if (rs.Count == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.center.x > MinX + 60f && b.center.x < MaxX - 60f) list.Add(b);
            }
        return list.Count > 0 ? list[Random.Range(0, list.Count)] : new Bounds(new Vector3(0f, 10f, 120f), new Vector3(30f, 20f, 30f));
    }
}
