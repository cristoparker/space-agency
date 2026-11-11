using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>One layer of the agency logo: a shape, a palette colour and a size.</summary>
[Serializable]
public class LogoLayer
{
    public int shape;      // index into LogoArt.ShapeNames
    public int color;      // index into LogoArt.Palette
    public int size = 2;   // 1..4
}

[Serializable]
public class LogoDesign
{
    public List<LogoLayer> layers = new List<LogoLayer>();
    public static LogoDesign Default()
    {
        var l = new LogoDesign();
        l.layers.Add(new LogoLayer { shape = 0, color = 5, size = 4 });
        l.layers.Add(new LogoLayer { shape = 3, color = 0, size = 3 });
        return l;
    }
    public string ToJson() => JsonUtility.ToJson(this);
    public static LogoDesign FromJson(string json)
    {
        try { var d = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<LogoDesign>(json); return d != null && d.layers != null && d.layers.Count > 0 ? d : Default(); }
        catch { return Default(); }
    }
}

/// <summary>
/// The player's agency: name, type, starting stats, logo, money and reputation.
/// Everything is kept in PlayerPrefs so the Main Menu and the Game scene share it.
/// </summary>
public static class Agency
{
    public const long BaseMoney = 24_875_000_000L;
    public static readonly string[] Types = { "Government", "Private Company", "Research Institute", "Commercial Startup" };
    public static readonly string[] TypeNotes =
    {
        "Large budget, slow growth.", "Balanced start.", "Less money, faster reputation.", "Small budget, cheaper rockets.",
    };
    public static readonly string[] StatNames = { "Engineering", "Science", "Finance", "Safety" };
    public static readonly string[] StatNotes =
    {
        "Cheaper rocket builds", "Faster reputation", "More starting money", "Fewer launch failures (coming soon)",
    };
    public const int StatPoints = 8, StatMax = 5;

    public static event Action Changed;
    static string K(string s) => "SpaceAgency." + s;

    public static bool Registered => PlayerPrefs.GetInt(K("Registered"), 0) == 1;
    public static string Name => PlayerPrefs.GetString(K("AgencyName"), "Space Agency");
    public static int TypeIndex => Mathf.Clamp(PlayerPrefs.GetInt(K("AgencyType"), 1), 0, Types.Length - 1);
    public static string Type => Types[TypeIndex];
    /// <summary>The saved logo as text; lets the HUD notice a change without parsing it.</summary>
    public static string LogoJson => PlayerPrefs.GetString(K("Logo"), "");
    public static LogoDesign Logo => LogoDesign.FromJson(PlayerPrefs.GetString(K("Logo"), ""));

    public static int Stat(int i)
    {
        var parts = PlayerPrefs.GetString(K("Stats"), "1,1,1,1").Split(',');
        return i < parts.Length && int.TryParse(parts[i], out var v) ? Mathf.Clamp(v, 1, StatMax) : 1;
    }

    // ---------------------------------------------------------------- money
    public static long Money => long.TryParse(PlayerPrefs.GetString(K("Money"), BaseMoney.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : BaseMoney;

    static void SetMoney(long v)
    {
        PlayerPrefs.SetString(K("Money"), v.ToString(CultureInfo.InvariantCulture));
        Changed?.Invoke();
    }

    public static bool CanAfford(long v) => Money >= v;

    public static bool TrySpend(long v)
    {
        if (v < 0 || Money < v) return false;
        SetMoney(Money - v);
        return true;
    }

    public static void Earn(long v) => SetMoney(Money + Math.Max(0, v));

    /// <summary>Engineering makes rockets a little cheaper (3% per point above 1).</summary>
    public static float CostFactor => Mathf.Max(0.5f, 1f - 0.03f * (Stat(0) - 1) - (TypeIndex == 3 ? 0.1f : 0f)
        - (BaseSave.Has("test_engine") ? 0.04f : 0f) - (BaseSave.Has("test_struct") ? 0.03f : 0f));

    /// <summary>Passing the comms link test makes contracts pay 10% more.</summary>
    public static float RewardFactor => 1f + (BaseSave.Has("test_comms") ? 0.1f : 0f);

    // ---------------------------------------------------------------- reputation
    public static int RepPoints => PlayerPrefs.GetInt(K("Rep"), 0);
    public static int RepLevel => 1 + RepPoints / 100;
    public static float RepProgress => (RepPoints % 100) / 100f;

    public static void AddReputation(int pts)
    {
        float mul = (TypeIndex == 2 ? 1.25f : 1f) * (1f + 0.1f * (Stat(1) - 1)) * (1f + 0.08f * BaseSave.Level("sci"));
        PlayerPrefs.SetInt(K("Rep"), Mathf.Max(0, RepPoints + Mathf.RoundToInt(pts * (pts > 0 ? mul : 1f))));
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- resources (a simple readout for the HUD)
    public static int Staff => 20 + 5 * RepLevel;
    public static int Facilities => 1;

    // ---------------------------------------------------------------- creating / loading
    public static void Create(string name, int type, int[] stats, LogoDesign logo)
    {
        // fresh game: wipe the previous run's progress
        foreach (var key in new[] { "SpaceAgency.Deployment", "SpaceAgency.RocketBuild", "SpaceAgency.Flight", "SpaceAgency.ClientTrust", "SpaceAgency.RocketDesign", "SpaceAgency.RocketName", "SpaceAgency.PayloadDesign", "SpaceAgency.PayloadName", K("Rep"), K("Owned"), K("Accepted") })
            PlayerPrefs.DeleteKey(key);
        BaseSave.Reset();                                     // buildings, programmes and certificates start over
        PlayerPrefs.SetString(K("AgencyName"), string.IsNullOrWhiteSpace(name) ? "Space Agency" : name.Trim());
        PlayerPrefs.SetInt(K("AgencyType"), Mathf.Clamp(type, 0, Types.Length - 1));
        PlayerPrefs.SetString(K("Stats"), string.Join(",", stats));
        PlayerPrefs.SetString(K("Logo"), (logo ?? LogoDesign.Default()).ToJson());
        PlayerPrefs.SetInt(K("Registered"), 1);

        long start = BaseMoney;
        if (type == 0) start = (long)(BaseMoney * 1.2);
        else if (type == 2) start = (long)(BaseMoney * 0.8);
        else if (type == 3) start = (long)(BaseMoney * 0.6);
        start += (long)(BaseMoney * 0.05 * (stats[2] - 1));
        SetMoney(start);
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- blueprints the player owns
    public static bool OwnsBlueprint(string id)
    {
        foreach (var s in PlayerPrefs.GetString(K("Owned"), "").Split('|')) if (s == id) return true;
        return false;
    }

    public static void AddBlueprint(string id)
    {
        if (OwnsBlueprint(id)) return;
        var cur = PlayerPrefs.GetString(K("Owned"), "");
        PlayerPrefs.SetString(K("Owned"), cur.Length == 0 ? id : cur + "|" + id);
        Changed?.Invoke();
    }
}
