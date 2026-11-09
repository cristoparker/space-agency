#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports missions from a JSON file (format: { "missions": [ ... ] }) into MissionDefinition assets
/// and adds them to the MissionDatabase. Re-importing updates existing assets by id.
/// Use: Tools > Missions > Import From JSON...
/// </summary>
public static class MissionJsonImporter
{
    [Serializable] class Root { public Dto[] missions; }
    [Serializable] class Dto
    {
        public string id, name, missionType, target, objective;
        public long budget;
        public float massLimit;
        public PayloadDto payload;
        public DurationDto duration;
        public RequirementDto[] specialRequirements;
    }
    [Serializable] class PayloadDto { public string type, description; public int quantity; }
    [Serializable] class DurationDto { public float value; public string unit; }
    [Serializable] class RequirementDto { public string type, value, unit; public float minimum; }

    public const string DefaultFolder = "Assets/Data/Missions";

    [MenuItem("Tools/Missions/Import From JSON...")]
    static void Menu()
    {
        string path = EditorUtility.OpenFilePanel("Import missions JSON", "Assets", "json");
        if (!string.IsNullOrEmpty(path)) Debug.Log(ImportFile(path, DefaultFolder));
    }

    public static string ImportFile(string jsonPath, string folder)
    {
        var root = JsonUtility.FromJson<Root>(File.ReadAllText(jsonPath));
        if (root?.missions == null) return "No missions found in " + jsonPath;

        string defsFolder = folder + "/Definitions";
        Directory.CreateDirectory(defsFolder);
        AssetDatabase.Refresh();

        string dbPath = folder + "/MissionDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<MissionDatabase>(dbPath);
        if (db == null) { db = ScriptableObject.CreateInstance<MissionDatabase>(); AssetDatabase.CreateAsset(db, dbPath); }

        int created = 0, updated = 0;
        foreach (var d in root.missions)
        {
            string assetPath = $"{defsFolder}/{d.id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<MissionDefinition>(assetPath);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<MissionDefinition>();

            def.id = d.id; def.displayName = d.name; def.missionType = d.missionType; def.target = d.target;
            def.budget = d.budget; def.massLimit = d.massLimit; def.objective = d.objective;
            def.payload = new MissionPayload { type = d.payload?.type, quantity = d.payload != null ? d.payload.quantity : 1, description = d.payload?.description };
            def.duration = new MissionDuration { value = d.duration != null ? d.duration.value : 0, unit = ParseUnit(d.duration?.unit) };
            def.specialRequirements.Clear();
            if (d.specialRequirements != null)
                foreach (var r in d.specialRequirements)
                    def.specialRequirements.Add(new MissionRequirement { type = r.type, value = r.value, unit = r.unit, hasMinimum = r.minimum > 0f, minimum = r.minimum });

            if (isNew) { AssetDatabase.CreateAsset(def, assetPath); created++; } else { EditorUtility.SetDirty(def); updated++; }
            if (!db.missions.Contains(def)) db.missions.Add(def);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        return $"Missions imported: {created} created, {updated} updated.";
    }

    static DurationUnit ParseUnit(string s)
    {
        s = (s ?? "days").ToLowerInvariant();
        if (s.StartsWith("year")) return DurationUnit.Years;
        if (s.StartsWith("month")) return DurationUnit.Months;
        return DurationUnit.Days;
    }
}
#endif
