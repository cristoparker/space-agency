using System.IO;
using PurrNet;
using PurrNet.Steam;
using PurrNet.Transports;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Resources/Multiplayer/NetSession.prefab: PurrNet's NetworkManager (never starts on its own), the Steam transport
/// (P2P by Steam ID) and a UDP transport for ParrelSync tests on one machine, plus <see cref="NetSession"/>.
/// Also writes steam_appid.txt (480, Valve's test app) when it is missing. Safe to run again.
/// Menu: Tools / Space Agency / Setup Multiplayer.
/// </summary>
public static class MultiplayerSetup
{
    const string Folder = "Assets/Resources/Multiplayer";
    const string PrefabPath = Folder + "/NetSession.prefab";
    const string RulesPath = "Packages/dev.purrnet.purrnet/Defaults/NetworkRules/ServerOwner.asset";
    const string VisibilityPath = "Packages/dev.purrnet.purrnet/Defaults/VisibilityRules/AlwaysVisible.asset";
    public const ushort LocalPort = 5055;

    [MenuItem("Tools/Space Agency/Setup Multiplayer")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Multiplayer");

        var root = new GameObject("NetSession");
        root.SetActive(false);                                   // no Awake while it is being put together
        var steam = root.AddComponent<SteamTransport>();
        var udp = root.AddComponent<UDPTransport>();
        var nm = root.AddComponent<NetworkManager>();
        var session = root.AddComponent<NetSession>();
        session.network = nm; session.steam = steam; session.udp = udp;

        var so = new SerializedObject(nm);
        Set(so, "_startServerFlags", (int)StartFlags.None);
        Set(so, "_startClientFlags", (int)StartFlags.None);
        Set(so, "_dontDestroyOnLoad", false);                    // NetSession keeps the whole object alive itself
        Set(so, "_stopPlayingOnDisconnect", false);
        so.FindProperty("_transport").objectReferenceValue = steam;
        var rules = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RulesPath);
        if (rules == null) Debug.LogError("Setup Multiplayer: PurrNet's default network rules were not found at " + RulesPath);
        so.FindProperty("_networkRules").objectReferenceValue = rules;
        var vis = so.FindProperty("_visibilityRules");
        if (vis != null) vis.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(VisibilityPath);
        so.ApplyModifiedPropertiesWithoutUndo();

        var st = new SerializedObject(steam);
        Set(st, "_peerToPeer", true);
        Set(st, "_dedicatedServer", false);
        st.ApplyModifiedPropertiesWithoutUndo();

        var ut = new SerializedObject(udp);
        Set(ut, "_serverPort", (int)LocalPort);
        Set(ut, "_maxConnections", SteamService.MaxPlayers);
        Set(ut, "_address", "127.0.0.1");
        ut.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(true);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var appId = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
        if (!File.Exists(appId)) File.WriteAllText(appId, "480");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Multiplayer set up: " + PrefabPath + " (Steam P2P + UDP port " + LocalPort + " for local tests).");
    }

    static void Set(SerializedObject so, string field, int v) { var p = so.FindProperty(field); if (p != null) { if (p.propertyType == SerializedPropertyType.Enum) p.enumValueFlag = v; else p.intValue = v; } else Debug.LogWarning("Setup Multiplayer: no field " + field + " on " + so.targetObject.GetType().Name); }
    static void Set(SerializedObject so, string field, bool v) { var p = so.FindProperty(field); if (p != null) p.boolValue = v; else Debug.LogWarning("Setup Multiplayer: no field " + field + " on " + so.targetObject.GetType().Name); }
    static void Set(SerializedObject so, string field, string v) { var p = so.FindProperty(field); if (p != null) p.stringValue = v; else Debug.LogWarning("Setup Multiplayer: no field " + field + " on " + so.targetObject.GetType().Name); }
}
