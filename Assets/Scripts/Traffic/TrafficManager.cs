using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a few cars and trucks that wander randomly along the base's dirt roads.
/// The roads are the same curves painted on the terrain (Catmull-Rom through the points below), merged into one
/// graph so vehicles can turn at junctions. Road points that fall inside a building (or any blocker) are
/// removed from the graph, so vehicles never drive through buildings. Speed follows the game's time scale
/// (Time.deltaTime): vehicles stop when paused and go fast when the game is fast.
/// </summary>
public class TrafficManager : MonoBehaviour
{
    [System.Serializable]
    public class Road { public Vector2[] points; }

    [Header("Vehicles")]
    public GameObject[] vehiclePrefabs;
    [Tooltip("Materials that share the palette but shift its UV so only the body colour changes.")]
    public Material[] bodyMaterials;
    public int minVehicles = 8;
    public int maxVehicles = 10;
    public Vector2 speedRange = new Vector2(5f, 8f);
    [Tooltip("Sideways offset from the road centre (drive on the right).")]
    public float laneOffset = 0.75f;
    public float turnSpeed = 6f;
    [Tooltip("Stop when another vehicle is this close in front.")]
    public float followDistance = 4f;

    [Header("Obstacles")]
    [Tooltip("Every child of this object blocks the roads under it (its renderer bounds plus the margin).")]
    public Transform obstacleRoot;
    public string[] ignoreObstacleNames = { "LaunchPad" };
    [Tooltip("Clearance around a building: car half-width + lane offset + some room.")]
    public float obstacleMargin = 2.5f;
    [Tooltip("Extra blocked rectangles: x = centre X, y = centre Z, z = half width X, w = half width Z (margin included).")]
    public Vector4[] extraBlockers;

    [Header("Roads (world X,Z) - match the painted terrain paths")]
    public Road[] roads = DefaultRoads();

    static Road R(params float[] xz)
    {
        var p = new Vector2[xz.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
        return new Road { points = p };
    }

    public static Road[] DefaultRoads() => new[]
    {
        // spine (south -> north) west of the pad
        R(-2,-60, -2,-35, -2,-18, -2,-11, -2,0, -3,7, -3,18, -3,28, -6,45, -8,70, -6,100, -4,125),
        // south avenue (west -> east), passes south of the pad and the admin office
        R(-125,-12, -90,-5, -50,-6, -28,-7, -12,-9, -2,-11, 10,-13, 14,-20, 22,-24, 34,-22),
        // east connector
        R(34,-22, 40,-10, 42,6, 40,20, 36,27),
        // north avenue
        R(-3,28, 10,30, 24,31, 36,27),
        // long roads leaving the base
        R(36,27, 55,24, 80,30, 110,22, 130,28),
        R(34,-22, 55,-25, 80,-15, 105,-22, 130,-12),
        // west avenue
        R(-3,7, -14,7, -30,9, -55,12, -90,10, -125,16),
        // short spurs to building fronts
        R(42,6, 31,8),
        R(22,-24, 22,-19.5f),
    };

    // ---- graph ----
    readonly List<Vector2> nodes = new List<Vector2>();
    readonly List<List<int>> links = new List<List<int>>();
    readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();
    readonly List<Vehicle> vehicles = new List<Vehicle>();
    readonly List<int> spawnNodes = new List<int>();
    Terrain terrain;

    const float Spacing = 0.5f, Merge = 0.25f;

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        BuildGraph();
        RemoveBlocked();
        FindSpawnNodes();
    }

    void Start()
    {
        if (vehiclePrefabs == null || vehiclePrefabs.Length == 0 || spawnNodes.Count == 0) return;
        int count = Random.Range(minVehicles, maxVehicles + 1);
        for (int i = 0; i < count; i++) Spawn(i);
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

    // ----- obstacles -----
    List<Vector4> CollectBlockers()
    {
        var rects = new List<Vector4>();
        if (obstacleRoot != null)
        {
            foreach (Transform child in obstacleRoot)
            {
                if (System.Array.IndexOf(ignoreObstacleNames, child.name) >= 0) continue;
                bool any = false; Bounds b = default;
                foreach (var r in child.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (any) rects.Add(new Vector4(b.center.x, b.center.z, b.extents.x + obstacleMargin, b.extents.z + obstacleMargin));
            }
        }
        if (extraBlockers != null) rects.AddRange(extraBlockers);
        return rects;
    }

    void RemoveBlocked()
    {
        var rects = CollectBlockers();
        int removed = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            foreach (var r in rects)
            {
                if (Mathf.Abs(nodes[i].x - r.x) < r.z && Mathf.Abs(nodes[i].y - r.y) < r.w)
                {
                    foreach (int n in links[i]) links[n].Remove(i);
                    links[i].Clear();
                    removed++;
                    break;
                }
            }
        }
        Debug.Log($"TrafficManager: {nodes.Count} road nodes, {removed} blocked by {rects.Count} obstacles.");
    }

    // only spawn on connected stretches of road that are long enough to be worth driving
    void FindSpawnNodes()
    {
        var comp = new int[nodes.Count];
        var stack = new Stack<int>();
        int id = 0;
        var sizes = new List<int> { 0 };
        for (int i = 0; i < nodes.Count; i++)
        {
            if (comp[i] != 0 || links[i].Count == 0) continue;
            id++; int size = 0;
            comp[i] = id; stack.Push(i);
            while (stack.Count > 0)
            {
                int n = stack.Pop(); size++;
                foreach (int m in links[n]) if (comp[m] == 0) { comp[m] = id; stack.Push(m); }
            }
            sizes.Add(size);
        }
        for (int i = 0; i < nodes.Count; i++)
            if (comp[i] != 0 && sizes[comp[i]] >= 200) spawnNodes.Add(i);
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

        int from = spawnNodes[Random.Range(0, spawnNodes.Count)];
        int to = links[from][Random.Range(0, links[from].Count)];
        var v = new Vehicle
        {
            tr = go.transform, from = from, to = to,
            t = 0f,
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
        Gizmos.color = Color.red;
        if (extraBlockers != null)
            foreach (var r in extraBlockers) Gizmos.DrawWireCube(new Vector3(r.x, 8f, r.y), new Vector3(r.z * 2f, 0.1f, r.w * 2f));
    }
#endif
}
