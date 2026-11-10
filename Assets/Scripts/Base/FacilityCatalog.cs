using UnityEngine;

/// <summary>
/// The models of the facilities that can be built on the empty plots (levels 1..3 each), plus the palette materials.
/// Lives in Resources/FacilityCatalog and is filled by Tools / Space Agency / Rebuild Level.
/// </summary>
[CreateAssetMenu(menuName = "Space Agency/Facility Catalog", fileName = "FacilityCatalog")]
public class FacilityCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string id;
        public GameObject[] levels = new GameObject[3];
        [Tooltip("Scale so the biggest level fits the plot.")] public float scale = 1f;
    }

    public Entry[] entries;
    public Material baseMaterial, lampOff;

    public Entry Get(string id)
    {
        if (entries == null) return null;
        foreach (var e in entries) if (e.id == id) return e;
        return null;
    }

    /// <summary>Creates the model of a facility level under a plot (same position and facing as the plot).</summary>
    public GameObject Spawn(string id, int level, Transform plot)
    {
        var e = Get(id);
        if (e == null || level < 1 || level > e.levels.Length || e.levels[level - 1] == null) return null;
        var go = Instantiate(e.levels[level - 1], plot.position, plot.rotation, plot);
        go.transform.localScale = Vector3.one * e.scale;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>();
            int subs = mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 1;
            r.sharedMaterials = subs > 1 ? new[] { baseMaterial, lampOff } : new[] { baseMaterial };
        }
        return go;
    }
}
