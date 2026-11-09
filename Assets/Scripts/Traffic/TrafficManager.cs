using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns cars and trucks that wander randomly along the base's dirt roads.
/// The roads are the same curves that were painted on the terrain (Catmull-Rom through the points below),
/// merged into one graph so vehicles can turn at junctions. Vehicle speed follows the game's time scale
/// (Time.deltaTime), so they stop when paused and go fast when the game is fast.
/// </summary>
public class TrafficManager : MonoBehaviour
{
    [System.Serializable]
    public class Road { public Vector2[] points; }

    [Header("Vehicles")]
    public GameObject[] vehiclePrefabs;
    [Tooltip("Materials that share the palette but shift its UV so only the body colour changes.")]
    public Material[] bodyMaterials;
    public int vehicleCount = 10;
    public Vector2 speedRange = new Vector2(5f, 8f);
    [Tooltip("Sideways offset from the road centre (drive on the right).")]
    public float laneOffset = 0.75f;
    public float turnSpeed = 6f;
    [Tooltip("Stop when another vehicle is this close in front.")]
    public float followDistance = 4f;

    [Header("Roads (world X,Z) - match the painted terrain paths")]
    public Road[] roads = DefaultRoads();

    static Road R(params float[] xz)
    {
        var p = new Vector2[xz.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
        return new Road { points = p };
    }

    static Road[] DefaultRoads() => new[]
    {
        R(-2,-45, -2,-30, -10,-18, 0,-8, 4,-3, 8,0),
        R(-50,13, -30,12, -18,9, -8,5, 0,-2, 8,0),
        R(8,0, 16,3, 24,16, 40,22, 60,22),
        R(8,0, 10,18, 14,32, 12,50),
        R(-18,9, -18,17),
        R(-8,5, -18,0),
        R(16,3, 20,-12),
    };

    // ---- graph ----
    readonly List<Vector2> nodes = new List<Vector2>();
    readonly List<List<int>> links = new List<List<int>>();
    readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();
    readonly List<Vehicle> vehicles = new List<Vehicle>();
    Terrain terrain;

    const float Spacing = 0.5f, Merge = 0.25f;

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        BuildGraph();
    }

    void Start()
    {
        if (vehiclePrefabs == null || vehiclePrefabs.Length == 0 || nodes.Count < 2) return;
        for (int i = 0; i < vehicleCount; i++) Spawn(i);
    }

    // ----- graph building -----
    int NodeAt(Vector2 p)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(p.x / Spacing), Mathf.FloorToInt(p.y / Spacing));
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (grid.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dz), out var list))
                    foreach (int id in list)
                        if ((nodes[id] - p).sqrMagnitude < Merge * Merge) return id;
        nodes.Add(p);
        links.Add(new List<int>());
        int n = nodes.Count - 1;
        if (!grid.TryGetValue(cell, out var l)) grid[cell] = l = new List<int>();
        l.Add(n);
        return n;
    }

    void Link(int a, int b)
    {
        if (a == b) return;
        if (!links[a].Contains(b)) links[a].Add(b);
        if (!links[b].Contains(a)) links[b].Add(a);
    }

    void BuildGraph()
    {
        foreach (var road in roads)
        {
            var pts = road.points;
            int prev = -1;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];
                int steps = Mathf.CeilToInt(Vector2.Distance(p1, p2) / Spacing);
                for (int s = 0; s <= steps; s++)
                {
                    float u = s / (float)steps, u2 = u * u, u3 = u2 * u;
                    Vector2 q = 0.5f * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (-p0 + 3 * p1 - 3 * p2 + p3) * u3);
                    int id = NodeAt(q);
                    if (prev >= 0) Link(prev, id);
                    prev = id;
                }
            }
        }
    }

    // ----- spawning -----
    void Spawn(int index)
    {
        var prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        var go = Instantiate(prefab, transform);
        go.name = prefab.name + " " + index;
        if (bodyMaterials != null && bodyMaterials.Length > 0)
        {
            var m = bodyMaterials[Random.Range(0, bodyMaterials.Length)];
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
        }

        int from = Random.Range(0, nodes.Count);
        while (links[from].Count == 0) from = Random.Range(0, nodes.Count);
        int to = links[from][Random.Range(0, links[from].Count)];
        var v = new Vehicle
        {
            tr = go.transform, from = from, to = to,
            t = Random.value * Vector2.Distance(nodes[from], nodes[to]),
            speed = Random.Range(speedRange.x, speedRange.y),
        };
        v.heading = Dir(from, to);
        vehicles.Add(v);
        Place(v, true);
    }

    Vector2 Dir(int a, int b) => (nodes[b] - nodes[a]).normalized;

    // ----- movement -----
    void Update()
    {
        float dt = Time.deltaTime;          // already scaled by the game speed (and 0 when paused)
        if (dt <= 0f) return;

        foreach (var v in vehicles)
        {
            // slow down if something is just ahead in our lane
            float target = v.speed;
            Vector3 fwd = v.tr.forward;
            foreach (var o in vehicles)
            {
                if (o == v) continue;
                Vector3 d = o.tr.position - v.tr.position;
                float ahead = Vector3.Dot(d, fwd);
                if (ahead <= 0f || ahead > followDistance) continue;
                if (Mathf.Abs(Vector3.Dot(d, v.tr.right)) > 1.1f) continue;
                if (Vector3.Dot(o.tr.forward, fwd) < 0.3f) continue;
                target = Mathf.Min(target, Mathf.Max(0f, (ahead - 2.2f) * 2f));
            }
            v.cur = Mathf.MoveTowards(v.cur, target, 14f * dt);

            float move = v.cur * dt;
            while (move > 0f)
            {
                float len = Vector2.Distance(nodes[v.from], nodes[v.to]);
                float left = len - v.t;
                if (move < left) { v.t += move; break; }
                move -= left;
                int arrived = v.to, came = v.from;
                var opts = links[arrived];
                int next = came;                                   // dead end: turn around
                if (opts.Count > 1)
                {
                    do { next = opts[Random.Range(0, opts.Count)]; } while (next == came);
                }
                v.from = arrived; v.to = next; v.t = 0f;
            }
            v.heading = Vector2.Lerp(v.heading, Dir(v.from, v.to), Mathf.Clamp01(turnSpeed * dt)).normalized;
            Place(v, false);
        }
    }

    void Place(Vehicle v, bool snap)
    {
        float len = Vector2.Distance(nodes[v.from], nodes[v.to]);
        Vector2 c = Vector2.Lerp(nodes[v.from], nodes[v.to], len > 0f ? v.t / len : 0f);
        Vector2 right = new Vector2(v.heading.y, -v.heading.x);
        Vector2 p = c + right * laneOffset;
        float yaw = Mathf.Atan2(v.heading.x, v.heading.y) * Mathf.Rad2Deg;

        float h0 = Height(p), hAhead = Height(p + v.heading * 1.3f), hBack = Height(p - v.heading * 1.3f);
        float pitch = Mathf.Atan2(hAhead - hBack, 2.6f) * Mathf.Rad2Deg;
        var pos = new Vector3(p.x, h0, p.y);
        var rot = Quaternion.Euler(-pitch, yaw, 0f);
        if (snap) v.tr.SetPositionAndRotation(pos, rot);
        else v.tr.SetPositionAndRotation(pos, Quaternion.Slerp(v.tr.rotation, rot, 0.5f));
    }

    float Height(Vector2 p)
    {
        if (terrain == null) return 0f;
        return terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
    }

    class Vehicle
    {
        public Transform tr;
        public int from, to;
        public float t, speed, cur;
        public Vector2 heading;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var road in roads)
            for (int i = 0; i < road.points.Length - 1; i++)
                Gizmos.DrawLine(new Vector3(road.points[i].x, 8f, road.points[i].y), new Vector3(road.points[i + 1].x, 8f, road.points[i + 1].y));
    }
#endif
}
