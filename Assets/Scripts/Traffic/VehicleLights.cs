using UnityEngine;

/// <summary>
/// Headlights (white, with a real spot light at night) and red tail lights on a vehicle. The glowing lamp lenses are always
/// visible; the beams light up the road when it is dark (same night curve as NightLamp). Added by TrafficManager on spawn.
/// </summary>
public class VehicleLights : MonoBehaviour
{
    Light[] beams, rears;
    float level;

    public static void Attach(GameObject car, Material head, Material tail)
    {
        // car size in its own space (forward = +Z)
        var rs = car.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var min = new Vector3(1e9f, 1e9f, 1e9f); var max = -min;
        foreach (var r in rs)
        {
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var l = car.transform.InverseTransformPoint(c);
                min = Vector3.Min(min, l); max = Vector3.Max(max, l);
            }
        }
        float sx = Mathf.Max(0.2f, (max.x - min.x) * 0.5f - 0.32f);
        float y = min.y + Mathf.Min(0.74f, (max.y - min.y) * 0.45f);
        float zF = max.z + 0.02f, zB = min.z - 0.02f;
        var vl = car.AddComponent<VehicleLights>();
        var beams = new Light[2]; var rears = new Light[2];
        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0 ? -1f : 1f) * sx;
            Lens("Headlight", car.transform, new Vector3(x, y, zF), 180f, head, new Vector2(0.42f, 0.2f));
            Lens("Taillight", car.transform, new Vector3(x, y, zB), 0f, tail, new Vector2(0.42f, 0.16f));

            var g = new GameObject("Beam"); g.transform.SetParent(car.transform, false);
            g.transform.localPosition = new Vector3(x, y + 0.1f, zF); g.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            var s = g.AddComponent<Light>();
            s.type = LightType.Spot; s.spotAngle = 70f; s.innerSpotAngle = 30f; s.range = 30f; s.color = new Color(1f, 0.95f, 0.82f);
            s.intensity = 0f; s.shadows = LightShadows.None; s.enabled = false;
            beams[i] = s;

            var rg = new GameObject("Tail Glow"); rg.transform.SetParent(car.transform, false);
            rg.transform.localPosition = new Vector3(x, y, zB - 0.25f);
            var p = rg.AddComponent<Light>();
            p.type = LightType.Point; p.range = 5f; p.color = new Color(1f, 0.1f, 0.06f);
            p.intensity = 0f; p.shadows = LightShadows.None; p.enabled = false;
            rears[i] = p;
        }
        vl.beams = beams; vl.rears = rears;
    }

    static void Lens(string name, Transform parent, Vector3 pos, float yaw, Material m, Vector2 size)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos; q.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    void Update()
    {
        bool night = NightLamp.IsNight(0.12f);
        level = Mathf.MoveTowards(level, night ? 1f : 0f, Time.unscaledDeltaTime / 1.2f);
        bool on = level > 0.001f;
        foreach (var l in beams) { if (l == null) continue; l.enabled = on; l.intensity = 320f * level; }
        foreach (var l in rears) { if (l == null) continue; l.enabled = on; l.intensity = 5f * level; }
    }
}
