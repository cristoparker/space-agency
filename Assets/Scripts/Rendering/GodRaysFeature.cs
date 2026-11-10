using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Adds screen-space god rays (light shafts) to the main camera, pointing at the sun. Runs before the post-processing, so the bloom
/// picks the shafts up and makes them glow. Added to the renderer by Tools / Space Agency / Setup Visual Effects.
/// </summary>
public class GodRaysFeature : ScriptableRendererFeature
{
    public Shader shader;
    [Range(0f, 2f)] public float intensity = 0.65f;
    [Range(0.8f, 0.99f)] public float decay = 0.955f;
    [Range(0.2f, 1f)] public float length = 0.85f;
    [Range(0.3f, 1f)] public float threshold = 0.72f;
    public Color tint = new Color(1f, 0.92f, 0.75f, 1f);

    Material material;
    Pass pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find("SpaceAgency/PostFX/GodRays");
        // a shader that failed to compile must never reach the screen (it would turn the whole picture black): then the pass is simply skipped
        material = shader != null && shader.isSupported ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new Pass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        pass.requiresIntermediateTexture = true;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var cam = renderingData.cameraData.camera;
        if (material == null || renderingData.cameraData.cameraType != CameraType.Game || !cam.CompareTag("MainCamera")) return;
        if (RenderSettings.sun == null) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); }

    class Pass : ScriptableRenderPass
    {
        readonly GodRaysFeature f;
        public Pass(GodRaysFeature feature) { f = feature; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var camData = frameData.Get<UniversalCameraData>();
            var cam = camData.camera;
            var sun = RenderSettings.sun;
            if (sun == null) return;

            // where the sun is on the screen, and how visible it is (in front of the camera, above the horizon)
            Vector3 toSun = -sun.transform.forward;
            float inFront = Vector3.Dot(cam.transform.forward, toSun);
            float vis = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.35f, inFront)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0f, 0.2f, toSun.y)) * Mathf.Clamp01(sun.intensity / 0.9f);
            Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + toSun * 5000f);
            var mat = f.material;
            mat.SetVector("_GodRayParams", new Vector4(f.intensity, f.decay, f.length, f.threshold));
            mat.SetVector("_GodRaySun", new Vector4(vp.x, vp.y, vis, 0f));
            mat.SetColor("_GodRayTint", f.tint);

            var src = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            desc.name = "_GodRaysTarget"; desc.clearBuffer = false;
            var dst = renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, mat, 0), "God Rays");
            resources.cameraColor = dst;
        }
    }
}
