using UnityEngine;

/// <summary>
/// Headlights (white, with a real spot light at night) and red tail lights on a vehicle. The glowing lamp lenses are always
/// visible; the beams light up the road when it is dark (same night curve as NightLamp). Added by TrafficManager on spawn.
/// </summary>
public class VehicleLights : MonoBehaviour
{
    Light[] beams, rears;
    TrailRenderer[] trails;
    float level;
    static Material trailMat;

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
        var beams = new Light[2]; var rears = new Light[0]; var trails = new TrailRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0 ? -1f : 1f) * sx;
            Lens("Headlight", car.transform, new Vector3(x, y, zF), 180f, head, new Vector2(0.42f, 0.2f));
            Lens("Taillight", car.transform, new Vector3(x, y, zB), 0f, tail, new Vector2(0.42f, 0.16f));

            var tg = new GameObject("Tail Trail"); tg.transform.SetParent(car.transform, false);
            tg.transform.localPosition = new Vector3(x, y, zB - 0.05f);
            var tr = tg.AddComponent<TrailRenderer>();
            if (trailMat == null)
            {
                var sh = Shader.Find("SpaceAgency/Trail");
                trailMat = sh != null ? new Material(sh) { name = "Tail Trail" } : new Material(Shader.Find("Sprites/Default"));
            }
            tr.sharedMaterial = trailMat; tr.time = 1.6f; tr.startWidth = 0.75f; tr.endWidth = 0.05f; tr.minVertexDistance = 0.3f; tr.alignment = LineAlignment.View;
            tr.textureMode = LineTextureMode.Stretch; tr.numCornerVertices = 2; tr.numCapVertices = 2;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false; tr.emitting = false;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.32f, 0.18f), 0f), new GradientColorKey(new Color(1f, 0.06f, 0.03f), 0.5f), new GradientColorKey(new Color(0.8f, 0.02f, 0.02f), 1f) },
                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.55f, 0.4f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = gr;
            trails[i] = tr;

            var g = new GameObject("Beam"); g.transform.SetParent(car.transform, false);
            g.transform.localPosition = new Vector3(x, y + 0.1f, zF); g.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            var s = g.AddComponent<Light>();
            s.type = LightType.Spot; s.spotAngle = 62f; s.innerSpotAngle = 12f; s.range = 38f; s.color = LightKit.Headlight; s.cookie = LightKit.Beam;
            s.intensity = 0f; s.shadows = LightShadows.None; s.enabled = false;
            beams[i] = s;

        }
        vl.beams = beams; vl.rears = rears; vl.trails = trails;
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

    /// <summary>Called when the car is moved somewhere else, so the trail does not stretch across the map.</summary>
    public void ResetTrails() { if (trails != null) foreach (var t in trails) if (t != null) t.Clear(); }

    void Update()
    {
        bool fast = GameClock.Instance != null && GameClock.Instance.Speed >= 5f;       // red light trails when the game runs fast
        if (trails != null) foreach (var t in trails) if (t != null) t.emitting = fast;
        bool night = NightLamp.IsNight(0.12f);
        level = Mathf.MoveTowards(level, night ? 1f : 0f, Time.unscaledDeltaTime / 1.2f);
        bool on = level > 0.001f;
        foreach (var l in beams) { if (l == null) continue; l.enabled = on; l.intensity = 1500f * level; }
        foreach (var l in rears) { if (l == null) continue; l.enabled = on; l.intensity = 5f * level; }
    }
}
