using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The agency save that a shared game runs on. The whole game keeps its state in PlayerPrefs (see <see cref="Agency"/>),
/// so the host sends its keys and every guest loads them before entering the Game scene. A guest's own single-player
/// save is set aside first and put back when the guest leaves, even if the game was closed in the middle
/// (the backup is checked again on the next start).
/// </summary>
public static class TeamWorld
{
    static readonly string[] StringKeys =
    {
        "SpaceAgency.AgencyName", "SpaceAgency.Logo", "SpaceAgency.Money", "SpaceAgency.Owned", "SpaceAgency.Stats", "SpaceAgency.Accepted",
        "SpaceAgency.Base", "SpaceAgency.Deployment", "SpaceAgency.Flight", "SpaceAgency.RocketBuild",
        "SpaceAgency.RocketDesign", "SpaceAgency.RocketName", "SpaceAgency.PayloadDesign", "SpaceAgency.PayloadName",
        "SpaceAgency.SavedRockets", "SpaceAgency.CustomPayloads",
    };
    static readonly string[] IntKeys = { "SpaceAgency.Registered", "SpaceAgency.AgencyType", "SpaceAgency.Rep", "SpaceAgency.ClientTrust" };

    const string BackupKey = "SpaceAgencyMP.Backup";        // outside the SpaceAgency.* names, so a snapshot never carries it

    [Serializable] class Backup { public List<string> sk = new List<string>(), sv = new List<string>(), ik = new List<string>(); public List<int> iv = new List<int>(); }

    public static bool HasBackup => PlayerPrefs.HasKey(BackupKey);

    /// <summary>The host's agency as it is saved right now.</summary>
    public static WorldMsg Capture()
    {
        var sk = new List<string>(); var sv = new List<string>(); var ik = new List<string>(); var iv = new List<int>();
        foreach (var k in StringKeys) if (PlayerPrefs.HasKey(k)) { sk.Add(k); sv.Add(PlayerPrefs.GetString(k)); }
        foreach (var k in IntKeys) if (PlayerPrefs.HasKey(k)) { ik.Add(k); iv.Add(PlayerPrefs.GetInt(k)); }
        return new WorldMsg { strKeys = sk.ToArray(), strValues = sv.ToArray(), intKeys = ik.ToArray(), intValues = iv.ToArray() };
    }

    /// <summary>Loads the host's agency in place of this player's own (which is kept aside once, the first time).</summary>
    /// <summary>
    /// A ParrelSync clone shares PlayerPrefs with the main editor (same registry keys on Windows): its "own save" is the host's save,
    /// so it must never set one aside or put one back, or leaving the test would roll the host's game back.
    /// </summary>
    static bool SharesPrefsWithHost => PurrNet.Utils.ApplicationContext.isClone;

    public static void Apply(WorldMsg w)
    {
        if (!HasBackup && !SharesPrefsWithHost) SaveBackup();
        if (!SharesPrefsWithHost)                                // a clone already reads the host's own keys: never delete them under it
        {
            foreach (var k in StringKeys) PlayerPrefs.DeleteKey(k);
            foreach (var k in IntKeys) PlayerPrefs.DeleteKey(k);
        }
        if (w.strKeys != null) for (int i = 0; i < w.strKeys.Length && i < w.strValues.Length; i++) if (IsOurs(w.strKeys[i])) PlayerPrefs.SetString(w.strKeys[i], w.strValues[i] ?? "");
        if (w.intKeys != null) for (int i = 0; i < w.intKeys.Length && i < w.intValues.Length; i++) if (IsOurs(w.intKeys[i])) PlayerPrefs.SetInt(w.intKeys[i], w.intValues[i]);
        PlayerPrefs.Save();
        BaseSave.Reload();
    }

    static bool IsOurs(string key) => Array.IndexOf(StringKeys, key) >= 0 || Array.IndexOf(IntKeys, key) >= 0;

    static void SaveBackup()
    {
        var b = new Backup();
        foreach (var k in StringKeys) if (PlayerPrefs.HasKey(k)) { b.sk.Add(k); b.sv.Add(PlayerPrefs.GetString(k)); }
        foreach (var k in IntKeys) if (PlayerPrefs.HasKey(k)) { b.ik.Add(k); b.iv.Add(PlayerPrefs.GetInt(k)); }
        PlayerPrefs.SetString(BackupKey, JsonUtility.ToJson(b));
        PlayerPrefs.Save();
    }

    /// <summary>Puts the guest's own save back. Safe to call any time; does nothing without a backup.</summary>
    public static void RestoreBackup()
    {
        if (!HasBackup || SharesPrefsWithHost) return;
        Backup b = null;
        try { b = JsonUtility.FromJson<Backup>(PlayerPrefs.GetString(BackupKey)); } catch { /* unreadable: drop it */ }
        if (b != null)
        {
            foreach (var k in StringKeys) PlayerPrefs.DeleteKey(k);
            foreach (var k in IntKeys) PlayerPrefs.DeleteKey(k);
            for (int i = 0; i < b.sk.Count && i < b.sv.Count; i++) PlayerPrefs.SetString(b.sk[i], b.sv[i]);
            for (int i = 0; i < b.ik.Count && i < b.iv.Count; i++) PlayerPrefs.SetInt(b.ik[i], b.iv[i]);
        }
        PlayerPrefs.DeleteKey(BackupKey);
        PlayerPrefs.Save();
        BaseSave.Reload();
    }
}
