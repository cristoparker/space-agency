using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable]
public class SavedRocket
{
    public string name, design, savedAt;
    public float costM, launchKg, dv;
    public bool valid;
}

[Serializable]
class SavedRocketList { public List<SavedRocket> items = new List<SavedRocket>(); }

/// <summary>The player's saved rocket designs (kept in PlayerPrefs).</summary>
public static class RocketSaves
{
    const string Key = "SpaceAgency.SavedRockets";

    public static List<SavedRocket> All()
    {
        try { return JsonUtility.FromJson<SavedRocketList>(PlayerPrefs.GetString(Key, "{}"))?.items ?? new List<SavedRocket>(); }
        catch { return new List<SavedRocket>(); }
    }

    static void Write(List<SavedRocket> list) => PlayerPrefs.SetString(Key, JsonUtility.ToJson(new SavedRocketList { items = list }));

    public static void Upsert(SavedRocket r)
    {
        var list = All();
        int i = list.FindIndex(x => x.name == r.name);
        if (i >= 0) list[i] = r; else list.Add(r);
        Write(list);
    }

    public static void Delete(string name)
    {
        var list = All();
        list.RemoveAll(x => x.name == name);
        Write(list);
    }
}

/// <summary>A rocket sent to the launch pad, waiting for its launch date (launching itself is coded later).</summary>
[Serializable]
public class Deployment
{
    public string rocketName, design, missionId, missionName, date;   // date = yyyy-MM-dd (game calendar)
}
