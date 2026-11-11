using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the space centre in the Game scene from the site sketch: a river in the south, a highway above it, the fenced base
/// north of the highway (rocket pad inside a road loop, power grid, office, two car parks, five empty plots P1-P5, staff housing),
/// mountains in the background. Terrain, roads (procedural meshes), Blender models and the Holotna nature pack (with LODs) are used.
/// Sketch scale: 1 sketch pixel = 0.45 m. Safe to run again. Menu: Tools / Space Agency / Rebuild Level.
/// </summary>
public static class LevelSetup
{
    const string Site = "Assets/Assets/Models/Site/";
    const string RoadDir = "Assets/Assets/Models/Site/Roads/";
    const string GfxDir = "Assets/Assets/Graphics/Site/";
    const string BaseMat = "Assets/Assets/Models/Base/Base.mat";
    const string LampOn = "Assets/Assets/Models/Base/Lamp_On.mat", LampOff = "Assets/Assets/Models/Base/Lamp_Off.mat";
    const string Nature = "Assets/Holotna/Mountain/Prefabs/";
    const float P = 6.0f;                                   // ground level of the base (metres)
    const float WaterY = 3.5f;
    const float RocketScale = 2.8f;                         // parts are modelled for the builder; on the pad they are shown ~3x for a 70 m rocket

    // ---- sketch layout, world metres (x east, z north; the river is in the south) ----
    static readonly Vector3 PadCenter = new Vector3(-187f, P, 161f);
    static readonly Vector2[] PlotPos = { new Vector2(-22, 231), new Vector2(92, 236), new Vector2(199, 233), new Vector2(85, 121), new Vector2(331, 221) };
    static readonly float[] PlotYaw = { 180, 180, 180, 180, 270 };           // all face a road; models face +Z at yaw 0
    static readonly Vector2 GateWest = new Vector2(-390, 32), GateMain = new Vector2(-94.5f, 32);
    const float FenceZ = 32f;

    static Material baseMat, lampOn, lampOff, hwMat, svMat, gravelMat, pathMat, plainMat, zebraMat;
    static Dictionary<string, Vector3> dims;
    static Terrain terrain;

    [MenuItem("Tools/Space Agency/Rebuild Level")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode before rebuilding the level."); return; }
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();                                // pick up freshly exported Blender models and textures
        EditorSceneManager.OpenScene("Assets/Scenes/2. Game.unity", OpenSceneMode.Single);
        FixImports();
        baseMat = AssetDatabase.LoadAssetAtPath<Material>(BaseMat);
        lampOn = AssetDatabase.LoadAssetAtPath<Material>(LampOn);
        lampOff = AssetDatabase.LoadAssetAtPath<Material>(LampOff);
        LoadDims();
        MakeRoadMaterials();

        BuildTerrain();
        ClearOld();
        var site = new GameObject("Site").transform;
        var buildings = new GameObject("Base Buildings").transform;
        var plots = new GameObject("Base Plots").transform;

        PlanCarParkA();
        BuildRoads(site);
        BuildFixedBuildings(buildings);
        BuildPlots(plots);
        BuildFenceAndGates(site);
        BuildLamps(site);
        BuildRoadEnds(site);
        BuildPadLights(site);
        BuildNature(site);
        BuildCatalog();
        SetupRocket();
        SetupWater();
        SetupTraffic();
        SetupCamera();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Level rebuilt.");
    }

    // ================================================================ helpers
    static void FixImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Site.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is ModelImporter mi && mi.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;      // the scene assigns the shared palette material
                mi.SaveAndReimport();
            }
        }
    }

    static void LoadDims()
    {
        dims = new Dictionary<string, Vector3>();
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Site + "site-manifest.json");
        if (ta == null) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ta.text,
            "\"(?<n>[A-Za-z0-9_]+)\":\\s*\\{\\s*\"w\":\\s*(?<w>[-0-9.]+),\\s*\"d\":\\s*(?<d>[-0-9.]+),\\s*\"h\":\\s*(?<h>[-0-9.]+)"))
            dims[m.Groups["n"].Value] = new Vector3(float.Parse(m.Groups["w"].Value, inv), float.Parse(m.Groups["h"].Value, inv), float.Parse(m.Groups["d"].Value, inv));
    }

    static GameObject Place(string model, Transform parent, Vector3 pos, float yaw, string name = null, float scale = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Site + model + ".fbx");
        if (prefab == null) { Debug.LogWarning("LevelSetup: missing model " + model); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name ?? model;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>();
            int subs = mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 1;
            r.sharedMaterials = subs > 1 ? new[] { baseMat, lampOff } : new[] { baseMat };
        }
        return go;
    }

    static void ClearOld()
    {
        foreach (var n in new[] { "Site", "Base Layout", "Lights", "Base Buildings", "Base Plots" })
        {
            var g = GameObject.Find(n);
            if (g != null) Object.DestroyImmediate(g);
        }
    }

    // ================================================================ terrain: base plateau, river in the south, foothills and mountains in the north
    static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static float Fbm(float x, float z, int oct = 5)
    {
        float s = 0f, a = 0.5f, f = 1f;
        for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + 31.7f, z * f + 12.3f); a *= 0.5f; f *= 2f; }
        return s;
    }

    static float Ridged(float x, float z)
    {
        float s = 0f, a = 0.55f, f = 1f;
        for (int i = 0; i < 5; i++) { float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * f + 7.1f, z * f + 3.9f) * 2f - 1f); s += a * n * n; a *= 0.5f; f *= 2.1f; }
        return Mathf.Clamp01(s);
    }

    const float WestEnd = -1296f;                           // the highway ends in a tunnel portal here
    static Vector2[] farRoad;                               // smoothed highway centre line, used to carve the valley around it

    // river (south) and the northern foothills of the base valley
    static float ValleyHeight(float wx, float wz)
    {
        if (wz < -28f)
        {
            float h;
            if (wz >= -48f) h = Mathf.Lerp(P, 2.2f, Smooth01((-28f - wz) / 20f));
            else if (wz >= -78f) h = Mathf.Lerp(2.2f, 0.3f, Smooth01((-48f - wz) / 30f));
            else if (wz >= -155f) h = 0.3f + 0.4f * Fbm(wx * 0.02f, wz * 0.02f, 3);
            else if (wz >= -205f) h = Mathf.Lerp(0.3f, 8f, Smooth01((-155f - wz) / 50f));
            else h = 8f + Smooth01((-205f - wz) / 55f) * (12f + 34f * Fbm(wx * 0.006f, wz * 0.006f));
            return h;
        }
        float tn = Smooth01((wz - 330f) / 340f);
        float ridge = Ridged(wx * 0.0035f, wz * 0.0035f);
        return P + tn * (30f + 130f * ridge * (0.4f + 0.6f * tn));
    }

    static float RoadDistance(float wx, float wz)
    {
        if (farRoad == null)
        {
            var hw = Roads[0];
            farRoad = Smooth(hw.pts, hw.radius, 10f).ToArray();
        }
        float best = float.MaxValue; var p = new Vector2(wx, wz);
        for (int i = 0; i < farRoad.Length - 1; i++)
        {
            Vector2 a = farRoad[i], ab = farRoad[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            float d = (p - (a + ab * t)).sqrMagnitude;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    static float Bump(float wx, float wz, float cx, float cz, float radius, float height)
    {
        float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(cx, cz)) / radius;
        return height * Mathf.Exp(-d * d);
    }

    static float HeightAt(float wx, float wz)
    {
        float v = ValleyHeight(wx, wz);
        float ax = Mathf.Abs(wx);
        float w = Smooth01((ax - 430f) / 120f);                       // 0 inside the base valley, 1 out on the long highway
        if (w <= 0f) return v;

        // mountains close in from both sides; the river runs on into a gorge
        float g = Smooth01((ax - 470f) / 330f);
        float lift = g * (30f + 100f * Ridged(wx * 0.004f, wz * 0.004f));
        float river = Smooth01((-20f - wz) / 30f) * (1f - Smooth01((wz + 215f) / 30f));
        lift *= 1f - 0.85f * river * (1f - Smooth01((ax - 850f) / 250f));
        float target = v + lift;
        target += Bump(wx, wz, 985f, 270f, 150f, 190f);               // the mountain the road bends around (east)
        target += Bump(wx, wz, 1300f, 130f, 130f, 150f);              // the far wall of the bend
        target += Bump(wx, wz, 1010f, 610f, 170f, 200f);              // the peak that hides the road's end

        float c = Smooth01((RoadDistance(wx, wz) - 30f) / 190f);        // flat road corridor, rock rising on both sides
        float h = Mathf.Lerp(P, target, c);
        h = Mathf.Lerp(v, h, w);

        // west end: a rock cut. The ground rises like a wall right behind the tunnel portal
        if (wx < WestEnd)
        {
            float wall = Smooth01((WestEnd - wx) / 30f);
            h += wall * (150f + 40f * Fbm(wx * 0.01f, wz * 0.01f, 4)) * Mathf.Lerp(1f, 0.0f, 0f);
        }
        return h;
    }

    static void BuildTerrain()
    {
        terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        var td = terrain.terrainData;
        td.heightmapResolution = 2049;
        td.size = new Vector3(3000f, 360f, 960f);
        terrain.transform.position = new Vector3(-1500f, 0f, -260f);
        int n = td.heightmapResolution;
        var hs = new float[n, n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
                hs[z, x] = HeightAt(-1500f + 3000f * x / (n - 1), -260f + 960f * z / (n - 1)) / 360f;
        td.SetHeights(0, 0, hs);
        UseNatureGround(terrain, td);

        td.alphamapResolution = 1024;
        int a = td.alphamapResolution;
        int layers = td.terrainLayers.Length;
        var map = new float[a, a, layers];
        for (int z = 0; z < a; z++)
            for (int x = 0; x < a; x++)
            {
                float u = (float)x / (a - 1), v = (float)z / (a - 1);
                float h = td.GetInterpolatedHeight(u, v);
                float slope = td.GetSteepness(u, v);
                float sand = Mathf.Clamp01((4.6f - h) / 1.3f);
                float rock = Mathf.Clamp01(Mathf.Max((slope - 30f) / 14f, (h - 85f) / 30f)) * (1f - sand);
                float grass = Mathf.Clamp01(1f - sand - rock);
                if (layers > 0) map[z, x, 0] = grass;
                if (layers > 1) map[z, x, 1] = sand;
                if (layers > 2) map[z, x, 2] = rock;
            }
        td.SetAlphamaps(0, 0, map);
        EditorUtility.SetDirty(td);
    }

    // grass, rock and dirt come from the nature pack (Ground01-03); sand is a generated texture in the same style
    static void UseNatureGround(Terrain terrain, TerrainData td)
    {
        const string misc = "Assets/Holotna/Mountain/Misc/";
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(misc + "Ground01.terrainlayer");
        var rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>(misc + "Ground02.terrainlayer");
        var dirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(misc + "Ground03.terrainlayer");
        if (grass == null || rock == null || dirt == null) { Debug.LogWarning("LevelSetup: the pack's Ground01-03 terrain layers were not found."); return; }

        string sandTex = GfxDir + "ground-sand.png", sandPath = GfxDir + "Layer-sand.terrainlayer";
        var sand = AssetDatabase.LoadAssetAtPath<TerrainLayer>(sandPath);
        if (sand == null) { sand = new TerrainLayer(); AssetDatabase.CreateAsset(sand, sandPath); }
        sand.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sandTex);
        sand.tileSize = new Vector2(5f, 5f);
        EditorUtility.SetDirty(sand);

        td.terrainLayers = new[] { grass, sand, rock, dirt };                      // order = splat channels: grass, sand, rock, path
        var mat = terrain.materialTemplate;
        if (mat != null)
        {
            mat.SetColor("_Color0", new Color(0.85f, 1.0f, 0.62f)); mat.SetColor("_Color1", Color.white);      // grass is tinted towards olive, the rest keeps its own colours
            mat.SetFloat("_Desat", 0.55f);                                                                      // the pack's ground is very saturated: calm it to match the trees
            mat.SetColor("_Color2", Color.white); mat.SetColor("_Color3", Color.white);
            mat.SetVector("_Tile", new Vector4(grass.tileSize.x, sand.tileSize.x, rock.tileSize.x, dirt.tileSize.x));
            mat.SetFloat("_TexBoost", 1.9f);
            EditorUtility.SetDirty(mat);
        }
    }

    static float GroundY(float x, float z) => terrain != null ? terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y : P;

    static void SetupWater()
    {
        var sea = GameObject.Find("Sea");
        if (sea == null) return;
        sea.transform.position = new Vector3(0f, WaterY, 0f);
        sea.transform.localScale = new Vector3(330f, 1f, 120f);
        sea.layer = 4;                                                                // "Water": left out of its own reflection
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (rp != null) { rp.supportsCameraDepthTexture = true; EditorUtility.SetDirty(rp); }       // the water reads the scene depth for its shore foam
        var r = sea.GetComponent<Renderer>();
        var shader = Shader.Find("SpaceAgency/Toon/Water");
        if (r != null && r.sharedMaterial != null && shader != null)
        {
            var m = r.sharedMaterial;
            m.shader = shader;
            m.SetColor("_Shallow", new Color(0.30f, 0.82f, 0.82f)); m.SetColor("_Deep", new Color(0.05f, 0.34f, 0.66f));
            m.renderQueue = -1;                                                    // the shader's own (opaque) queue
            EditorUtility.SetDirty(m);
        }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (sea.GetComponent<WaterReflection>() == null) sea.AddComponent<WaterReflection>();
        if (sea.GetComponent<WaterDepthMap>() == null) sea.AddComponent<WaterDepthMap>();
    }

    // ================================================================ roads (procedural meshes with painted textures)
    sealed class RoadDef { public string name; public Vector2[] pts; public float radius; public bool highway, gravel; }

    static RoadDef[] Roads =
    {
        new RoadDef { name = "Highway", highway = true, radius = 150f, pts = new[] { new Vector2(WestEnd, 0), new Vector2(-396, 0), new Vector2(-94.5f, 0), new Vector2(900, 0), new Vector2(1130, 20), new Vector2(1205, 200), new Vector2(1140, 420), new Vector2(960, 570) } },
        new RoadDef { name = "Loop", radius = 16, pts = new[] { new Vector2(-94.5f, 11.4f), new Vector2(-94.5f, 238), new Vector2(-279, 238), new Vector2(-279, 85), new Vector2(-94.5f, 85) } },
        new RoadDef { name = "TopWest", radius = 6, pts = new[] { new Vector2(-279, 238), new Vector2(-380, 234), new Vector2(-470, 226) } },
        new RoadDef { name = "East", radius = 16, pts = new[] { new Vector2(-94.5f, 173), new Vector2(270, 173), new Vector2(270, 272) } },
        new RoadDef { name = "Mid", radius = 14, pts = new[] { new Vector2(29, 284), new Vector2(29, 79), new Vector2(196, 79) } },
        new RoadDef { name = "GateWest", radius = 30, pts = new[] { new Vector2(-396, 11.4f), new Vector2(-396, 20), new Vector2(-390, 32), new Vector2(-372, 54), new Vector2(-330, 71), new Vector2(-279, 85) } },
        new RoadDef { name = "Spur Office", pts = new[] { new Vector2(-94.5f, 118), new Vector2(-44, 118) } },
        new RoadDef { name = "Spur Pad", pts = new[] { new Vector2(-187, 85), new Vector2(-187, 98) } },
        new RoadDef { name = "Spur Power", pts = new[] { new Vector2(-375, 238), new Vector2(-375, 185) } },
        new RoadDef { name = "Spur P1", pts = new[] { new Vector2(-22, 173), new Vector2(-22, 197) } },
        new RoadDef { name = "Spur P2", pts = new[] { new Vector2(92, 173), new Vector2(92, 202) } },
        new RoadDef { name = "Spur P3", pts = new[] { new Vector2(199, 173), new Vector2(199, 199) } },
        new RoadDef { name = "Spur P5", pts = new[] { new Vector2(270, 221), new Vector2(298, 221) } },
        new RoadDef { name = "Spur CarPark", pts = new[] { new Vector2(-94.5f, 62), new Vector2(-82, 62) } },
        new RoadDef { name = "Spur P4", pts = new[] { new Vector2(85, 79), new Vector2(85, 87) } },
    };

    static void MakeRoadMaterials()
    {
        hwMat = RoadMaterial("Road_Highway", GfxDir + "road-highway.png");
        svMat = RoadMaterial("Road_Service", GfxDir + "road-service.png");
        gravelMat = RoadMaterial("Road_Gravel", GfxDir + "road-gravel.png");
        pathMat = RoadMaterial("Path_Pave", GfxDir + "path-pave.png");
        plainMat = RoadMaterial("Road_Plain", GfxDir + "road-plain.png");
        zebraMat = RoadMaterial("Road_Zebra", GfxDir + "road-zebra.png");
    }

    static Material RoadMaterial(string name, string texPath)
    {
        if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8; ti.wrapMode = TextureWrapMode.Repeat;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        string path = GfxDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, path); }
        m.shader = baseMat.shader;
        m.CopyPropertiesFromMaterial(baseMat);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetColor("_BaseColor", Color.white);
        // road UVs run over the whole texture, so they would hit the palette's "window" cell and glow at night: switch the window glow off
        var lv = m.GetVector("_ToonLevels"); m.SetVector("_ToonLevels", new Vector4(lv.x, lv.y, 1f, lv.w));
        EditorUtility.SetDirty(m);
        return m;
    }

    // rounds the sharp corners of a polyline with quadratic curves, then resamples it evenly
    static List<Vector2> Smooth(Vector2[] pts, float radius, float step)
    {
        var outp = new List<Vector2> { pts[0] };
        for (int i = 1; i < pts.Length - 1; i++)
        {
            Vector2 a = pts[i - 1], b = pts[i], c = pts[i + 1];
            float r = Mathf.Min(radius, 0.45f * Vector2.Distance(a, b), 0.45f * Vector2.Distance(b, c));
            if (radius <= 0.01f || r < 0.5f) { outp.Add(b); continue; }
            Vector2 p0 = b + (a - b).normalized * r, p2 = b + (c - b).normalized * r;
            for (int s = 0; s <= 10; s++)
            {
                float t = s / 10f;
                outp.Add((1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * b + t * t * p2);
            }
        }
        outp.Add(pts[pts.Length - 1]);
        var res = new List<Vector2> { outp[0] };
        for (int i = 1; i < outp.Count; i++)
        {
            float d = Vector2.Distance(outp[i - 1], outp[i]);
            int n = Mathf.Max(1, Mathf.CeilToInt(d / step));
            for (int k = 1; k <= n; k++) res.Add(Vector2.Lerp(outp[i - 1], outp[i], k / (float)n));
        }
        return res;
    }

    static Mesh RoadMesh(string name, List<Vector2> p, float width, float y, float tile)
    {
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        float dist = 0f;
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 prev = p[Mathf.Max(0, i - 1)], next = p[Mathf.Min(p.Count - 1, i + 1)];
            Vector2 t = (next - prev).normalized;
            if (i == 0 && Mathf.Abs(p[0].y - HwEdge) < 0.01f) t = Vector2.up;                 // flush, square end on the highway lane edge
            Vector2 nrm = new Vector2(-t.y, t.x);
            if (i > 0) dist += Vector2.Distance(p[i - 1], p[i]);
            Vector2 l = p[i] + nrm * width / 2f, r = p[i] - nrm * width / 2f;
            verts.Add(new Vector3(l.x, y, l.y)); verts.Add(new Vector3(r.x, y, r.y));
            uvs.Add(new Vector2(0f, dist / tile)); uvs.Add(new Vector2(1f, dist / tile));
        }
        for (int i = 0; i < p.Count - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            AddTri(verts, tris, a, c, b); AddTri(verts, tris, b, c, d);
        }
        var m = new Mesh { name = name, indexFormat = verts.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static void AddTri(List<Vector3> v, List<int> t, int a, int b, int c)
    {
        // Unity front faces are clockwise: the normal must point up
        if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y < 0f) { t.Add(a); t.Add(c); t.Add(b); } else { t.Add(a); t.Add(b); t.Add(c); }
    }

    const float HwEdge = 11.4f;                                                          // half the highway width
    static Vector2 carAPos; static float carAYaw;

    // car park A sits beside the west gate road with a clear gap and its own entrance spur (a road is added for it)
    static void PlanCarParkA()
    {
        var gw = System.Array.Find(Roads, r => r.name == "GateWest");
        var path = Smooth(gw.pts, gw.radius, 6f);
        int bi = 1; float best = float.MaxValue;
        for (int i = 1; i < path.Count - 1; i++) { float d = Vector2.Distance(path[i], new Vector2(-356f, 62f)); if (d < best) { best = d; bi = i; } }
        Vector2 p0 = path[bi], t = (path[bi + 1] - path[bi - 1]).normalized, n = new Vector2(-t.y, t.x);
        const float gap = 11f, hd = 21.6f + 0.6f;                                      // clear ground between the road and the lot, lot half depth with kerb
        carAPos = p0 + n * (5.7f + gap + hd);
        carAYaw = -Mathf.Atan2(t.y, t.x) * Mathf.Rad2Deg;
        var list = new List<RoadDef>(Roads.Where(r => r.name != "Spur CarParkA"));
        list.Add(new RoadDef { name = "Spur CarParkA", pts = new[] { p0, p0 + n * (5.7f + gap + 0.4f) } });
        Roads = list.ToArray();
    }

    static readonly Dictionary<string, List<Vector2>> roadPaths = new Dictionary<string, List<Vector2>>();

    static void BuildRoads(Transform site)
    {
        var root = new GameObject("Roads").transform; root.SetParent(site, false);
        if (!AssetDatabase.IsValidFolder(RoadDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Assets/Models/Site", "Roads");
        roadPaths.Clear();
        foreach (var r in Roads)
        {
            var path = Smooth(r.pts, r.radius, 6f);
            roadPaths[r.name] = path;
            var mesh = RoadMesh(r.name.Replace(' ', '_'), path, r.highway ? 22.8f : r.gravel ? 8f : 11.4f, P + (r.highway ? 0.10f : 0.22f), r.gravel ? 16f : 32f);
            var assetPath = RoadDir + r.name.Replace(' ', '_') + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (old != null) { EditorUtility.CopySerialized(mesh, old); mesh = old; }
            else AssetDatabase.CreateAsset(mesh, assetPath);
            var go = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = r.highway ? hwMat : r.gravel ? gravelMat : svMat;
            go.isStatic = true;
        }
        BuildFootpathsAndLamps(root, site);
        BuildJunctions(root);
        BuildHousingWalks(root);
        AssetDatabase.SaveAssets();
    }

    // footpath beside the concrete site roads, and a lamp post every ~45 m on the outer edge of the path
    static readonly string[] PathRoads = { "Loop", "TopWest", "East", "Mid", "GateWest" };

    static bool InBuildingZone(Vector2 q, float shrink)
    {
        foreach (var k in Keepout) if (Mathf.Abs(q.x - k.x) < k.z - shrink && Mathf.Abs(q.y - k.y) < k.w - shrink) return true;
        return false;
    }

    static float DistToOtherRoads(string self, Vector2 q)
    {
        float best = 1e9f;
        foreach (var kv in roadPaths)
        {
            if (kv.Key == self) continue;
            var p = kv.Value;
            float hw = kv.Key == "Highway" ? 11.4f : kv.Key == "HousingTrack" ? 4f : 5.7f;
            for (int i = 0; i < p.Count; i++) best = Mathf.Min(best, Vector2.Distance(q, p[i]) - hw);
        }
        return best;
    }

    static void BuildFootpathsAndLamps(Transform roadsRoot, Transform site)
    {
        var lampRoot = new GameObject("Road Lamps").transform; lampRoot.SetParent(site, false);
        int lampCount = 0;
        void Lamp(Vector2 pos, Vector2 towardRoad)
        {
            float yaw = Mathf.Atan2(towardRoad.x, towardRoad.y) * Mathf.Rad2Deg;
            var go = Place("RoadLamp", lampRoot, new Vector3(pos.x, GroundY(pos.x, pos.y) + 0.02f, pos.y), yaw, "RoadLamp");
            if (go == null) return;
            lampCount++;
            GlowAtNight(go, new[] { new Vector3(0f, 7.7f, 2.3f) }, 18f, 26f);
        }
        foreach (var name in PathRoads)
        {
            if (!roadPaths.TryGetValue(name, out var path) || path.Count < 3) continue;
            var normals = new Vector2[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 t = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                normals[i] = new Vector2(-t.y, t.x);
            }
            // pick the side that has less building ground on it
            int bad0 = 0, bad1 = 0;
            for (int i = 0; i < path.Count; i++) { if (InBuildingZone(path[i] + normals[i] * 7.4f, 6f)) bad0++; if (InBuildingZone(path[i] - normals[i] * 7.4f, 6f)) bad1++; }
            float side = bad0 <= bad1 ? 1f : -1f;

            var run = new List<Vector2>(); int runIdx = 0;
            void Flush()
            {
                run = new List<Vector2>();
            }
            float sinceLamp = 30f;
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 q = path[i] + normals[i] * side * 7.4f;
                bool ok = !InBuildingZone(q, 6f) && DistToOtherRoads(name, q) > 3.2f && Mathf.Abs(q.y) > 17f;
                if (i > 0) sinceLamp += Vector2.Distance(path[i - 1], path[i]);
                if (!ok) { Flush(); continue; }
                run.Add(q);
                if (sinceLamp >= 45f)
                {
                    Vector2 lp = path[i] + normals[i] * side * 7.0f;
                    if (DistToOtherRoads(name, lp) > 4f && !InBuildingZone(lp, 6f)) { Lamp(lp, -normals[i] * side); sinceLamp = 0f; }
                }
            }
            Flush();
        }
        // gravel track to the housing: two lamps
        if (roadPaths.TryGetValue("HousingTrack", out var gp) && gp.Count > 4)
            foreach (float f in new[] { 0.35f, 0.8f })
            {
                int i = Mathf.Clamp((int)(gp.Count * f), 1, gp.Count - 2);
                Vector2 t = (gp[i + 1] - gp[i - 1]).normalized, n = new Vector2(-t.y, t.x);
                Lamp(gp[i] + n * (f < 0.5f ? 5.8f : -5.8f), n * (f < 0.5f ? -1f : 1f));
            }
    }

    // gravel walkways inside the staff housing (model at (317,110): walks at z=86 and z=128, and three cross walks)
    static void BuildHousingWalks(Transform root)
    {
        Vector2 c = new Vector2(317f, 110f);
        var runs = new List<Vector2[]>
        {
            new[] { c + new Vector2(-89f, -24f), c + new Vector2(89f, -24f) },
            new[] { c + new Vector2(-89f, 18f), c + new Vector2(89f, 18f) },
            new[] { c + new Vector2(-72f, -24f), c + new Vector2(-72f, 18f) },
            new[] { c + new Vector2(0f, -24f), c + new Vector2(0f, 18f) },
            new[] { c + new Vector2(72f, -24f), c + new Vector2(72f, 18f) },
        };
        int n = 0;
        foreach (var r in runs)
        {
            var pts = new List<Vector2>();
            float len = Vector2.Distance(r[0], r[1]);
            for (int i = 0; i <= (int)(len / 4f); i++) pts.Add(Vector2.Lerp(r[0], r[1], i * 4f / len));
            pts.Add(r[1]);
            var go = new GameObject("Housing Walk " + n++, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = RoadMesh("HousingWalk" + n, pts, 3.2f, P + 0.24f, 8f);
            go.GetComponent<MeshRenderer>().sharedMaterial = gravelMat; go.isStatic = true;
        }
    }

    // road crossings: one blank concrete patch where roads meet (hides the overlapping geometry and markings) and a zebra crossing on every arm
    static bool OnRoad(Vector2 q, float tol, string except = null)
    {
        foreach (var kv in roadPaths)
        {
            if (kv.Key == "Highway" || kv.Key == except || kv.Key == "HousingTrack") continue;
            var p = kv.Value;
            for (int i = 0; i < p.Count; i++) if ((p[i] - q).sqrMagnitude < tol * tol) return true;
        }
        return false;
    }

    static void BuildJunctions(Transform root)
    {
        var found = new List<(Vector2 c, Vector2 t)>();
        void Add(Vector2 c, Vector2 t) { foreach (var f in found) if (Vector2.Distance(f.c, c) < 9f) return; found.Add((c, t.normalized)); }
        var names = roadPaths.Keys.Where(k => k != "Highway" && k != "HousingTrack").ToList();
        // road ends that land on another road
        foreach (var a in names)
            foreach (var e in new[] { roadPaths[a][0], roadPaths[a][roadPaths[a].Count - 1] })
                foreach (var b in names)
                {
                    if (a == b) continue;
                    var pb = roadPaths[b]; int bi = -1; float bd = 6f;
                    for (int i = 0; i < pb.Count; i++) { float d = Vector2.Distance(pb[i], e); if (d < bd) { bd = d; bi = i; } }
                    if (bi < 0) continue;
                    Add(e, pb[Mathf.Min(pb.Count - 1, bi + 1)] - pb[Mathf.Max(0, bi - 1)]);
                }
        // roads that cross
        for (int ai = 0; ai < names.Count; ai++)
            for (int bi2 = ai + 1; bi2 < names.Count; bi2++)
            {
                var pa = roadPaths[names[ai]]; var pb = roadPaths[names[bi2]];
                for (int i = 0; i < pa.Count - 1; i++)
                    for (int j = 0; j < pb.Count - 1; j++)
                    {
                        Vector2 r = pa[i + 1] - pa[i], q = pb[j + 1] - pb[j];
                        float den = r.x * q.y - r.y * q.x;
                        if (Mathf.Abs(den) < 1e-4f) continue;
                        Vector2 d = pb[j] - pa[i];
                        float u = (d.x * q.y - d.y * q.x) / den, v = (d.x * r.y - d.y * r.x) / den;
                        if (u >= 0f && u <= 1f && v >= 0f && v <= 1f) Add(pa[i] + r * u, r);
                    }
            }

        const float half = 5.7f;
        int n = 0;
        foreach (var (c, t) in found)
        {
            Vector2 nrm = new Vector2(-t.y, t.x);
            Mesh Quad(string nm, Vector2 centre, Vector2 dir, float length, float width, float y, float tile)
                => RoadMesh(nm, new List<Vector2> { centre - dir * length / 2f, centre + dir * length / 2f }, width, y, tile);
            void Make(string nm, Mesh m, Material mat)
            {
                var go = new GameObject(nm, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = m; go.GetComponent<MeshRenderer>().sharedMaterial = mat; go.isStatic = true;
            }
            Make("Junction " + n, Quad("Junction" + n, c, t, half * 2f, half * 2f, P + 0.26f, 32f), plainMat);
            foreach (var dir in new[] { t, -t, nrm, -nrm })
            {
                if (!OnRoad(c + dir * 14f, 4.5f)) continue;
                Vector2 zc = c + dir * (half + 1.9f);
                bool clash = false;
                foreach (var f in found) if (f.c != c && Vector2.Distance(f.c, zc) < half + 2.5f) clash = true;
                if (clash) continue;
                Make("Zebra " + n, Quad("Zebra" + n, zc, dir, 3.2f, half * 2f, P + 0.28f, 3.2f), zebraMat);
            }
            n++;
        }
        Debug.Log("LevelSetup: " + found.Count + " road junctions");
    }

    // ================================================================ buildings
    static void Levels(Transform root, string modelPrefix, int count, float scale = 1f)
    {
        for (int l = 1; l <= count; l++)
        {
            var go = Place(count == 1 ? modelPrefix : modelPrefix + "_L" + l, root, root.position, root.eulerAngles.y, "L" + l, scale);
            if (go != null) go.SetActive(l == 1);              // everything pre-built starts at level 1 and is already standing
        }
    }

    static Transform Fixed(Transform parent, string id, Vector2 xz, float yaw)
    {
        var t = new GameObject(id).transform;
        t.SetParent(parent, false);
        t.position = new Vector3(xz.x, P, xz.y);
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    static void BuildFixedBuildings(Transform parent)
    {
        Levels(Fixed(parent, "LaunchPad", new Vector2(PadCenter.x, PadCenter.z), 0f), "LaunchComplex", 1);
        Levels(Fixed(parent, "AdminOffice", new Vector2(-32, 118), 180f), "AdminOffice", 3);
        var pg = Fixed(parent, "PowerGrid", new Vector2(-375, 160), 0f);
        for (int l = 1; l <= 3; l++)
        {
            // the full station (with its pylons and wires) is scaled to fit between the top road and the car park
            float s = dims.TryGetValue("PowerGrid_L" + l, out var d) ? Mathf.Min(1f, 140f / d.x, 94f / d.z) : 1f;
            var go = Place("PowerGrid_L" + l, pg, pg.position, 0f, "L" + l, s);
            if (go != null) go.SetActive(l == 1);
        }
        var carA = Fixed(parent, "CarParkA", carAPos, carAYaw);
        var carB = Fixed(parent, "CarParkB", new Vector2(-50, 62), 0f);
        Levels(carA, "CarPark", 1);
        Levels(carB, "CarPark", 1);
        foreach (var car in new[] { carA, carB }) ParkingLamps(car);
        var padRoot = parent.Find("LaunchPad");
        if (padRoot != null && padRoot.Find("L1") != null) GlowAtNight(padRoot.Find("L1").gameObject, new Vector3[0], 0f, 0f);
        Levels(Fixed(parent, "StaffHousing", new Vector2(317, 110), 0f), "StaffHousing", 1);
    }

    // lamp glass that switches on at night, with real lights where given (local positions in the model)
    static void GlowAtNight(GameObject go, Vector3[] localLights, float intensity, float range)
    {
        var nl = go.AddComponent<NightLamp>();
        nl.lampRenderer = go.GetComponentInChildren<MeshRenderer>();
        nl.lampSlot = 1; nl.onMaterial = lampOn; nl.offMaterial = lampOff;
        var lights = new List<Light>();
        foreach (var lp in localLights)
        {
            var g = new GameObject("Lot Light");
            g.transform.SetParent(go.transform, false);
            g.transform.position = go.transform.TransformPoint(lp);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.color = new Color(1f, 0.82f, 0.55f); l.intensity = 0f; l.shadows = LightShadows.None; l.enabled = false;
            lights.Add(l);
        }
        nl.lights = lights.ToArray(); nl.onIntensity = intensity;
    }

    // the car park model has light poles at x = -27, -9, 9, 27 down the middle aisle (two heads each)
    static void ParkingLamps(Transform park)
    {
        var l1 = park.Find("L1");
        if (l1 == null) return;
        var pts = new List<Vector3>();
        foreach (float x in new[] { -27f, -9f, 9f, 27f })
            foreach (float s in new[] { -1f, 1f }) pts.Add(new Vector3(x, 8.2f, s * 2.2f));
        GlowAtNight(l1.gameObject, pts.ToArray(), 26f, 26f);
    }

    static void BuildPlots(Transform parent)
    {
        for (int i = 0; i < PlotPos.Length; i++)
        {
            var t = new GameObject("Plot" + (i + 1)).transform;
            t.SetParent(parent, false);
            t.position = new Vector3(PlotPos[i].x, P, PlotPos[i].y);
            t.rotation = Quaternion.Euler(0f, PlotYaw[i], 0f);
            Place("Plot_Generic", t, t.position, PlotYaw[i], "Marker");
        }
    }

    static void BuildCatalog()
    {
        const string path = "Assets/Resources/FacilityCatalog.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var cat = AssetDatabase.LoadAssetAtPath<FacilityCatalog>(path);
        if (cat == null) { cat = ScriptableObject.CreateInstance<FacilityCatalog>(); AssetDatabase.CreateAsset(cat, path); }
        var list = new List<FacilityCatalog.Entry>();
        foreach (var (id, scale) in new[] { ("MissionControl", 1f), ("Hangar", 0.78f), ("FuelDepot", 1f), ("RadarTower", 1f), ("ResearchLab", 1f), ("TestStand", 1f) })
        {
            var e = new FacilityCatalog.Entry { id = id, scale = scale, levels = new GameObject[3] };
            for (int l = 1; l <= 3; l++) e.levels[l - 1] = AssetDatabase.LoadAssetAtPath<GameObject>(Site + id + "_L" + l + ".fbx");
            list.Add(e);
        }
        cat.entries = list.ToArray();
        cat.baseMaterial = baseMat; cat.lampOff = lampOff;
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
    }

    // ================================================================ fence along the highway side with two gates
    static void BuildFenceAndGates(Transform site)
    {
        var root = new GameObject("Fence").transform; root.SetParent(site, false);
        for (float x = -510f; x <= 510f; x += 20f)
        {
            if (Mathf.Abs(x - GateWest.x) < 13f || Mathf.Abs(x - GateMain.x) < 13f) continue;
            var go = Place("FenceSegment", root, new Vector3(x, P, FenceZ), 0f);
            if (go != null) go.isStatic = true;
        }
        Place("Gate", root, new Vector3(GateWest.x, P, FenceZ), -18f, "Gate West");
        Place("Gate", root, new Vector3(GateMain.x, P, FenceZ), 0f, "Gate Main");
    }

    // glowing lens material for the vehicle head and tail lights
    static Material LightMaterial(string name, Color c)
    {
        string path = GfxDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Unlit");
        m.SetColor("_BaseColor", c); m.color = c;
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        return m;
    }

    // ================================================================ the ends of the long highway: tunnel (west) and the bend round the mountain (east)
    static bool TerrainSlopeOk(float x, float z, float maxSlope)
    {
        var td = terrain.terrainData;
        return td.GetSteepness((x + 1500f) / 3000f, (z + 260f) / 960f) < maxSlope;
    }

    static void BuildRoadEnds(Transform site)
    {
        var root = new GameObject("Road Ends").transform; root.SetParent(site, false);
        var portal = Place("TunnelPortal", root, new Vector3(WestEnd, P + 0.1f, 0f), 90f, "Tunnel Portal");
        if (portal != null)
        {
            var pts = new List<Vector3>();
            foreach (float d in new[] { 10f, 22f, 34f }) pts.Add(new Vector3(0f, 10.8f, -d));       // local space: tunnel runs along -Z
            GlowAtNight(portal, pts.ToArray(), 70f, 24f);
        }

        // guardrails along the bend, chevron signs on the outside of the curve
        var path = roadPaths.ContainsKey("Highway") ? roadPaths["Highway"] : null;
        if (path != null)
            for (int i = 2; i < path.Count - 2; i += 2)
            {
                if (path[i].x < 880f) continue;
                Vector2 t = (path[i + 1] - path[i - 1]).normalized;
                Vector2 right = new Vector2(t.y, -t.x), left = -right;
                float yaw = Mathf.Atan2(-t.y, t.x) * Mathf.Rad2Deg;
                foreach (var side in new[] { right, left })
                {
                    var q = path[i] + side * 13.2f;
                    Place("Guardrail", root, new Vector3(q.x, GroundY(q.x, q.y), q.y), yaw, "Guardrail");
                }
                if (i % 12 == 0 && path[i].y > 5f)
                {
                    var q = path[i] + right * 14.5f; Vector2 f = left;
                    Place("ChevronSign", root, new Vector3(q.x, GroundY(q.x, q.y), q.y), Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg, "Chevron");
                }
            }

        // trees on the valley sides, boulders at the foot of the cliffs and around the portal (LOD prefabs from the pack)
        var rng = new System.Random(77);
        System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
        var far = new GameObject("Valley Trees").transform; far.SetParent(root, false);
        var rocks = new GameObject("Valley Rocks").transform; rocks.SetParent(root, false);
        string[] trees = { "Tree01A", "Tree01B" };
        int placed = 0, tries = 0;
        while (placed < 320 && tries < 40000)
        {
            tries++;
            float x = R(-1280f, 1240f), z = R(-190f, 610f);
            if (Mathf.Abs(x) < 480f) continue;
            float d = RoadDistance(x, z);
            if (d < 34f || d > 320f) continue;
            if (Mathf.PerlinNoise(x * 0.008f + 4f, z * 0.008f + 1f) < 0.5f) continue;
            float y = GroundY(x, z);
            if (y < 5f || y > 140f || !TerrainSlopeOk(x, z, 28f)) continue;
            Spawn(trees[rng.Next(2)], far, new Vector3(x, y - 0.4f, z), R(0, 360), R(3f, 6f), true);
            placed++;
        }
        placed = 0; tries = 0;
        while (placed < 90 && tries < 20000)
        {
            tries++;
            float x = R(-1300f, 1240f), z = R(-30f, 600f);
            if (Mathf.Abs(x) < 480f) continue;
            float d = RoadDistance(x, z);
            if (d < 30f || d > 90f) continue;
            float y = GroundY(x, z);
            Spawn(rng.NextDouble() < 0.5 ? "Rock01" : "Rock02", rocks, new Vector3(x, y - 0.6f, z), R(0, 360), R(2.5f, 6.5f));
            placed++;
        }
        for (int i = 0; i < 14; i++)                                                     // rubble in front of the portal cliff
        {
            float x = WestEnd + R(8f, 40f), z = (i % 2 == 0 ? -1f : 1f) * R(28f, 70f);
            Spawn(rng.NextDouble() < 0.5 ? "Rock01" : "Rock02", rocks, new Vector3(x, GroundY(x, z) - 0.6f, z), R(0, 360), R(3f, 7f));
        }
    }

    // ================================================================ lamps along the highway median
    static void BuildLamps(Transform site)
    {
        var lamps = new GameObject("Lamps").transform; lamps.SetParent(site, false);
        for (float x = -1260f; x <= 900f; x += 60f)
        {
            var go = Place("HighwayLamp", lamps, new Vector3(x, P + 0.1f, 0f), 0f, "HighwayLamp");
            if (go == null) continue;
            var nl = go.AddComponent<NightLamp>();
            nl.lampRenderer = go.GetComponentInChildren<MeshRenderer>();
            nl.lampSlot = 1; nl.onMaterial = lampOn; nl.offMaterial = lampOff;
            var lg = new GameObject("Light");
            lg.transform.SetParent(go.transform, false);
            lg.transform.position = new Vector3(x, P + 12.0f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 34f; l.color = new Color(1f, 0.82f, 0.55f); l.intensity = 0f; l.shadows = LightShadows.None; l.enabled = false;
            nl.lights = new[] { l }; nl.onIntensity = 45f;
        }
    }

    static void AddPadSpot(Transform parent, string name, Vector3 pos, Vector3 target, Color color, float angle, float intensity, float range)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(target - pos);
        var l = g.AddComponent<Light>();
        l.type = LightType.Spot; l.spotAngle = angle; l.range = range; l.color = color;
        l.intensity = 0f; l.shadows = LightShadows.None; l.enabled = false;
        var nl = g.AddComponent<NightLamp>();
        nl.lights = new[] { l }; nl.onIntensity = intensity; nl.lampRenderer = null;
    }

    static void BuildPadLights(Transform site)
    {
        var lights = new GameObject("Pad Lights").transform; lights.SetParent(site, false);
        var target = new Vector3(PadCenter.x, P + 38f, PadCenter.z);
        int i = 0;
        foreach (var (dx, dz) in new[] { (-52f, -52f), (52f, -52f), (-52f, 52f), (52f, 52f) })
        {
            var g = new GameObject("Pad Floodlight " + (++i));
            g.transform.SetParent(lights, false);
            var pos = new Vector3(PadCenter.x + dx, P + 14f, PadCenter.z + dz);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.LookRotation(target - pos);
            var l = g.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 55f; l.range = 150f; l.color = new Color(1f, 0.96f, 0.88f);
            l.intensity = 0f; l.shadows = LightShadows.None; l.enabled = false;
            var nl = g.AddComponent<NightLamp>();
            nl.lights = new[] { l }; nl.onIntensity = 2200f; nl.lampRenderer = null;
        }

        // strong white key lights on the rocket from four sides, high up
        var rocketMid = new Vector3(PadCenter.x, P + 34f, PadCenter.z);
        int k = 0;
        foreach (var (dx, dz) in new[] { (-62f, 0f), (62f, 0f), (0f, -62f), (0f, 62f) })
        {
            var pos = new Vector3(PadCenter.x + dx, P + 8f, PadCenter.z + dz);
            AddPadSpot(lights, "Rocket Key " + (++k), pos, rocketMid, new Color(0.93f, 0.96f, 1f), 38f, 3500f, 230f);
        }
        // coloured accents, low and aimed up the rocket / tower like real pad lighting
        var accents = new (float dx, float dz, Color c, string n)[]
        {
            (-22f, -22f, new Color(1f, 0.55f, 0.15f), "amber"), (22f, -22f, new Color(0.15f, 0.65f, 1f), "cyan"),
            (-22f, 22f, new Color(0.8f, 0.25f, 0.95f), "magenta"), (22f, 22f, new Color(1f, 0.35f, 0.25f), "red"),
        };
        foreach (var a in accents)
            AddPadSpot(lights, "Accent " + a.n, new Vector3(PadCenter.x + a.dx, P + 0.6f, PadCenter.z + a.dz), new Vector3(PadCenter.x, P + 42f, PadCenter.z), a.c, 40f, 26000f, 150f);
        // soft coloured washes on other buildings: blue on mission control side, amber on the power plant, teal on the hangar front
        foreach (var (pos, c, n) in new[] { (new Vector3(-32f, P + 1f, 118f), new Color(0.25f, 0.55f, 1f), "mission"), (new Vector3(-375f, P + 1f, 160f), new Color(1f, 0.6f, 0.2f), "power"), (new Vector3(92f, P + 1f, 236f), new Color(0.2f, 0.9f, 0.8f), "hangar") })
            AddPadSpot(lights, "Accent " + n, pos, pos + new Vector3(0f, 14f, 0f), c, 60f, 6000f, 80f);
    }

    // ================================================================ nature: sparse greenery on the site, LOD forest and mountains far away
    static GameObject Spawn(string prefab, Transform parent, Vector3 pos, float yaw, float scale, bool lean = false)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Nature + prefab + ".prefab");
        if (p == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(lean ? Random.Range(-3f, 3f) : 0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        go.isStatic = true;
        return go;
    }

    // rectangles (centre x, centre z, half width, half depth) the greenery must stay out of
    static readonly Vector4[] Keepout =
    {
        new Vector4(-187, 161, 100, 85), new Vector4(-375, 160, 80, 52), new Vector4(-32, 118, 50, 40),
        new Vector4(-50, 62, 38, 28), new Vector4(-22, 231, 46, 42), new Vector4(92, 236, 46, 42), new Vector4(199, 233, 46, 42),
        new Vector4(85, 121, 46, 42), new Vector4(331, 221, 46, 42),
    };

    static bool InKeepout(float x, float z)
    {
        foreach (var k in Keepout) if (Mathf.Abs(x - k.x) < k.z && Mathf.Abs(z - k.y) < k.w) return true;
        if (Mathf.Abs(x - carAPos.x) < 44f && Mathf.Abs(z - carAPos.y) < 44f) return true;   // car park A (rotated)
        if (Mathf.Abs(z) < 22f) return true;                                         // highway
        if (Mathf.Abs(z - FenceZ) < 4f) return true;                                 // fence line
        foreach (var kv in roadPaths)                                                // roads
        {
            if (kv.Key == "Highway") continue;
            var p = kv.Value;
            for (int i = 0; i < p.Count; i += 2)
                if ((new Vector2(x, z) - p[i]).sqrMagnitude < 13f * 13f) return true;
        }
        return false;
    }

    static void BuildNature(Transform site)
    {
        var rng = new System.Random(21);
        System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
        var root = new GameObject("Nature").transform; root.SetParent(site, false);
        var near = new GameObject("Greenery").transform; near.SetParent(root, false);
        var far = new GameObject("Background Forest").transform; far.SetParent(root, false);
        var rocks = new GameObject("Rocks").transform; rocks.SetParent(root, false);
        string[] trees = { "Tree01A", "Tree01B" };
        string[] grass = { "Grass01A", "Grass01B", "Grass01C" };
        string[] flowers = { "Flower01A", "Flower01B", "Flowers01A", "Flowers01B" };

        // ---- staff housing: trees, bushes and flowers on the lawn (the dorm blocks, paths and hall are kept clear)
        var hc = new Vector2(317f, 110f);
        var dorms = new List<Vector2>();
        foreach (float dx in new[] { -72f, -36f, 0f, 36f, 72f }) { dorms.Add(hc + new Vector2(dx, -34f)); dorms.Add(hc + new Vector2(dx, 30f)); }
        System.Func<Vector2, bool> housingFree = q =>
        {
            foreach (var d in dorms) if (Mathf.Abs(q.x - d.x) < 13f && Mathf.Abs(q.y - d.y) < 9f) return false;
            if (Mathf.Abs(q.x - hc.x) < 20f && Mathf.Abs(q.y - (hc.y - 3f)) < 13f) return false;                 // hall (and its canopy)
            if (Mathf.Abs(q.y - (hc.y + 18f)) < 3.5f || Mathf.Abs(q.y - (hc.y - 24f)) < 3.5f) return false;     // footpaths
            foreach (float px in new[] { -72f, 0f, 72f }) if (Mathf.Abs(q.x - (hc.x + px)) < 3.5f && Mathf.Abs(q.y - (hc.y - 3f)) < 25f) return false;
            return true;
        };
        int placed = 0, tries = 0;
        while (placed < 46 && tries < 3000)
        {
            tries++;
            var q = hc + new Vector2(R(-92f, 108f), R(-52f, 56f));
            if (!housingFree(q)) continue;
            Spawn(trees[rng.Next(2)], near, new Vector3(q.x, GroundY(q.x, q.y), q.y), R(0, 360), R(1.9f, 3.0f), true);
            placed++;
        }
        placed = 0; tries = 0;
        while (placed < 60 && tries < 3000)
        {
            tries++;
            var q = hc + new Vector2(R(-92f, 100f), R(-54f, 56f));
            if (!housingFree(q)) continue;
            Spawn("Bush01", near, new Vector3(q.x, GroundY(q.x, q.y), q.y), R(0, 360), R(1.4f, 2.4f));
            placed++;
        }
        for (int c = 0; c < 26; c++)                                                                      // flower and grass patches
        {
            var q = hc + new Vector2(R(-90f, 98f), R(-50f, 54f));
            if (!housingFree(q)) continue;
            for (int k = 0; k < 7; k++)
            {
                var f = q + new Vector2(R(-4f, 4f), R(-4f, 4f));
                Spawn(flowers[rng.Next(flowers.Length)], near, new Vector3(f.x, GroundY(f.x, f.y), f.y), R(0, 360), R(2.5f, 4f));
                var g2 = q + new Vector2(R(-5f, 5f), R(-5f, 5f));
                Spawn(grass[rng.Next(3)], near, new Vector3(g2.x, GroundY(g2.x, g2.y), g2.y), R(0, 360), R(2f, 3.2f));
            }
        }
        // a green belt around the housing
        for (int i = 0; i < 40; i++)
        {
            float a = R(0f, Mathf.PI * 2f);
            var q = hc + new Vector2(Mathf.Cos(a) * R(112f, 140f), Mathf.Sin(a) * R(72f, 96f));
            if (InKeepout(q.x, q.y) || q.y < FenceZ + 8f) continue;
            Spawn(trees[rng.Next(2)], near, new Vector3(q.x, GroundY(q.x, q.y), q.y), R(0, 360), R(2.2f, 3.4f), true);
        }

        // ---- verge of the highway: a few trees on the base side, bushes and rocks on the river bank
        for (float x = -500f; x < 500f; x += R(26f, 46f))
        {
            if (Mathf.Abs(x - GateWest.x) < 22f || Mathf.Abs(x - GateMain.x) < 22f) continue;
            float z = R(17f, 25f);                                                    // between the highway edge and the fence
            Spawn(trees[rng.Next(2)], near, new Vector3(x, GroundY(x, z), z), R(0, 360), R(2.2f, 3.2f), true);
        }
        for (float x = -500f; x < 500f; x += R(22f, 40f))
        {
            float z = -R(16f, 30f);
            Spawn(rng.NextDouble() < 0.6 ? "Bush01" : trees[rng.Next(2)], near, new Vector3(x, GroundY(x, z), z), R(0, 360), R(1.6f, 2.8f), true);
        }
        for (int i = 0; i < 40; i++)                                                  // rocks on the shore
        {
            float x = R(-500f, 500f), z = -R(30f, 46f);
            Spawn(rng.NextDouble() < 0.7 ? "Rock01" : "Rock02", rocks, new Vector3(x, GroundY(x, z) - 0.3f, z), R(0, 360), R(0.8f, 1.8f));
        }

        // ---- open plateau: sparse trees in the gaps (no dense forest)
        placed = 0; tries = 0;
        while (placed < 70 && tries < 6000)
        {
            tries++;
            float x = R(-490f, 490f), z = R(40f, 330f);
            if (InKeepout(x, z) || (Mathf.Abs(x - hc.x) < 110f && Mathf.Abs(z - hc.y) < 70f)) continue;
            if (Mathf.PerlinNoise(x * 0.012f + 5f, z * 0.012f + 9f) < 0.55f) continue;       // clusters, not a carpet
            Spawn(trees[rng.Next(2)], near, new Vector3(x, GroundY(x, z), z), R(0, 360), R(2.2f, 3.6f), true);
            placed++;
        }

        // ---- the pack's grass tufts: loose patches across the open ground, thicker along the roads and the river bank
        placed = 0; tries = 0;
        while (placed < 520 && tries < 20000)
        {
            tries++;
            float x = R(-490f, 490f), z = R(40f, 340f);
            if (InKeepout(x, z) || (Mathf.Abs(x - hc.x) < 110f && Mathf.Abs(z - hc.y) < 70f)) continue;
            if (Mathf.PerlinNoise(x * 0.02f + 11f, z * 0.02f + 3f) < 0.5f) continue;        // patches, not a carpet
            for (int k = 0; k < 3; k++)
            {
                float gx = x + R(-4f, 4f), gz = z + R(-4f, 4f);
                if (InKeepout(gx, gz)) continue;
                Spawn(grass[rng.Next(3)], near, new Vector3(gx, GroundY(gx, gz), gz), R(0, 360), R(2.2f, 3.6f));
            }
            placed++;
        }
        for (float x = -500f; x < 500f; x += R(5f, 9f))                                  // river bank
        {
            float z = -R(14f, 36f);
            Spawn(grass[rng.Next(3)], near, new Vector3(x, GroundY(x, z), z), R(0, 360), R(2.4f, 4f));
        }

        // ---- background: LOD forest on the foothills and the far river bank (3 LOD levels each); the mountains are the terrain itself
        placed = 0; tries = 0;
        while (placed < 330 && tries < 20000)
        {
            tries++;
            float x = R(-520f, 520f), z = R(335f, 720f);
            float h = GroundY(x, z);
            if (Mathf.PerlinNoise(x * 0.006f + 2f, z * 0.006f + 8f) < 0.5f) continue;
            Spawn(trees[rng.Next(2)], far, new Vector3(x, h - 0.4f, z), R(0, 360), R(3.5f, 6.5f), true);
            placed++;
        }
        for (int i = 0; i < 45; i++)                                                  // far bank across the river
        {
            float x = R(-520f, 520f), z = -R(205f, 255f);
            Spawn(trees[rng.Next(2)], far, new Vector3(x, GroundY(x, z) - 0.4f, z), R(0, 360), R(2.6f, 4.2f), true);
        }
        for (int i = 0; i < 24; i++)                                                  // boulders on the slopes
        {
            float x = R(-480f, 480f), z = R(330f, 620f);
            Spawn(rng.NextDouble() < 0.5 ? "Rock01" : "Rock02", rocks, new Vector3(x, GroundY(x, z) - 0.5f, z), R(0, 360), R(2.5f, 6f));
        }
    }

    // ================================================================ rocket on the mount (realistic size)
    static void SetupRocket()
    {
        var dm = Object.FindAnyObjectByType<DeploymentManager>(FindObjectsInactive.Include);
        float mountTop = P + 12.25f;
        var pos = new Vector3(PadCenter.x, mountTop, PadCenter.z);
        if (dm != null)
        {
            dm.rocketPosition = pos; dm.rocketScale = RocketScale;
            EditorUtility.SetDirty(dm);
        }
        var falcon = GameObject.Find("Falcon (Blender)");
        if (falcon != null)
        {
            falcon.transform.position = pos; falcon.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            falcon.transform.localScale = Vector3.one * 100f * RocketScale;      // the FBX is in centimetres (x100)
        }
    }

    // ================================================================ traffic on the highway and the site roads
    // closest point of a polyline to p (distance and the segment index it lies on)
    static float DistToPolyline(Vector2[] pl, Vector2 p, out int seg, out Vector2 foot)
    {
        float best = float.MaxValue; seg = 0; foot = pl[0];
        for (int i = 0; i < pl.Length - 1; i++)
        {
            Vector2 a = pl[i], ab = pl[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            Vector2 f = a + ab * t; float d = (p - f).magnitude;
            if (d < best) { best = d; seg = i; foot = f; }
        }
        return best;
    }

    static void SetupTraffic()
    {
        var tm = Object.FindAnyObjectByType<TrafficManager>(FindObjectsInactive.Include);
        if (tm == null) return;

        // road centre lines for the vehicles: the base roads reach all the way to the highway centre line,
        // so a vehicle can turn from the highway into the base (the road meshes themselves stop at the highway lane edge)
        var lines = new List<(string name, List<Vector2> pts, float radius)>();
        foreach (var r in Roads)
        {
            var pts = r.pts.ToList();
            if (!r.highway && Mathf.Abs(pts[0].y - HwEdge) < 0.01f) pts.Insert(0, new Vector2(pts[0].x, 0f));
            lines.Add((r.name, pts, r.radius));
        }
        // every road that starts or ends on another road gets a shared vertex on that road, so the graph is joined there
        for (int i = 0; i < lines.Count; i++)
            foreach (var end in new[] { lines[i].pts[0], lines[i].pts[lines[i].pts.Count - 1] })
                for (int j = 0; j < lines.Count; j++)
                {
                    if (i == j) continue;
                    var other = lines[j].pts.ToArray();
                    if (DistToPolyline(other, end, out int seg, out Vector2 foot) > 0.75f) continue;
                    if (Vector2.Distance(foot, other[seg]) > 0.05f && Vector2.Distance(foot, other[seg + 1]) > 0.05f) lines[j].pts.Insert(seg + 1, end);
                }

        var list = new List<TrafficManager.Road>();
        foreach (var (name, pts, radius) in lines)
            list.Add(new TrafficManager.Road { points = Smooth(pts.ToArray(), radius, 2f).ToArray(), highway = name == "Highway" });
        tm.roads = list.ToArray();
        tm.laneOffset = 2.9f; tm.highwayLanes = new[] { 3.6f, 8.6f }; tm.highwayCount = 36; tm.facilityCount = 5;
        tm.highwaySpeed = new Vector2(22f, 38f); tm.facilitySpeed = new Vector2(6f, 10f);
        tm.headLightMaterial = LightMaterial("Vehicle_Head", new Color(1f, 0.96f, 0.82f)); tm.tailLightMaterial = LightMaterial("Vehicle_Tail", new Color(1f, 0.08f, 0.05f));
        tm.obstacleMargin = 1.5f;
        tm.ignoreObstacleNames = new[] { "LaunchPad", "CarParkA", "CarParkB", "StaffHousing" };
        EditorUtility.SetDirty(tm);
    }

    // ================================================================ camera: overview from the river side, with pan and zoom
    static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var rig = cam.GetComponent<CameraRig>();
        if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
        rig.focus = new Vector3(-15f, P, 175f); rig.distance = 640f; rig.yaw = 0f; rig.pitch = 23f;
        rig.xLimits = new Vector2(-600f, 600f); rig.zLimits = new Vector2(-60f, 380f); rig.zoomedPitch = 60f; rig.zoomSpeed = 0.05f; rig.visibleHalfWidth = 540f; rig.minDistance = 120f; rig.maxDistance = 640f;
        var rot = Quaternion.Euler(rig.pitch, rig.yaw, 0f);
        cam.transform.rotation = rot;
        cam.transform.position = rig.focus - rot * Vector3.forward * rig.distance;
        cam.fieldOfView = 40f; cam.farClipPlane = 3000f; cam.nearClipPlane = 0.3f;      // the UI canvas sits 1 unit from the camera
        EditorUtility.SetDirty(rig); EditorUtility.SetDirty(cam);
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset rp)
        {
            rp.shadowDistance = 800f;
            EditorUtility.SetDirty(rp);
        }
    }
}
