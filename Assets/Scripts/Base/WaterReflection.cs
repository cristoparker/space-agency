using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Planar reflection for the toon water: renders the scene mirrored in the water plane into a half-resolution texture
/// (global _WaterReflectionTex). SpaceAgency/Toon/Water posterises and wobbles it for a cartoon look.
/// </summary>
[DisallowMultipleComponent]
public class WaterReflection : MonoBehaviour
{
    [Range(0.2f, 1f)] public float resolutionScale = 0.5f;
    public float farClip = 1800f;
    public LayerMask reflectedLayers = ~0;

    Camera reflectCam;
    RenderTexture rt;
    static bool rendering;

    void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBegin;

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBegin;
        if (reflectCam != null) Destroy(reflectCam.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    void Setup(Camera cam)
    {
        int w = Mathf.Max(64, Mathf.RoundToInt(cam.pixelWidth * resolutionScale)), h = Mathf.Max(64, Mathf.RoundToInt(cam.pixelHeight * resolutionScale));
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "WaterReflection", antiAliasing = 1 };
        }
        if (reflectCam == null)
        {
            var go = new GameObject("Water Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
            reflectCam = go.AddComponent<Camera>();
            reflectCam.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false; data.renderPostProcessing = false;
        }
    }

    void OnBegin(ScriptableRenderContext ctx, Camera cam)
    {
        if (rendering || cam.name == "FacilityPreviewCam" || (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView)) return;
        if (!isActiveAndEnabled) return;
        Setup(cam);

        float y = transform.position.y;
        reflectCam.CopyFrom(cam);
        reflectCam.enabled = false;
        reflectCam.targetTexture = null;
        reflectCam.cullingMask = reflectedLayers & ~(1 << gameObject.layer);
        reflectCam.farClipPlane = Mathf.Min(cam.farClipPlane, farClip);
        reflectCam.clearFlags = CameraClearFlags.Skybox;

        // mirror the camera in the plane y = water level
        Vector4 plane = new Vector4(0f, 1f, 0f, -y);
        Matrix4x4 refl = Reflection(plane);
        reflectCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;
        Vector3 cp = reflectCam.worldToCameraMatrix.MultiplyPoint(new Vector3(0f, y, 0f));
        Vector3 cn = reflectCam.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
        Vector4 clip = new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn) + 0.05f);
        reflectCam.projectionMatrix = cam.CalculateObliqueMatrix(clip);
        reflectCam.transform.position = refl.MultiplyPoint(cam.transform.position);

        rendering = true;
        GL.invertCulling = true;
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(reflectCam, request)) RenderPipeline.SubmitRenderRequest(reflectCam, request);
        }
        finally { GL.invertCulling = false; rendering = false; }
        Shader.SetGlobalTexture("_WaterReflectionTex", rt);
    }

    static Matrix4x4 Reflection(Vector4 p)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y; m.m02 = -2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
        m.m10 = -2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
        m.m20 = -2f * p.z * p.x; m.m21 = -2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
        return m;
    }
}
