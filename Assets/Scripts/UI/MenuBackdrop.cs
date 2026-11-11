using UnityEngine;

/// <summary>
/// The main menu's moving backdrop: stills rendered from the game's own scene (golden hour, night, day, dusk from the air) that drift slowly,
/// like a cinematic camera, and cross-fade into each other. A flat dark tint (in the shader) keeps the logo and the menu easy to read.
/// The pictures are drawn as an opaque full-screen quad on the camera, not as UI, so the glass buttons can blur them (URP Opaque Texture). The stills live in Resources/MenuBackdrop (shot_0, shot_1, ...); with none present the menu keeps its plain colour backdrop.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    const float Hold = 7.5f, Fade = 2.4f, Life = Hold + Fade;
    const float Aspect = 16f / 9f;

    class Layer { public int shot; public float age; public Vector2 drift; }

    Texture2D[] shots;
    Material mat;
    Layer cur, next;
    int index;
    GameObject quad;

    /// <summary>Builds the backdrop on the main camera (the UI canvas stays transparent above it).</summary>
    public static void Create(Camera cam)
    {
        var m = Resources.Load<Material>("MenuBackdrop/Backdrop");
        if (cam == null || m == null) return;
        var list = new System.Collections.Generic.List<Texture2D>();
        for (int i = 0; i < 12; i++)
        {
            var t = Resources.Load<Texture2D>("MenuBackdrop/shot_" + i);
            if (t == null) break;
            list.Add(t);
        }
        if (list.Count == 0) return;
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Menu Backdrop";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(cam.transform, false);
        q.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 1f);
        var mr = q.GetComponent<MeshRenderer>();
        var inst = new Material(m);
        mr.sharedMaterial = inst;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        var mb = q.AddComponent<MenuBackdrop>();
        mb.quad = q; mb.mat = inst; mb.shots = list.ToArray();
        mb.cur = mb.NewLayer(0); mb.next = mb.NewLayer(0); mb.index = 0;
        mb.Apply();
    }

    Layer NewLayer(int shot)
    {
        return new Layer { shot = shot, age = 0f, drift = new Vector2(Random.value < 0.5f ? -1f : 1f, Random.Range(-0.4f, 0.4f)) * 0.012f };
    }

    // cover the screen: crop the picture if the window is wider or narrower than 16:9
    Vector2 Cover()
    {
        float screen = Screen.height > 0 ? (float)Screen.width / Screen.height : Aspect;
        return screen >= Aspect ? new Vector2(1f, screen / Aspect) : new Vector2(Aspect / screen, 1f);
    }

    static Vector4 Xform(Layer l, Vector2 cover)
    {
        float k = Mathf.Clamp01(l.age / Life);
        float zoom = Mathf.Lerp(1.07f, 1.17f, k);                                // a slow push in
        return new Vector4(zoom * cover.x, zoom * cover.y, l.drift.x * (k - 0.5f), l.drift.y * (k - 0.5f));   // and a gentle sideways drift
    }

    void Apply()
    {
        var c = Cover();
        mat.SetTexture("_A", shots[cur.shot]); mat.SetTexture("_B", shots[next.shot]);
        mat.SetVector("_TA", Xform(cur, c)); mat.SetVector("_TB", Xform(next, c));
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        cur.age += dt;
        float fade = 0f;
        if (shots.Length > 1 && cur.age > Hold)
        {
            if (next.age <= 0f) { index = (index + 1) % shots.Length; next = NewLayer(index); }
            next.age += dt;
            fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((cur.age - Hold) / Fade));
            if (fade >= 1f) { cur = next; next = NewLayer(index); fade = 0f; }
        }
        mat.SetFloat("_T", fade);
        Apply();
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}
