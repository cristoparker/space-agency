using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>One rocket part's stats (loaded from parts-catalog.json, written by ArtSource/build_parts.py).</summary>
[Serializable]
public class PartInfo
{
    public string id, model, category, size, diameterClass, note, payloadType;
    public float bottomZ, heightM, widthM, depthM;
    public float fuelKg, dryMassKg, costM, thrustKn, thrustVacKn, ispSl, ispVac, gimbalDeg;
    public float powerW, dataMbps, storageWh, massKg, commMbps, payloadMaxKg, reactionWheelNm, chuteDragArea;
    public int bells, payloadSlots, crew, controlLevel, tris;
    public bool heatShield, reusable;

    public float Radius => widthM * 0.5f;

    // Stack parts that change size class are named "M-L" (bottom class - top class).
    public string BottomClass => !string.IsNullOrEmpty(diameterClass) && diameterClass.Contains("-") ? diameterClass.Split('-')[0] : diameterClass;
    public string TopClass => !string.IsNullOrEmpty(diameterClass) && diameterClass.Contains("-") ? diameterClass.Split('-')[1] : diameterClass;

    static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "Thr_Ion", "Ion Thruster" }, { "Thr_RCS_Block", "RCS Block" }, { "Com_Whip", "Whip Antenna" }, { "Com_Patch", "Patch Antenna" },
        { "Com_Dish_S", "Dish Antenna S" }, { "Com_Dish_L", "Dish Antenna L" }, { "Com_PhasedArray", "Phased Array" },
        { "Pow_Solar_S", "Solar Panels S" }, { "Pow_Solar_L", "Solar Panels L" }, { "Pow_Battery", "Battery" }, { "Pow_RTG", "RTG" },
        { "Rec_LandingLeg", "Landing Leg" }, { "Rec_GridFin", "Grid Fin" }, { "Rec_Parachute_S", "Parachute S" }, { "Rec_Parachute_M", "Parachute M" },
        { "Head_Nose_S", "Nose Cone S" }, { "Head_Nose_M", "Nose Cone M" }, { "Head_Nose_L", "Nose Cone L" },
        { "Eng_S_SolidKick", "Solid Kick Motor" },
    };

    public string DisplayName
    {
        get
        {
            if (Names.TryGetValue(id, out var n)) return n;
            string s = id;
            foreach (var pre in new[] { "Eng_", "Head_", "Pay_", "Dec_", "Adp_", "Avi_" })
                if (s.StartsWith(pre))
                {
                    string word = pre == "Eng_" ? "Engine " : pre == "Dec_" ? "Decoupler " : pre == "Adp_" ? "Adapter " : pre == "Avi_" ? "Avionics " : "";
                    s = word + s.Substring(pre.Length);
                    break;
                }
            s = s.Replace("_", " ");
            return Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ");
        }
    }

    public static int SizeRank(string s) => s == "S" ? 0 : s == "M" ? 1 : s == "L" ? 2 : 1;
}

[Serializable]
public class PartCatalogFile { public PartInfo[] parts; }

/// <summary>All parts: stats from the JSON catalog plus the Blender models. Create with Tools / Space Agency / Setup Rocket Builder.</summary>
[CreateAssetMenu(menuName = "Space Agency/Part Library", fileName = "PartLibrary")]
public class PartLibrary : ScriptableObject
{
    [Serializable] public struct ModelEntry { public string id; public GameObject prefab; }
    [Serializable] public struct IconEntry { public string id; public Sprite sprite; }

    public TextAsset catalog;
    public ModelEntry[] models;
    [Tooltip("Part_0..Part_7: same palette, shifted so only the livery colours change.")]
    public Material[] liveryMaterials;
    public IconEntry[] icons;
    [Tooltip("Unlit, cull front: the cyan outline drawn around the selected part.")]
    public Material outlineMaterial;
    [Tooltip("Translucent livery material for the interstage covers shown in the rocket builder (engine stays visible through it).")]
    public Material ghostMaterial;

    List<PartInfo> parts;
    Dictionary<string, PartInfo> infoById;
    Dictionary<string, GameObject> prefabById;
    Dictionary<string, Sprite> iconById;

    void Init()
    {
        if (parts != null) return;
        parts = new List<PartInfo>();
        infoById = new Dictionary<string, PartInfo>();
        prefabById = new Dictionary<string, GameObject>();
        iconById = new Dictionary<string, Sprite>();
        if (icons != null) foreach (var i in icons) if (i.sprite != null) iconById[i.id] = i.sprite;
        if (catalog != null)
        {
            var file = JsonUtility.FromJson<PartCatalogFile>(catalog.text);
            if (file?.parts != null) foreach (var p in file.parts) { parts.Add(p); infoById[p.id] = p; }
        }
        if (models != null) foreach (var m in models) if (m.prefab != null) prefabById[m.id] = m.prefab;
    }

    void OnEnable() => parts = null;

    public IReadOnlyList<PartInfo> Parts { get { Init(); return parts; } }

    public PartInfo Get(string id)
    {
        Init();
        return !string.IsNullOrEmpty(id) && infoById.TryGetValue(id, out var p) ? p : null;
    }

    public GameObject Prefab(string id)
    {
        Init();
        return prefabById.TryGetValue(id, out var g) ? g : null;
    }

    public Sprite Icon(string id)
    {
        Init();
        return !string.IsNullOrEmpty(id) && iconById.TryGetValue(id, out var s) ? s : null;
    }

    public Material Livery(int i) => liveryMaterials != null && liveryMaterials.Length > 0 ? liveryMaterials[Mathf.Clamp(i, 0, liveryMaterials.Length - 1)] : null;
}
