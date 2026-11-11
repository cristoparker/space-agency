using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens the flight scene after a launch: an office with a big monitor on the wall, and the camera looking at it. The monitor plays the simulated flight
/// as a graph of the Earth with the rocket's path. Esc, Space or a click leaves it (it can also be left at any moment). The game scene stays loaded
/// underneath, so nothing is lost; the mission goes on in the interface.
/// </summary>
public static class FlightSceneLauncher
{
    public const string SceneName = "3. Flight";
    public static bool Active { get; private set; }
    public static void ResetStatics() { Active = false; }

    public static FlightRecord Record; public static FlightProfile Profile; static Action done;

    /// <summary>Plays the flight scene, then calls <paramref name="onClosed"/>. Without the scene (or without a profile) it just calls it.</summary>
    public static void Begin(FlightRecord rec, FlightProfile prof, Action onClosed)
    {
        if (Active || prof == null || !Application.CanStreamedLevelBeLoaded(SceneName)) { onClosed?.Invoke(); return; }
        Record = rec; Profile = prof; done = onClosed; Active = true;
        var runner = new GameObject("Flight Scene Runner").AddComponent<Runner>();
        runner.StartCoroutine(runner.Run());
    }

    /// <summary>Called by the scene when the player leaves.</summary>
    public static void Close()
    {
        var r = UnityEngine.Object.FindAnyObjectByType<Runner>();
        if (r != null) r.StartCoroutine(r.Leave());
    }

    class Runner : MonoBehaviour
    {
        Camera gameCam; AudioListener gameListener; DayNightCycle dn; readonly List<Light> lights = new List<Light>();
        int prevSpeed = -1; HudController hud;
        AmbientMode amode; Color aLight, aSky, aEq, aGround; bool fog;

        public IEnumerator Run()
        {
            gameCam = Camera.main; gameListener = gameCam != null ? gameCam.GetComponent<AudioListener>() : null;
            hud = FindAnyObjectByType<HudController>();
            dn = FindAnyObjectByType<DayNightCycle>();
            var clock = GameClock.Instance;
            if (clock != null && clock.Speed > 0f) { prevSpeed = clock.SpeedIndex; clock.SetSpeedIndex(0); }
            if (hud != null) hud.HideInterface();
            // the game's sun, camera and sky must not light or hide the office
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.enabled) { lights.Add(l); l.enabled = false; }
            if (dn != null) dn.enabled = false;
            amode = RenderSettings.ambientMode; aLight = RenderSettings.ambientLight; aSky = RenderSettings.ambientSkyColor; aEq = RenderSettings.ambientEquatorColor; aGround = RenderSettings.ambientGroundColor; fog = RenderSettings.fog;
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.42f);
            if (gameCam != null) gameCam.enabled = false;
            if (gameListener != null) gameListener.enabled = false;

            var op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            if (op == null) { yield return Leave(); yield break; }
            while (!op.isDone) yield return null;
        }

        public IEnumerator Leave()
        {
            var sc = SceneManager.GetSceneByName(SceneName);
            if (sc.isLoaded) { var op = SceneManager.UnloadSceneAsync(sc); while (op != null && !op.isDone) yield return null; }
            RenderSettings.ambientMode = amode; RenderSettings.ambientLight = aLight; RenderSettings.ambientSkyColor = aSky; RenderSettings.ambientEquatorColor = aEq; RenderSettings.ambientGroundColor = aGround; RenderSettings.fog = fog;
            foreach (var l in lights) if (l != null) l.enabled = true;
            if (dn != null) dn.enabled = true;
            if (gameCam != null) gameCam.enabled = true;
            if (gameListener != null) gameListener.enabled = true;
            if (hud != null) hud.ShowInterface();
            var clock = GameClock.Instance;
            if (prevSpeed > 0 && clock != null) clock.SetSpeedIndex(prevSpeed);
            Active = false;
            var cb = done; done = null; Record = null; Profile = null;
            Destroy(gameObject);
            cb?.Invoke();
        }
    }
}

/// <summary>Lives in the "3. Flight" scene: builds the office and the monitor, and plays the simulated flight on it.</summary>
public class FlightScene : MonoBehaviour
{
    const int TW = 960, TH = 540;
    static readonly Vector3 Origin = new Vector3(0f, 3000f, 0f);        // far above the base, inside a closed room

    FlightRecord rec; FlightProfile prof;
    Texture2D tex; Color32[] bg, buf;
    Camera cam; Transform screen;
    TextMeshPro title, readout, events, banner, hint;
    float t, speedUp, endAt, failAt, shown;
    bool finished, leaving; float openedAt;
    int lastIdx = -1;
    const float Cx = 250f, Cy = -150f, Rpx = 330f;
    float angExag = 1f, theta0;

    void Start()
    {
        rec = FlightSceneLauncher.Record ?? new FlightRecord(); prof = FlightSceneLauncher.Profile;
        if (prof == null || prof.Count == 0) { Leave(); return; }
        openedAt = Time.unscaledTime;
        BuildOffice();
        BuildMonitor();
        Setup();
    }

    // ---------------------------------------------------------------- the office
    static Material Mat(Color c, float emission = 0f)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh); m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        if (emission > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        return m;
    }

    GameObject Box(string name, Vector3 center, Vector3 size, Material m, Transform parent = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name; Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent != null ? parent : transform, false);
        g.transform.position = Origin + center; g.transform.localScale = size;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    Material officeMat, lampMat;

    GameObject Spawn(string model, Vector3 pos, float yaw = 0f)
    {
        var prefab = Resources.Load<GameObject>("Office/" + model);
        if (prefab == null) return null;
        var g = Instantiate(prefab, transform);
        g.name = model; g.transform.position = Origin + pos; g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var r in g.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>();
            int subs = mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 1;
            r.sharedMaterials = subs > 1 && lampMat != null ? new[] { officeMat, lampMat } : new[] { officeMat };
        }
        return g;
    }

    /// <summary>A mission-control room: carpet, two columns of consoles facing the big wall monitor, windows onto a night skyline, a whiteboard, cabinets, plants and a coffee corner.</summary>
    void BuildOffice()
    {
        officeMat = Resources.Load<Material>("Office/OfficeBase"); lampMat = Resources.Load<Material>("Office/OfficeLamp");
        var carpet = Mat(new Color(0.16f, 0.18f, 0.22f)); var walk = Mat(new Color(0.24f, 0.26f, 0.31f)); var wall = Mat(new Color(0.82f, 0.82f, 0.84f));
        var dark = Mat(new Color(0.07f, 0.08f, 0.1f)); var ceil = Mat(new Color(0.9f, 0.9f, 0.92f)); var accent = Mat(new Color(0.12f, 0.3f, 0.55f));
        const float W = 16f, D = 11f, H = 4.4f;
        Box("Floor", new Vector3(0, -0.05f, 0), new Vector3(W, 0.1f, D), carpet);
        Box("Walkway", new Vector3(0, 0.006f, 0), new Vector3(2.2f, 0.01f, D - 0.4f), walk);
        Box("Ceiling", new Vector3(0, H + 0.05f, 0), new Vector3(W, 0.1f, D), ceil);
        Box("Front wall", new Vector3(0, H / 2, -D / 2 - 0.05f), new Vector3(W, H, 0.1f), wall);
        Box("Right wall", new Vector3(W / 2 + 0.05f, H / 2, 0), new Vector3(0.1f, H, D), wall);
        Box("Left wall", new Vector3(-W / 2 - 0.05f, H / 2, 0), new Vector3(0.1f, H, D), wall);
        // the big wall: dark panelling with a blue stripe around the monitor
        Box("Back wall", new Vector3(0, H / 2, D / 2 + 0.05f), new Vector3(W, H, 0.1f), dark);
        Box("Back stripe", new Vector3(0, 0.55f, D / 2 - 0.02f), new Vector3(W, 0.12f, 0.04f), accent);
        Box("Back stripe top", new Vector3(0, 3.95f, D / 2 - 0.02f), new Vector3(W, 0.12f, 0.04f), accent);
        for (int i = -3; i <= 3; i++) if (i != 0) Box("Acoustic panel", new Vector3(i * 2.1f, 2.15f, D / 2 - 0.03f), new Vector3(1.7f, 2.9f, 0.05f), i % 2 == 0 ? Mat(new Color(0.1f, 0.11f, 0.14f)) : Mat(new Color(0.14f, 0.16f, 0.2f)));

        // windows on the left wall (the room is on the +x side of it)
        Spawn("OfficeWindowWall", new Vector3(-W / 2 + 0.1f, 0f, -3.5f), 90f);
        Spawn("OfficeWindowWall", new Vector3(-W / 2 + 0.1f, 0f, 0.5f), 90f);
        Box("Wall piece", new Vector3(-W / 2 + 0.1f, H / 2, 4.0f), new Vector3(0.2f, H, 3f), wall);
        Spawn("OfficePlant", new Vector3(-W / 2 + 0.6f, 0f, 4.4f));
        // right wall: whiteboard, cabinets, coffee corner, plants
        Spawn("OfficeWhiteboard", new Vector3(W / 2 - 0.08f, 0f, 0.6f), -90f);
        for (int i = 0; i < 3; i++) Spawn("OfficeCabinet", new Vector3(W / 2 - 0.3f, 0f, 3.0f + i * 0.95f), -90f);
        Spawn("OfficeCoffee", new Vector3(W / 2 - 0.4f, 0f, -3.3f), -90f);
        Spawn("OfficePlant", new Vector3(W / 2 - 0.6f, 0f, -5.0f));
        Spawn("OfficePlant", new Vector3(W / 2 - 0.6f, 0f, -1.3f));
        // consoles in two columns and three rows, an operator's chair behind each one
        for (int row = 0; row < 3; row++)
            foreach (float cx in new[] { -2.6f, 2.6f })
            {
                float z = 1.5f - row * 1.95f;
                Spawn("OfficeConsole", new Vector3(cx, 0f, z));
                Spawn("OfficeChair", new Vector3(cx + (row == 1 ? 0.15f : -0.1f), 0f, z - 1.0f), (row - 1) * 8f);
            }
        // ceiling lights
        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 2; j++)
                for (int c = 0; c < 2; c++)
                {
                    float x = i * (c == 0 ? 1.9f : 5.4f), z = -3.9f + (j + 1) * 2.6f;
                    Spawn("OfficeLight", new Vector3(x, H - 0.02f, z));
                    if (c == 0)
                    {
                        var lg = new GameObject("Light"); lg.transform.SetParent(transform, false); lg.transform.position = Origin + new Vector3(x, H - 1.6f, z);
                        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 8f; l.intensity = 4.5f; l.color = new Color(1f, 0.94f, 0.82f); l.shadows = LightShadows.None;
                    }
                }
        var wl = new GameObject("Wall wash"); wl.transform.SetParent(transform, false); wl.transform.position = Origin + new Vector3(0f, 3.4f, 3.2f);
        var wlight = wl.AddComponent<Light>(); wlight.type = LightType.Point; wlight.range = 9f; wlight.intensity = 6f; wlight.color = new Color(0.55f, 0.7f, 1f); wlight.shadows = LightShadows.None;
    }

    void BuildMonitor()
    {
        Box("Monitor frame", new Vector3(0, 2.15f, 4.93f), new Vector3(6.5f, 3.75f, 0.12f), Mat(new Color(0.03f, 0.03f, 0.04f)));
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Screen"; Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.position = Origin + new Vector3(0, 2.15f, 4.86f);
        q.transform.localScale = new Vector3(6.2f, 3.49f, 1f);                   // a Unity quad shows its front towards -z: towards the room
        screen = q.transform;
        tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        buf = new Color32[TW * TH]; bg = new Color32[TW * TH];
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        var m = new Material(sh); m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        q.GetComponent<MeshRenderer>().sharedMaterial = m;

        // the text sits just in front of the screen; positions and sizes are in screen pixels (960 x 540), 1 px = 6.2 / 960 m
        textRoot = new GameObject("Screen Text").transform; textRoot.SetParent(transform, false);
        textRoot.position = Origin + new Vector3(0, 2.15f, 4.82f);
        TextMeshPro Text(string n, float x, float y, float size, TextAlignmentOptions al, Color c, float w) => MakeText(n, x, y, size, al, c, w);
        title = Text("Title", 24f, TH - 22f, 28f, TextAlignmentOptions.TopLeft, new Color(0.8f, 0.9f, 1f), 900f);
        readout = Text("Readout", 700f, TH - 70f, 26f, TextAlignmentOptions.TopLeft, new Color(0.75f, 1f, 0.85f), 240f);
        events = Text("Events", 640f, TH - 200f, 18f, TextAlignmentOptions.TopLeft, new Color(0.7f, 0.8f, 0.95f), 310f);
        banner = Text("Banner", TW / 2f, 70f, 36f, TextAlignmentOptions.Center, Color.white, 900f);
        hint = Text("Hint", TW / 2f, 22f, 16f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f), 900f);
        hint.text = "Esc, Space or click to leave";
    }

    Transform textRoot;

    TextMeshPro MakeText(string n, float x, float y, float size, TextAlignmentOptions al, Color c, float w)
    {
        float px = 6.2f / TW;
        var g = new GameObject(n); g.transform.SetParent(textRoot, false);
        g.transform.localPosition = new Vector3((x - TW / 2f) * px, (y - TH / 2f) * px, 0f);          // x, y in screen pixels, y up
        g.transform.localScale = Vector3.one * px * 10f;
        var tm = g.AddComponent<TextMeshPro>(); var f = UIKit.T != null ? UIKit.T.body : null; if (f != null) tm.font = f;
        tm.fontSize = size; tm.alignment = al; tm.color = c; tm.textWrappingMode = TextWrappingModes.NoWrap; tm.richText = true;
        tm.rectTransform.pivot = al == TextAlignmentOptions.Center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);       // left texts start exactly at (x, y)
        tm.rectTransform.sizeDelta = new Vector2(w / 10f, size / 5f);
        return tm;
    }

    // ---- the orbits the monitor draws: name, altitude (km). The radial scale is logarithmic so that low orbits and geostationary orbit both fit.
    static readonly (string name, float km)[] Orbits = { ("KARMAN LINE", 100f), ("LEO", 400f), ("MEO", 20200f), ("GEO", 35786f) };
    const float RingSpan = 250f, MaxRingKm = 40000f;
    static float F(float altKm) => RingSpan * Mathf.Log10(1f + Mathf.Max(0f, altKm) / 100f) / Mathf.Log10(1f + MaxRingKm / 100f);

    static string OrbitName(float perigeeKm, float apogeeKm)
    {
        float a = (perigeeKm + Mathf.Min(apogeeKm, 60000f)) * 0.5f;
        return a < 2000f ? "LOW EARTH ORBIT (LEO)" : a < 30000f ? "MEDIUM EARTH ORBIT (MEO)" : "GEOSTATIONARY RANGE (GEO)";
    }

    void BuildOrbitLabels()
    {
        foreach (var o in Orbits)
        {
            float r = Rpx + F(o.km), a = 97f * Mathf.Deg2Rad;
            float x = Cx + r * Mathf.Cos(a), y = Cy + r * Mathf.Sin(a);
            if (y > TH - 40f) { a = 120f * Mathf.Deg2Rad; x = Cx + r * Mathf.Cos(a); y = Cy + r * Mathf.Sin(a); }
            var tm = MakeText("Orbit " + o.name, x + 6f, y + 6f, 14f, TextAlignmentOptions.TopLeft, new Color(0.62f, 0.82f, 1f, 0.9f), 260f);
            tm.text = "<b>" + o.name + "</b>  " + (o.km >= 1000f ? (o.km / 1000f).ToString("0.#") + "k km" : o.km.ToString("0") + " km");
        }
    }

    void Setup()
    {
        var g = new GameObject("Flight Camera"); g.transform.SetParent(transform, false);
        cam = g.AddComponent<Camera>(); cam.fieldOfView = 52f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.allowHDR = true;
        g.AddComponent<AudioListener>();
        var data = g.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
        // the flight
        float d = prof.duration > 1f ? prof.duration : 12f;
        failAt = rec != null && rec.Result == LaunchResult.Failure ? rec.failAt : 0f;
        endAt = rec != null && rec.Result == LaunchResult.Failure ? (failAt > 0f ? failAt : 8f) : d;
        endAt = Mathf.Min(endAt, d);
        speedUp = Mathf.Max(1f, endAt / 36f);                                  // the whole flight plays in about half a minute
        // the drawing scale: the path is bent so that it fits the screen
        theta0 = Mathf.Atan2(prof.pos[0].y, prof.pos[0].x);
        float maxAng = 0.0001f, maxAlt = 150f;
        for (int i = 0; i < prof.Count; i++) { maxAng = Mathf.Max(maxAng, Mathf.Abs(Ang(i))); maxAlt = Mathf.Max(maxAlt, prof.alt[i]); }
        angExag = Mathf.Min(40f, (55f * Mathf.Deg2Rad) / maxAng);
        BuildOrbitLabels();
        PaintBackground();
        title.text = (string.IsNullOrEmpty(rec.missionName) ? "FLIGHT" : rec.missionName.ToUpperInvariant()) + "  ·  " + (string.IsNullOrEmpty(rec.rocketName) ? "Rocket" : rec.rocketName);
    }

    float Ang(int i) => theta0 - Mathf.Atan2(prof.pos[i].y, prof.pos[i].x);       // angle travelled round the Earth (radians)

    Vector2 ToScreen(int i)
    {
        float a = Mathf.PI / 2f - Ang(i) * angExag;
        float r = Rpx + F(Mathf.Min(prof.alt[i], MaxRingKm));
        return new Vector2(Cx + r * Mathf.Cos(a), Cy + r * Mathf.Sin(a));
    }

    // ---------------------------------------------------------------- drawing
    static readonly Color32 Space = new Color32(6, 9, 20, 255);

    void PaintBackground()
    {
        var rng = new System.Random(7);
        for (int y = 0; y < TH; y++)
            for (int x = 0; x < TW; x++) { float g = y / (float)TH; bg[y * TW + x] = new Color32((byte)(4 + 8 * g), (byte)(7 + 12 * g), (byte)(18 + 22 * g), 255); }
        for (int s = 0; s < 260; s++) { int x = rng.Next(TW), y = rng.Next(TH); byte v = (byte)rng.Next(90, 220); bg[y * TW + x] = new Color32(v, v, (byte)Mathf.Min(255, v + 25), 255); }
        // earth
        for (int y = 0; y < TH; y++)
            for (int x = 0; x < TW; x++)
            {
                float dx = x - Cx, dy = y - Cy, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > Rpx + 14f) continue;
                if (r <= Rpx)
                {
                    float shade = Mathf.Clamp01(0.55f + 0.45f * (dx / Rpx) * -0.4f + 0.25f * ((y - Cy) / Rpx));
                    float land = Mathf.PerlinNoise(x * 0.012f + 3f, y * 0.012f) + 0.4f * Mathf.PerlinNoise(x * 0.04f, y * 0.04f);
                    Color c = land > 0.95f ? new Color(0.22f, 0.5f, 0.28f) : new Color(0.07f, 0.28f, 0.6f);
                    bg[y * TW + x] = (Color32)(c * (0.5f + 0.5f * shade));
                }
                else
                {
                    float a = 1f - (r - Rpx) / 14f; var old = (Color)bg[y * TW + x];
                    bg[y * TW + x] = (Color32)Color.Lerp(old, new Color(0.35f, 0.65f, 1f), a * a * 0.8f);
                }
            }
        // grid and frame
        for (int x = 0; x < TW; x += 80) for (int y = 0; y < TH; y++) Blend(bg, x, y, new Color32(80, 110, 160, 255), 0.07f);
        for (int y = 0; y < TH; y += 80) for (int x = 0; x < TW; x++) Blend(bg, x, y, new Color32(80, 110, 160, 255), 0.07f);
        // the named orbits never change: paint them once into the background (Ring/Disc draw into buf, so point it at bg for a moment)
        var live = buf; buf = bg;
        foreach (var o in Orbits) Ring(Rpx + F(o.km), new Color32(130, 160, 220, 255), o.name == "KARMAN LINE" ? 0.28f : 0.38f, true);
        buf = live;
    }

    static void Blend(Color32[] b, int x, int y, Color32 c, float a)
    {
        if (x < 0 || y < 0 || x >= TW || y >= TH) return;
        var o = b[y * TW + x];
        b[y * TW + x] = new Color32((byte)(o.r + (c.r - o.r) * a), (byte)(o.g + (c.g - o.g) * a), (byte)(o.b + (c.b - o.b) * a), 255);
    }

    void Line(Vector2 a, Vector2 b, float w, Color32 c)
    {
        int n = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
        for (int i = 0; i <= n; i++) { var p = Vector2.Lerp(a, b, i / (float)n); Disc(p.x, p.y, w * 0.5f, c, 1f); }
    }

    void Disc(float cx, float cy, float r, Color32 c, float alpha)
    {
        int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1), y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r) Blend(buf, x, y, c, alpha); else if (d <= r + 1f) Blend(buf, x, y, c, alpha * (r + 1f - d));
            }
    }

    void Ring(float r, Color32 c, float a, bool dotted)
    {
        int n = Mathf.CeilToInt(2f * Mathf.PI * r / 3f);
        for (int i = 0; i < n; i++)
        {
            if (dotted && i % 3 == 2) continue;
            float ang = i / (float)n * 2f * Mathf.PI;
            Disc(Cx + r * Mathf.Cos(ang), Cy + r * Mathf.Sin(ang), 1.2f, c, a);
        }
    }

    void Draw(float time)
    {
        System.Array.Copy(bg, buf, bg.Length);
        int idx = prof.IndexAt(time);
        // the orbit ring once it is reached
        bool orbitNow = prof.orbit && (rec == null || rec.Result != LaunchResult.Failure) && time >= prof.burnoutT;
        if (orbitNow)
        {
            float orbitAlt = Mathf.Clamp((prof.perigeeKm + Mathf.Min(prof.apogeeKm, 60000f)) * 0.5f, 100f, MaxRingKm);
            Ring(Rpx + F(orbitAlt), new Color32(120, 255, 200, 255), 0.95f, true);                   // the orbit the rocket reached, brighter
            Ring(Rpx + F(orbitAlt) + 1.5f, new Color32(120, 255, 200, 255), 0.5f, true);
        }
        // the trail so far
        for (int i = 1; i <= idx; i++)
        {
            float f = i / (float)Mathf.Max(1, idx);
            Line(ToScreen(i - 1), ToScreen(i), 3.2f, Color32.Lerp(new Color32(255, 120, 40, 255), new Color32(255, 240, 160, 255), f));
        }
        var cur = ToScreen(idx);
        bool broke = failAt > 0f && time >= failAt || failAt == 0f && rec != null && rec.Result == LaunchResult.Failure && time > 2f;
        if (broke) { float s = Mathf.Clamp01((time - Mathf.Max(failAt, 2f)) * 0.6f); Disc(cur.x, cur.y, 6f + 22f * s, new Color32(255, 90, 40, 255), 0.55f); Disc(cur.x, cur.y, 3f + 10f * s, new Color32(255, 230, 170, 255), 0.9f); }
        else { Disc(cur.x, cur.y, 6.5f, new Color32(255, 255, 255, 255), 0.35f); Disc(cur.x, cur.y, 3.2f, new Color32(255, 255, 255, 255), 1f); }
        tex.SetPixels32(buf); tex.Apply(false);
        lastIdx = idx;
    }

    // ---------------------------------------------------------------- playback
    void Update()
    {
        if (leaving) return;
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (Time.unscaledTime - openedAt > 0.5f && ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (mouse != null && mouse.leftButton.wasPressedThisFrame))) { Leave(); return; }
        if (prof == null) return;

        // camera: a slow move towards the screen
        float u = Mathf.Clamp01((Time.unscaledTime - openedAt) / 22f);
        cam.transform.position = Origin + new Vector3(Mathf.Sin(Time.unscaledTime * 0.25f) * 0.2f, 1.78f + Mathf.Sin(Time.unscaledTime * 0.31f) * 0.03f, Mathf.Lerp(-4.9f, -1.4f, Mathf.SmoothStep(0f, 1f, u)));
        cam.transform.rotation = Quaternion.LookRotation(Origin + new Vector3(0f, 2.15f, 4.9f) - cam.transform.position);

        if (!finished) { t += Time.unscaledDeltaTime * speedUp; if (t >= endAt) { t = endAt; finished = true; ShowEnd(); } }
        shown = t;
        Draw(Mathf.Min(shown, prof.duration));
        UpdateText();
    }

    void UpdateText()
    {
        int i = prof.IndexAt(Mathf.Min(shown, prof.duration));
        int tt = Mathf.RoundToInt(shown);
        string status = finished ? "" : "";
        readout.text = "<b>T+ " + (tt / 60).ToString("00") + ":" + (tt % 60).ToString("00") + "</b>\nALT  " + prof.alt[i].ToString("0") + " km\nVEL  " + (prof.speed[i] / 1000f).ToString("0.00") + " km/s" + status;
        var sb = new System.Text.StringBuilder();
        for (int e = prof.events.Count - 1, n = 0; e >= 0 && n < 5; e--) if (prof.events[e].t <= shown) { sb.Insert(0, "T+" + Mathf.RoundToInt(prof.events[e].t) + "s  " + prof.events[e].label + "\n"); n++; }
        events.text = sb.ToString();
    }

    void ShowEnd()
    {
        if (rec == null) return;
        string cause = string.IsNullOrEmpty(rec.cause) ? "" : "\n" + rec.cause;
        switch (rec.Result)
        {
            case LaunchResult.Success: banner.text = "<color=#7CFFB0>" + (prof.escape ? "ESCAPE TRAJECTORY" : OrbitName(prof.perigeeKm, prof.apogeeKm)) + "</color>"; break;
            case LaunchResult.Partial: banner.text = "<color=#FFC46B>" + (prof.orbit ? OrbitName(prof.perigeeKm, prof.apogeeKm) + ", WITH PROBLEMS" : "SUBORBITAL FLIGHT") + "</color>"; break;
            default: banner.text = "<color=#FF7A6B>VEHICLE LOST</color>"; break;
        }
    }

    void Leave()
    {
        if (leaving) return;
        leaving = true;
        FlightSceneLauncher.Close();
    }
}
