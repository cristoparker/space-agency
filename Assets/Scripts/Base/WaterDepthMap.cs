using UnityEngine;

/// <summary>
/// Gives the toon water the terrain height as a texture, so the water can know how deep it is without reading the scene depth.
/// That lets the water be drawn as an opaque surface, which is what makes the UI glass blur show the water behind it.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterDepthMap : MonoBehaviour
{
    public int width = 1536, height = 512;
    Texture2D map;

    void OnEnable() => Build();

    void OnDisable()
    {
        if (map != null) { if (Application.isPlaying) Destroy(map); else DestroyImmediate(map); map = null; }
    }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        var td = terrain.terrainData;
        var pos = terrain.transform.position;
        float[,] h = td.GetInterpolatedHeights(0f, 0f, width, height, 1f / (width - 1), 1f / (height - 1));   // [y, x] in metres
        var data = new float[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) data[y * width + x] = pos.y + h[y, x];
        if (map == null || map.width != width || map.height != height)
        {
            if (map != null) { if (Application.isPlaying) Destroy(map); else DestroyImmediate(map); }
            map = new Texture2D(width, height, TextureFormat.RFloat, false, true) { name = "Water Depth Map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        }
        map.SetPixelData(data, 0);
        map.Apply(false);
        Shader.SetGlobalTexture("_WaterDepthMap", map);
        Shader.SetGlobalVector("_WaterDepthParams", new Vector4(pos.x, pos.z, 1f / td.size.x, 1f / td.size.z));
    }
}
