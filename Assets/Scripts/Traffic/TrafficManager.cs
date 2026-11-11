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
    public class Road { public Vector2[] points; public bool highway; }

    [Header("Vehicles")]
    public GameObject[] vehiclePrefabs;
    [Tooltip("Materials that share the palette but shift its UV so only the body colour changes.")]
    public Material[] bodyMaterials;
    [Tooltip("Glowing lens materials for the headlights (white) and tail lights (red).")]
    public Material headLightMaterial, tailLightMaterial;
    [Header("Traffic mix")]
    [Tooltip("Cars on the highway (most of the traffic) and cars driving around inside the facility (only a few).")]
    public int highwayCount = 36;
    public int facilityCount = 5;
    [Tooltip("Cruising speed (m/s) of each driver: fast on the highway, slow inside the facility. Every driver gets their own pace.")]
    public Vector2 highwaySpeed = new Vector2(22f, 38f);
    public Vector2 facilitySpeed = new Vector2(6f, 10f);
    [Tooltip("Sideways offset from the road centre (drive on the right) on the facility roads.")]
    public float laneOffset = 2.9f;
    [Tooltip("Lane centres on the highway, measured to the right of the driving direction from the median.")]
    public float[] highwayLanes = { 3.6f, 8.6f };
    [Tooltip("Stop when another vehicle is this close in front.")]
    public float followDistance = 4f;
    public float turnSpeed = 6f;
    public Vector2 speedRange = new Vector2(5f, 8f);
    public int minVehicles = 8, maxVehicles = 10;

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
    readonly List<byte> cls = new List<byte>();                    // 1 = highway, 2 = facility road, 3 = both (a junction between them)
    readonly List<Vehicle> vehicles = new List<Vehicle>();
    readonly List<int> hwNodes = new List<int>(), facNodes = new List<int>(), hwEnds = new List<int>(), gateNodes = new List<int>();
    Terrain terrain;

    const float Spacing = 0.5f, Merge = 0.25f;

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        BuildGraph();
        RemoveBlocked();
        FindSpawnNodes();
    }

    /// <summary>A random car for the cinematic camera to follow (null when there are none). Facility cars are preferred.</summary>
    public Transform PickVehicle(bool preferFacility)
    {
        var list = new List<Transform>();
        foreach (var v in vehicles) if (v.placed && v.tr != null && (!preferFacility || !v.hw)) list.Add(v.tr);
        if (list.Count == 0) foreach (var v in vehicles) if (v.placed && v.tr != null) list.Add(v.tr);
        return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
    }

    public bool Ready => hwNodes.Count > 0 || facNodes.Count > 0;

    void Start()
    {
        if (vehiclePrefabs == null || vehiclePrefabs.Length == 0 || !Ready) return;
        for (int i = 0; i < highwayCount; i++) Spawn(true, true);
        for (int i = 0; i < facilityCount; i++) Spawn(false, true);
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
        links.Add(new List<int>()); cls.Add(0);
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
                    cls[id] |= (byte)(road.highway ? 1 : 2);
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

        for (int i = 0; i < nodes.Count; i++)
        {
            foreach (var r in rects)
            {
                if (Mathf.Abs(nodes[i].x - r.x) < r.z && Mathf.Abs(nodes[i].y - r.y) < r.w)
                {
                    foreach (int n in links[i]) links[n].Remove(i);
                    links[i].Clear();

                    break;
                }
            }
        }
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
        {
            if (comp[i] == 0 || sizes[comp[i]] < 60) continue;
            if ((cls[i] & 1) != 0) { hwNodes.Add(i); int hl = 0; foreach (int m in links[i]) if ((cls[m] & 1) != 0) hl++; if (hl == 1) hwEnds.Add(i); }
            if ((cls[i] & 2) != 0 && links[i].Count > 1) facNodes.Add(i);
            if (cls[i] == 3) gateNodes.Add(i);
        }
    }

    // ----- spawning -----
    static readonly Vector2[] none = new Vector2[0];

    void Spawn(bool highway, bool anywhere)
    {
        var prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        var go = Instantiate(prefab, transform);
        go.name = prefab.name + " " + vehicles.Count;
        if (bodyMaterials != null && bodyMaterials.Length > 0)
        {
            var m = bodyMaterials[Random.Range(0, bodyMaterials.Length)];
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
        }
        if (headLightMaterial != null && tailLightMaterial != null) VehicleLights.Attach(go, headLightMaterial, tailLightMaterial);
        var v = new Vehicle { tr = go.transform, cruiseHw = Random.Range(highwaySpeed.x, highwaySpeed.y), cruiseFac = Random.Range(facilitySpeed.x, facilitySpeed.y) };
        vehicles.Add(v);
        Respawn(v, highway, anywhere);
    }

    // puts a vehicle on the road: anywhere along it at the start, afterwards at the ends of the highway / at the gate junctions
    void Respawn(Vehicle v, bool highway, bool anywhere)
    {
        if (highway ? hwNodes.Count == 0 : facNodes.Count == 0) return;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int at, to;
            if (highway)
            {
                if (anywhere) at = hwNodes[Random.Range(0, hwNodes.Count)]; else at = hwEnds.Count > 0 ? hwEnds[Random.Range(0, hwEnds.Count)] : hwNodes[Random.Range(0, hwNodes.Count)];
                var opts = new List<int>(); foreach (int m in links[at]) if ((cls[m] & 1) != 0) opts.Add(m);
                if (opts.Count == 0) continue;
                to = opts[Random.Range(0, opts.Count)];
            }
            else
            {
                if (anywhere || gateNodes.Count == 0) at = facNodes.Count > 0 ? facNodes[Random.Range(0, facNodes.Count)] : -1;
                else at = gateNodes[Random.Range(0, gateNodes.Count)];
                if (at < 0) return;
                var opts = new List<int>(); foreach (int m in links[at]) if ((cls[m] & 2) != 0 && (cls[m] != 1)) opts.Add(m);
                if (opts.Count == 0) continue;
                to = opts[Random.Range(0, opts.Count)];
            }
            Vector2 dir = Dir(at, to);
            bool free = true;
            foreach (var o in vehicles) if (o != v && o.placed && (o.pos - nodes[at]).sqrMagnitude < 12f * 12f) { free = false; break; }
            if (!free) continue;
            v.hw = highway; v.planHw = highway; v.deadEnd = false;
            v.route.Clear(); v.route.Add(at); v.route.Add(to);
            v.heading = dir;
            v.lane = v.hw ? (v.cruiseHw > (highwaySpeed.x + highwaySpeed.y) * 0.5f ? 0 : highwayLanes.Length - 1) : 0;
            v.laneCur = LaneTarget(v);
            v.cur = highway ? Mathf.Min(v.cruiseHw, 16f) : v.cruiseFac * 0.6f;
            if (anywhere) v.cur = v.hw ? v.cruiseHw : v.cruiseFac;
            Vector2 right = new Vector2(dir.y, -dir.x);
            v.pos = nodes[at] + right * v.laneCur;
            v.placed = true; v.spin = 0f; v.sinceNode = 0f;
            Extend(v);
            Place(v, true);
            var vl = v.tr.GetComponent<VehicleLights>(); if (vl != null) vl.ResetTrails();
            return;
        }
    }

    float LaneTarget(Vehicle v) => v.hw ? highwayLanes[Mathf.Clamp(v.lane, 0, highwayLanes.Length - 1)] : laneOffset;

    Vector2 Dir(int a, int b) => (nodes[b] - nodes[a]).normalized;

    // ----- route planning: where does the vehicle go at the next junction? -----
    int Choose(Vehicle v, int prev, int at)
    {
        var same = new List<int>(); var other = new List<int>(); var wts = new List<float>();
        int mask = v.planHw ? 1 : 2;
        foreach (int m in links[at])
        {
            if (m == prev) continue;
            if ((cls[m] & mask) != 0 && cls[m] != 0) same.Add(m); else other.Add(m);
        }
        List<int> pick = same;
        if (same.Count == 0) pick = other;
        else if (other.Count > 0 && cls[at] == 3)
        {
            // gate junction: facility cars usually leave onto the highway, highway cars rarely turn into the base
            float pSwitch = v.planHw ? 0.06f : 0.65f;
            if (Random.value < pSwitch) pick = other;
        }
        if (pick.Count == 0) return -1;
        // spurs lead to a building front: avoid them most of the time
        float total = 0f; wts.Clear();
        Vector2 cur = Dir(prev, at);
        foreach (int m in pick)
        {
            float w = 1f;
            if (links[m].Count <= 2 && IsShortSpur(m, at)) w = 0.12f;
            w *= 0.6f + 0.4f * Mathf.Max(0f, Vector2.Dot(cur, Dir(at, m)));       // mild preference for going straight on
            wts.Add(w); total += w;
        }
        float r = Random.value * total;
        for (int i = 0; i < pick.Count; i++) { r -= wts[i]; if (r <= 0f) { Switch(v, pick[i]); return pick[i]; } }
        Switch(v, pick[pick.Count - 1]); return pick[pick.Count - 1];
    }

    void Switch(Vehicle v, int next) { if (cls[next] == 1) v.planHw = true; else if (cls[next] == 2) v.planHw = false; }

    // does the road starting at "from -> first" reach a dead end within ~60 m (a spur to a building)?
    bool IsShortSpur(int first, int from)
    {
        int prev = from, cur = first;
        for (int i = 0; i < 120; i++)
        {
            var l = links[cur];
            if (l.Count != 2) return l.Count <= 1;
            int nx = l[0] == prev ? l[1] : l[0];
            prev = cur; cur = nx;
        }
        return false;
    }

    void Extend(Vehicle v)
    {
        int guard = 0;
        while (!v.deadEnd && guard++ < 200)
        {
            float len = 0f;
            for (int i = 1; i < v.route.Count - 1; i++) len += Vector2.Distance(nodes[v.route[i]], nodes[v.route[i + 1]]);
            if (len >= 34f) break;
            int n = v.route.Count;
            int next = Choose(v, v.route[n - 2], v.route[n - 1]);
            if (next < 0) { v.deadEnd = true; break; }
            v.route.Add(next);
        }
    }

    // point on the route "s" metres ahead of the vehicle's reference node, plus the road direction there
    Vector2 PointAhead(Vehicle v, float s, out Vector2 dir)
    {
        var r = v.route;
        Vector2 a = nodes[r[0]];
        dir = Dir(r[0], r[1]);
        float t = Mathf.Max(0f, Vector2.Dot(v.pos - a, dir));
        s += t;
        for (int i = 0; i + 1 < r.Count; i++)
        {
            Vector2 p0 = nodes[r[i]], p1 = nodes[r[i + 1]];
            float len = Vector2.Distance(p0, p1);
            dir = len > 0f ? (p1 - p0) / len : dir;
            if (s <= len) return p0 + dir * s;
            s -= len;
        }
        return nodes[r[r.Count - 1]];
    }

    // ----- movement -----
    void Update()
    {
        float dt = Time.deltaTime;          // already scaled by the game speed (and 0 when paused)
        if (dt <= 0f) return;
        dt = Mathf.Min(dt, 0.1f);

        for (int vi = 0; vi < vehicles.Count; vi++)
        {
            var v = vehicles[vi];
            if (!v.placed || v.route.Count < 2) { RespawnSame(v); continue; }

            // the class of road we are on now decides our pace
            int curCls = cls[v.route[1]];
            if (curCls == 1) v.hw = true; else if (curCls == 2) v.hw = false;
            float cruise = v.hw ? v.cruiseHw : v.cruiseFac;

            // ---- how fast may we go? corners and junctions ahead, then traffic ahead
            float target = cruise;
            const float latAcc = 3.2f, brake = 4.5f;
            Vector2 prevDir = v.heading, d;
            float acc = 0f;
            for (float s = 2f; s <= 36f; s += 2f)
            {
                PointAhead(v, s, out d);
                float ang = Vector2.Angle(prevDir, d) * Mathf.Deg2Rad;
                prevDir = d; acc = s;
                if (ang < 0.02f) continue;
                float kappa = ang / 2f;
                float vmax = Mathf.Max(3.2f, Mathf.Sqrt(latAcc / kappa));
                target = Mathf.Min(target, Mathf.Sqrt(vmax * vmax + 2f * brake * Mathf.Max(0f, s - 2f)));
            }
            // junctions: slow down in front of every crossing (facility roads only)
            if (!v.hw)
            {
                float dist = Vector2.Distance(v.pos, nodes[v.route[1]]);
                for (int i = 1; i < v.route.Count && dist < 30f; i++)
                {
                    if (links[v.route[i]].Count > 2) target = Mathf.Min(target, Mathf.Sqrt(5f * 5f + 2f * brake * Mathf.Max(0f, dist - 6f)));
                    if (i + 1 < v.route.Count) dist += Vector2.Distance(nodes[v.route[i]], nodes[v.route[i + 1]]);
                }
            }

            // traffic: keep a gap to whoever is in front, give way to cars crossing in front of us
            float look = 7f + v.cur * 1.5f;
            Vehicle leader = null; float leaderGap = 1e9f;
            foreach (var o in vehicles)
            {
                if (o == v || !o.placed) continue;
                Vector2 dd = o.pos - v.pos;
                float ahead = Vector2.Dot(dd, v.heading);
                if (ahead <= 0f || ahead > look) continue;
                float lat = Mathf.Abs(Vector2.Dot(dd, new Vector2(v.heading.y, -v.heading.x)));
                if (lat > 2.3f) continue;
                if (Vector2.Dot(o.heading, v.heading) < -0.6f) continue;       // oncoming in the other lane
                float gap = ahead - 4.6f;
                if (gap < leaderGap) { leaderGap = gap; leader = o; }
            }
            if (leader != null && v.stuck < 3f)
                target = Mathf.Min(target, Mathf.Max(0f, leader.cur + (leaderGap - 3.2f) * 0.9f));
            v.stuck = v.cur < 0.4f && leader != null ? v.stuck + dt : 0f;
            if (v.stuck > 6f) v.stuck = 0f;

            // highway: change lane to get past a slower car if the other lane is free
            v.laneTimer -= dt;
            if (v.hw && v.laneTimer <= 0f)
            {
                v.laneTimer = Random.Range(1.2f, 2.4f);
                if (leader != null && leader.cur < cruise - 3f && leaderGap < 28f) TryLaneChange(v);
                else if (v.lane != (cruise > (highwaySpeed.x + highwaySpeed.y) * 0.5f ? 0 : highwayLanes.Length - 1) && Random.value < 0.25f) TryLaneChange(v);
            }
            v.laneCur = Mathf.MoveTowards(v.laneCur, LaneTarget(v), 2.2f * dt);

            v.cur = Mathf.MoveTowards(v.cur, target, (target > v.cur ? (v.hw ? 4.5f : 3.2f) : 8f) * dt);

            // ---- steer towards a point on our lane a bit ahead (pure pursuit): smooth turns, always facing forward
            float L = 3.5f + v.cur * 0.38f;
            Vector2 tp = PointAhead(v, L, out d);
            tp += new Vector2(d.y, -d.x) * v.laneCur;
            Vector2 want = (tp - v.pos);
            if (want.sqrMagnitude > 0.0001f)
            {
                want.Normalize();
                float angle = Vector2.SignedAngle(v.heading, want) * Mathf.Deg2Rad;
                float maxYaw = Mathf.Max(v.cur, 2.5f) / 3.0f * 0.9f;
                float step = Mathf.Clamp(angle, -maxYaw * dt, maxYaw * dt);
                v.spin += Mathf.Abs(step);
                float cs = Mathf.Cos(step), sn = Mathf.Sin(step);
                v.heading = new Vector2(v.heading.x * cs - v.heading.y * sn, v.heading.x * sn + v.heading.y * cs).normalized;
            }
            v.pos += v.heading * v.cur * dt;

            // ---- passed the next node (or came close to it)? move on along the route
            v.sinceNode += dt;
            while (v.route.Count > 2)
            {
                Vector2 e = Dir(v.route[0], v.route[1]);
                Vector2 toNode = v.pos - nodes[v.route[1]];
                if (Vector2.Dot(toNode, e) > -0.2f || toNode.sqrMagnitude < 2.5f * 2.5f) { v.route.RemoveAt(0); v.spin = 0f; v.sinceNode = 0f; }
                else break;
            }
            // the end of the road: leave once we are there
            if (v.route.Count <= 2 && v.deadEnd && (v.pos - nodes[v.route[v.route.Count - 1]]).sqrMagnitude < 4.5f * 4.5f) { RespawnSame(v); continue; }
            // a car that turns round and round without getting anywhere (or is stuck for a long time) is taken off the road
            if (v.spin > 9f || v.sinceNode > 25f) { RespawnSame(v); continue; }
            Extend(v);
            Place(v, false);
        }
    }

    void TryLaneChange(Vehicle v)
    {
        int other = v.lane == 0 ? highwayLanes.Length - 1 : 0;
        if (other == v.lane) return;
        float side = highwayLanes[other] - highwayLanes[v.lane];
        Vector2 right = new Vector2(v.heading.y, -v.heading.x);
        foreach (var o in vehicles)
        {
            if (o == v || !o.placed) continue;
            Vector2 dd = o.pos - v.pos;
            float ahead = Vector2.Dot(dd, v.heading), lat = Vector2.Dot(dd, right);
            if (ahead > -14f && ahead < 30f && Mathf.Abs(lat - side) < 3.2f) return;      // somebody is in that lane
        }
        v.lane = other;
    }

    // a vehicle reached the end of its road: it leaves and a new one enters at the highway ends / the gates
    void RespawnSame(Vehicle v)
    {
        int hwNow = 0, facNow = 0;
        foreach (var o in vehicles) if (o != v && o.placed) { if (o.hw) hwNow++; else facNow++; }
        bool highway = (highwayCount - hwNow) >= (facilityCount - facNow);
        v.placed = false;
        Respawn(v, highway, false);
    }

    void Place(Vehicle v, bool snap)
    {
        Vector2 p = v.pos;
        float yaw = Mathf.Atan2(v.heading.x, v.heading.y) * Mathf.Rad2Deg;
        float h0 = Height(p), hAhead = Height(p + v.heading * 1.3f), hBack = Height(p - v.heading * 1.3f);
        float pitch = Mathf.Atan2(hAhead - hBack, 2.6f) * Mathf.Rad2Deg;
        var pos = new Vector3(p.x, h0, p.y);
        var rot = Quaternion.Euler(-pitch, yaw, 0f);
        if (snap) v.tr.SetPositionAndRotation(pos, rot);
        else v.tr.SetPositionAndRotation(pos, Quaternion.Slerp(v.tr.rotation, rot, 0.6f));
    }

    float Height(Vector2 p)
    {
        if (terrain == null) return 0f;
        return terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
    }

    class Vehicle
    {
        public Transform tr;
        public List<int> route = new List<int>();
        public Vector2 pos, heading;
        public float cur, cruiseHw, cruiseFac, laneCur, laneTimer, stuck, spin, sinceNode;
        public int lane;
        public bool hw, planHw, deadEnd, placed;
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
