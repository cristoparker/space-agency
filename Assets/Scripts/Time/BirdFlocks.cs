using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient life: now and then a small flock of very low-poly birds flies across the sky in a loose V. Only in daylight, one flock at a time,
/// and it follows the game clock (frozen while paused, faster at fast speeds, but never more than a few times faster so it stays readable).
/// The model is Resources/Ambience/Bird (17 triangles: body and two wings), the material Resources/Ambience/Bird.
/// </summary>
public class BirdFlocks : MonoBehaviour
{
    class Bird { public Transform root, wingL, wingR; public Vector3 offset; public float phase, rate; }

    GameObject prefab;
    Material material;
    readonly List<Bird> birds = new List<Bird>();
    Transform flock;
    Vector3 dir;
    float wait, life;
    Vector3 start;
    float pathLength;

    public static void Create()
    {
        if (FindAnyObjectByType<BirdFlocks>() != null) return;
        new GameObject("Bird Flocks").AddComponent<BirdFlocks>();
    }

    void Start()
    {
        prefab = Resources.Load<GameObject>("Ambience/Bird");
        material = Resources.Load<Material>("Ambience/Bird");
        if (prefab == null) { enabled = false; return; }
        wait = Random.Range(8f, 25f);                                   // the first flock comes soon after the start
    }

    static float Daylight()
    {
        var c = GameClock.Instance;
        if (c == null) return 1f;
        float h = c.DayProgress * 24f;
        return h > 7f && h < 18f ? 1f : 0f;
    }

    float Dt()
    {
        var c = GameClock.Instance;
        float speed = c != null ? c.Speed : 1f;
        return Time.deltaTime * Mathf.Min(speed, 3f);
    }

    void Update()
    {
        float dt = Dt();
        if (dt <= 0f) return;
        if (flock == null)
        {
            wait -= dt;
            if (wait <= 0f) { if (Daylight() > 0f) Spawn(); else wait = 20f; }
            return;
        }
        life -= dt;
        flock.position += dir * dt;
        float t = Time.time;
        foreach (var b in birds)
        {
            float a = -16f + Mathf.Sin((t + b.phase) * b.rate) * 40f;                  // wings rest a little raised (so they read from the side) and beat down/up
            b.wingL.localRotation = Quaternion.Euler(0f, a, 0f);
            b.wingR.localRotation = Quaternion.Euler(0f, -a, 0f);
            b.root.localPosition = b.offset + Vector3.up * Mathf.Sin((t + b.phase) * b.rate * 0.5f) * 0.35f;     // a gentle bob with each beat
        }
        if (life <= 0f || (flock.position - start).sqrMagnitude > pathLength * pathLength) { Destroy(flock.gameObject); flock = null; birds.Clear(); wait = Random.Range(40f, 100f); }
    }

    void Spawn()
    {
        flock = new GameObject("Bird Flock").transform;
        // The flock is laid out FOR the camera, so the player actually sees it cross:
        //  * looking down from above: it passes in the air between the camera and the ground, in the middle of the picture (seen from above);
        //  * oblique or far away: it passes close to the camera, in the upper part of the picture (against the sky).
        // Its size follows its distance, so it always looks like the same small flock on the screen.
        var cam = Camera.main;
        if (cam == null) { Destroy(flock.gameObject); flock = null; wait = 30f; return; }
        var ct = cam.transform;
        bool topDown = Vector3.Angle(ct.forward, Vector3.down) < 50f;
        float groundDist = ct.forward.y < -0.05f ? Mathf.Max(30f, (ct.position.y - 6f) / -ct.forward.y) : 400f;      // where the middle of the picture meets the ground
        float d = topDown ? groundDist * Random.Range(0.35f, 0.6f) : Mathf.Min(Random.Range(80f, 170f), groundDist * 0.6f);
        float vy = topDown ? Random.Range(0.35f, 0.7f) : Random.Range(0.55f, 0.85f);
        Vector3 point = cam.ViewportPointToRay(new Vector3(Random.Range(0.35f, 0.65f), vy, 0f)).GetPoint(d);
        point.y = Mathf.Max(point.y, 25f);
        Vector3 across = Vector3.ProjectOnPlane(ct.right, Vector3.up); if (across.sqrMagnitude < 0.01f) across = Vector3.right; across.Normalize();
        float sign = Random.value < 0.5f ? 1f : -1f;
        float halfWidth = d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;      // half of what the camera sees at that distance
        float length = halfWidth * 2f + 120f;
        float speed = length / Random.Range(10f, 15f);                                           // it takes 10 to 15 seconds to cross
        start = point - across * sign * (length * 0.5f);
        flock.position = start;
        dir = (across * sign + Vector3.up * Random.Range(-0.02f, 0.04f)).normalized * speed;
        life = length / speed + 3f;
        pathLength = length;
        flock.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        int n = Random.Range(5, 10);
        float scale = Mathf.Clamp(d * 0.05f, 2f, 18f) * Random.Range(0.85f, 1.15f);
        for (int i = 0; i < n; i++)
        {
            var holder = new GameObject("Bird " + i).transform;
            holder.SetParent(flock, false);
            holder.localScale = Vector3.one * scale;
            var model = Instantiate(prefab, holder);
            // keep the model's own import rotation (the FBX root is turned 270 degrees about X, which lays the body flat, beak towards -Z), then turn it 180 degrees about Y to face the flight direction
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
            var rs = model.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in rs) { if (material != null) r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            Transform wl = null, wr = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>()) { if (t.name == "WingL") wl = t; else if (t.name == "WingR") wr = t; }
            if (wl == null || wr == null) { Destroy(holder.gameObject); continue; }
            // loose V: the leader in front, the others in pairs behind and to the sides, each a little off
            int row = (i + 1) / 2; float side = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f);
            var off = new Vector3(side * row * 5.5f * scale * 0.5f + Random.Range(-0.8f, 0.8f), Random.Range(-0.6f, 0.6f), -row * 4.5f * scale * 0.5f + Random.Range(-0.8f, 0.8f));
            holder.localPosition = Vector3.zero;
            birds.Add(new Bird { root = holder, wingL = wl, wingR = wr, offset = off, phase = Random.Range(0f, 6f), rate = Random.Range(8f, 11f) });
        }
    }
}
