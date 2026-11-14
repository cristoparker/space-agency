using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The launch cutscene. The game clock pauses, the HUD hides behind letterbox bars, a countdown runs and the rocket on the pad lifts off
/// into the sky with fire, smoke and a light, filmed by three cameras (pad, tower, far). Every camera stands low inside the map
/// and looks towards the mountains or up at the sky, so the edge of the world is never in the picture.
/// Click, Space or Esc skips it. When it ends the rocket is gone from the pad and the contract it was deployed for is completed.
/// </summary>
public class LaunchSequence : MonoBehaviour
{
    public static bool Running { get; private set; }
    static LaunchSequence inst;

    public static void ResetStatics() { Running = false; inst = null; }

    FlightRecord record;
    FlightProfile profile;

    public static bool Begin()
    {
        if (Running) return false;
        var dep = DeploymentManager.Instance;
        if (dep == null || dep.PadRocket == null || !MissionObjectives.LaunchAllowed(dep)) return false;
        var route = TeamSync.Route(TeamRole.Safety, TeamAction.Launch);          // shared game: the Safety officer gives the GO, the host flies it
        if (route != TeamSync.RouteResult.Local) return route == TeamSync.RouteResult.Sent;
        if (inst == null) inst = new GameObject("Launch Sequence").AddComponent<LaunchSequence>();
        var mission = MissionManager.Instance != null && dep.Current != null && !string.IsNullOrEmpty(dep.Current.missionId)
            ? MissionManager.Instance.Running.FirstOrDefault(x => x.Definition.id == dep.Current.missionId)?.Definition : null;
        inst.record = dep.library != null && dep.Current != null ? FlightOutcome.Evaluate(dep.Current, dep.library, mission) : new FlightRecord();
        inst.profile = FlightOutcome.LastProfile;
        TeamSync.HostLaunched(inst.record);
        inst.StartCoroutine(inst.Run(dep));
        return true;
    }

    /// <summary>A guest in a shared game watches the host's launch: same rocket, same path, the host's result.</summary>
    public static void Spectate(FlightRecord hostRecord)
    {
        if (Running || hostRecord == null) return;
        var dep = DeploymentManager.Instance;
        if (dep == null) return;
        if (dep.PadRocket == null) dep.ReloadFromSave();
        if (dep.PadRocket == null || dep.Current == null) { Hud.Toast("Liftoff! \"" + hostRecord.rocketName + "\" is on its way."); return; }
        if (inst == null) inst = new GameObject("Launch Sequence").AddComponent<LaunchSequence>();
        var mission = MissionManager.Instance != null && !string.IsNullOrEmpty(dep.Current.missionId)
            ? MissionManager.Instance.Running.FirstOrDefault(x => x.Definition.id == dep.Current.missionId)?.Definition : null;
        if (dep.library != null) FlightOutcome.Evaluate(dep.Current, dep.library, mission);     // only for the flight path (it does not depend on luck)
        inst.record = hostRecord;
        inst.profile = FlightOutcome.LastProfile;
        inst.StartCoroutine(inst.Run(dep));
    }

    // ---------------------------------------------------------------- the show
    Camera cam;
    CameraRig rig;
    RectTransform overlay;
    Image fade;
    TMP_Text countdown, hint;
    readonly List<GameObject> hidden = new List<GameObject>();
    readonly List<GameObject> fx = new List<GameObject>();
    readonly List<ParticleSystem> systems = new List<ParticleSystem>();      // stepped by hand while the game clock is paused
    static Texture2D soft;
    bool skip;

    IEnumerator Run(DeploymentManager dep)
    {
        Running = true;
        var clock = GameClock.Instance;
        int prevSpeed = clock != null ? Mathf.Max(1, clock.SpeedIndex) : 1;
        if (clock != null) clock.SetSpeedIndex(0);
        string missionId = dep.Current != null ? dep.Current.missionId : "";
        string missionName = dep.Current != null ? dep.Current.missionName : "";

        var rocket = dep.PadRocket;
        Vector3 basePos = rocket.transform.position;
        // the size of this rocket (designs differ a lot): the onboard camera must stay OUTSIDE it, however big it is
        float rocketH = 30f, rocketR = 4f;
        {
            Bounds rb = default; bool has = false;
            foreach (var mr in rocket.GetComponentsInChildren<MeshRenderer>()) { if (!has) { rb = mr.bounds; has = true; } else rb.Encapsulate(mr.bounds); }
            if (has) { rocketH = Mathf.Max(8f, rb.size.y); rocketR = Mathf.Max(1.5f, Mathf.Max(rb.size.x, rb.size.z) * 0.5f); }
        }
        cam = Camera.main;
        rig = cam != null ? cam.GetComponent<CameraRig>() : null;
        Vector3 camPos = cam.transform.position; Quaternion camRot = cam.transform.rotation; float camFov = cam.fieldOfView;
        if (rig != null) rig.enabled = false;
        BuildOverlay();

        // ---- effects on the rocket
        var flame = MakeFlame(rocket.transform);
        var trail = MakeTrail(rocket.transform);
        var billow = MakeBillow(basePos);
        var light = MakeLight(rocket.transform);

        float t = 0f, lift = 4.0f, v = 0f, pitch = 0f, a = 11f;
        Vector3 pos = basePos;
        int shot = -1;
        Vector3 look = basePos + Vector3.up * 30f;
        bool ignited = false;
        float end = 21.5f;
        bool fails = record != null && record.Result == LaunchResult.Failure, blasted = false;
        float blastT = Random.Range(10.5f, 13f);

        while (t < end && !skip)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (t > 0.6f && ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (mouse != null && mouse.leftButton.wasPressedThisFrame))) skip = true;

            // countdown text
            string txt = t < 1f ? "T-3" : t < 2f ? "T-2" : t < 3f ? "T-1" : t < lift + 1.2f ? "LIFTOFF" : "";
            countdown.text = txt;
            float frac = t % 1f;
            countdown.color = new Color(1f, 1f, 1f, txt == "" ? 0f : Mathf.Lerp(1f, 0.25f, frac));
            countdown.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Min(1f, frac * 3f));

            // ignition
            if (!ignited && t >= 3f)
            {
                ignited = true;
                flame.Play(); trail.Play(); billow.Play();
            }
            if (ignited)
            {
                var em = flame.emission; em.rateOverTime = Mathf.Lerp(30f, 160f, Mathf.Clamp01((t - 3f) / 1.4f));
                light.intensity = Mathf.Lerp(0f, 5200f, Mathf.Clamp01((t - 3f) / 1.2f)) * Random.Range(0.85f, 1.1f);
            }

            // flight: slow start, steadily faster, leaning over a little
            if (t > lift)
            {
                float ft = t - lift;
                v += a * (0.35f + Mathf.Min(1f, ft / 5f) * 0.9f) * dt;
                pitch = Mathf.Clamp((ft - 4f) * 1.6f, 0f, 20f);
                rocket.transform.rotation = Quaternion.AngleAxis(pitch, Vector3.right);
                pos += rocket.transform.up * v * dt;
                rocket.transform.position = pos;
            }
            else rocket.transform.position = basePos + new Vector3(Random.Range(-0.03f, 0.03f), 0f, Random.Range(-0.03f, 0.03f)) * (ignited ? 1f : 0f);

            // a failed launch: the rocket blows up
            if (fails && !blasted && t >= blastT)
            {
                blasted = true;
                rocket.SetActive(false);
                foreach (var ps0 in new[] { flame, trail }) { var e0 = ps0.emission; e0.rateOverTime = 0f; e0.rateOverDistance = 0f; }
                light.intensity = 90000f;
                var blast = MakeBlast(pos);
                blast.Play();
                end = Mathf.Min(end, t + 5.5f);
                fadeAlpha = 0.9f;
            }
            if (blasted) light.intensity = Mathf.MoveTowards(light.intensity, 0f, 60000f * dt);

            // the game clock is paused (timeScale 0), so the engine does not move the particles: step them here
            if (ignited) foreach (var psys in systems) if (psys != null) psys.Simulate(dt, false, false, false);

            // cameras
            int want = t < 6.4f ? 0 : t < 9.4f ? 3 : t < 14.5f ? 1 : 2;      // 3 = rocket POV
            bool cut = false;
            if (want != shot)
            {
                shot = want; cut = true; cutT = t;
                fadeAlpha = 1f;                                          // a quick dip to black on every cut
                if (shot == 0) { cam.fieldOfView = 46f; }
                if (shot == 1) { cam.fieldOfView = 58f; }
                if (shot == 2) { cam.fieldOfView = 34f; }
                if (shot == 3) { cam.fieldOfView = 68f; }
            }
            Vector3 cpos, target;
            if (shot == 3)
            {
                // a chase view from above and beside the rocket (never inside the body, whatever its size): it looks down along the rocket
                // while the pad falls away behind it
                cpos = pos + rocket.transform.up * (rocketH * 0.75f + 6f) + rocket.transform.right * (rocketR * 2.4f + 9f);
                target = Vector3.Lerp(pos + rocket.transform.up * (rocketH * 0.4f), basePos, 0.3f);
            }
            else if (shot == 0)
            {
                cpos = basePos + new Vector3(92f, 9f, -78f) + (new Vector3(70f, 9f, -62f) - new Vector3(92f, 9f, -78f)) * Mathf.Clamp01(t / 9f);
                target = Vector3.Lerp(basePos + Vector3.up * 34f, pos + Vector3.up * 16f, t > lift ? 0.8f : 0f);
            }
            else if (shot == 1)
            {
                cpos = basePos + new Vector3(-58f, 6f, -86f);
                target = pos + rocket.transform.up * 12f;
            }
            else
            {
                cpos = basePos + new Vector3(-28f, 24f, -330f);
                target = pos;
            }
            cpos.y = Mathf.Max(cpos.y, GroundY(cpos) + 4f);
            cam.transform.position = cpos;
            if (cut) look = target;                                      // snap at the cut: nothing carries over from the previous shot
            else look = Vector3.Lerp(look, target, 1f - Mathf.Exp(-(shot == 0 ? 4f : 7f) * dt));
            var want_rot = Quaternion.LookRotation((look - cpos).normalized, Vector3.up);
            cam.transform.rotation = cut ? want_rot : Quaternion.Slerp(cam.transform.rotation, want_rot, 1f - Mathf.Exp(-12f * dt));
            if (ignited && shot != 2)                                    // smooth rumble, faded in after each cut
            {
                float amp = 0.07f * Mathf.Clamp01((t - 3f) / 2f) * Mathf.Clamp01((t - cutT) / 0.8f);
                cam.transform.position += new Vector3(Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f, Mathf.PerlinNoise(t * 9f, 7.7f) - 0.5f, Mathf.PerlinNoise(t * 9f, 15.1f) - 0.5f) * 2f * amp;
            }

            // fades: dip on cuts, black at the very end
            fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, dt * 5f);
            float endFade = Mathf.Clamp01((t - (end - 1.4f)) / 1.2f);
            fade.color = new Color(0f, 0f, 0f, Mathf.Max(fadeAlpha, endFade));
            yield return null;
        }

        // ---- finish: black for a moment, clean up, give the camera back
        fade.color = Color.black;
        yield return null;
        foreach (var go in fx) if (go != null) Destroy(go);
        fx.Clear(); systems.Clear();
        dep.Consume();
        cam.transform.position = camPos; cam.transform.rotation = camRot; cam.fieldOfView = camFov;
        if (rig != null) rig.enabled = true;
        foreach (var go in hidden) if (go != null) go.SetActive(true);
        hidden.Clear();
        if (overlay != null) Destroy(overlay.gameObject);
        if (clock != null) clock.SetSpeedIndex(prevSpeed);
        Running = false;
        skip = false;

        // next: the flight scene (the office monitor). It can be skipped too; afterwards the launch report opens in the game
        var rec = record; var prof = profile; record = null; profile = null;
        FlightSceneLauncher.Begin(rec, prof, () =>
        {
            if (FlightManager.Instance != null && rec != null) FlightManager.Instance.Launched(rec);
            else Hud.Toast("Launch complete.");
        });
    }

    float fadeAlpha, cutT;

    static float GroundY(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;
    }

    // ---------------------------------------------------------------- overlay
    void BuildOverlay()
    {
        var hud = FindAnyObjectByType<HudController>();
        var canvas = (RectTransform)hud.transform;
        // hide the whole HUD, keep only the cutscene overlay
        foreach (Transform c in canvas) if (c.gameObject.activeSelf) { hidden.Add(c.gameObject); c.gameObject.SetActive(false); }
        overlay = new GameObject("Launch Overlay", typeof(RectTransform)).GetComponent<RectTransform>();
        overlay.SetParent(canvas, false);
        UIKit.Fill(overlay);
        overlay.SetAsLastSibling();

        foreach (bool topBar in new[] { true, false })                           // letterbox bars
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            bar.SetParent(overlay, false);
            bar.anchorMin = new Vector2(0f, topBar ? 1f : 0f); bar.anchorMax = new Vector2(1f, topBar ? 1f : 0f);
            bar.pivot = new Vector2(0.5f, topBar ? 1f : 0f); bar.sizeDelta = new Vector2(0f, 30f); bar.anchoredPosition = Vector2.zero;
            bar.GetComponent<Image>().color = Color.black; bar.GetComponent<Image>().raycastTarget = false;
        }
        countdown = UIKit.Text(overlay, "", 46f, TextAlignmentOptions.Center, true);
        countdown.raycastTarget = false;
        hint = UIKit.TextAt(overlay, "Hint", 0, 0, 200, 12, "", 5.8f, TextAlignmentOptions.MidlineRight, false, new Color(1f, 1f, 1f, 0.6f));
        var hr = (RectTransform)hint.transform.parent; hr.anchorMin = hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(1f, 0f); hr.anchoredPosition = new Vector2(-10f, 8f);
        hint.text = "Click or press Space to skip";
        var f = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        f.SetParent(overlay, false); UIKit.Fill(f);
        fade = f.GetComponent<Image>(); fade.color = new Color(0f, 0f, 0f, 1f); fade.raycastTarget = false;
        fadeAlpha = 1f;
        var click = new GameObject("Catcher", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        click.SetParent(overlay, false); UIKit.Fill(click);
        click.GetComponent<Image>().color = Color.clear;
    }

    // ---------------------------------------------------------------- effects
    static Texture2D Soft()
    {
        if (soft != null) return soft;
        const int n = 64;
        soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d); a *= a * (3f - 2f * a);
                soft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        soft.Apply();
        return soft;
    }

    ParticleSystem NewPs(string name, Transform parent, Vector3 worldPos)
    {
        var go = new GameObject(name);
        fx.Add(go);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = true; main.useUnscaledTime = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        systems.Add(ps);
        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = Soft() };
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    static Gradient Grad(params (float t, Color c)[] keys)
    {
        var g = new Gradient();
        var ck = new List<GradientColorKey>(); var ak = new List<GradientAlphaKey>();
        foreach (var k in keys) { ck.Add(new GradientColorKey(new Color(k.c.r, k.c.g, k.c.b), k.t)); ak.Add(new GradientAlphaKey(k.c.a, k.t)); }
        g.SetKeys(ck.ToArray(), ak.ToArray());
        return g;
    }

    ParticleSystem MakeFlame(Transform rocket)
    {
        var ps = NewPs("Flame", rocket, rocket.position);
        ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); main.startSpeed = new ParticleSystem.MinMaxCurve(26f, 40f);
        main.startSize = new ParticleSystem.MinMaxCurve(6f, 10f); main.maxParticles = 800;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = 1.6f;
        var em = ps.emission; em.rateOverTime = 0f;
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = Grad((0f, new Color(1f, 0.95f, 0.7f, 1f)), (0.35f, new Color(1f, 0.6f, 0.2f, 0.85f)), (1f, new Color(1f, 0.3f, 0.1f, 0f)));
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 0.2f));
        return ps;
    }

    ParticleSystem MakeTrail(Transform rocket)
    {
        var ps = NewPs("Trail", rocket, rocket.position);
        ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f); main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(9f, 16f); main.maxParticles = 3000;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.5f;
        var em = ps.emission; em.rateOverTime = 0f; em.rateOverDistance = 1.5f;
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = Grad((0f, new Color(0.95f, 0.93f, 0.9f, 0.7f)), (0.5f, new Color(0.85f, 0.85f, 0.88f, 0.4f)), (1f, new Color(0.8f, 0.82f, 0.86f, 0f)));
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.6f));
        return ps;
    }

    ParticleSystem MakeBlast(Vector3 at)
    {
        var ps = NewPs("Blast", null, at);
        var main = ps.main;
        main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 60f);
        main.startSize = new ParticleSystem.MinMaxCurve(14f, 38f); main.maxParticles = 900; main.gravityModifier = 0.15f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 4f;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 500) });
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = Grad((0f, new Color(1f, 0.95f, 0.7f, 1f)), (0.2f, new Color(1f, 0.55f, 0.15f, 0.9f)), (0.6f, new Color(0.25f, 0.22f, 0.22f, 0.7f)), (1f, new Color(0.15f, 0.15f, 0.17f, 0f)));
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));
        return ps;
    }

    ParticleSystem MakeBillow(Vector3 basePos)
    {
        var ps = NewPs("Billow", null, basePos + Vector3.up * 1.5f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f); main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(16f, 30f); main.maxParticles = 600; main.gravityModifier = -0.02f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 7f; sh.rotation = new Vector3(-90f, 0f, 0f);
        var em = ps.emission; em.rateOverTime = 55f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.radial = new ParticleSystem.MinMaxCurve(7f, 13f);
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = Grad((0f, new Color(1f, 0.85f, 0.65f, 0.55f)), (0.15f, new Color(0.95f, 0.94f, 0.92f, 0.7f)), (1f, new Color(0.8f, 0.82f, 0.86f, 0f)));
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.6f));
        // the cloud stops being fed after a few seconds
        StartCoroutine(StopLater(ps, 11.5f));
        return ps;
    }

    IEnumerator StopLater(ParticleSystem ps, float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        if (ps != null) { var em = ps.emission; em.rateOverTime = 0f; }
    }

    Light MakeLight(Transform rocket)
    {
        var go = new GameObject("Exhaust Light");
        fx.Add(go);
        go.transform.SetParent(rocket, true);
        go.transform.position = rocket.position + Vector3.up * 3f;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.7f, 0.35f); l.range = 260f; l.intensity = 0f; l.shadows = LightShadows.None;
        return l;
    }
}
